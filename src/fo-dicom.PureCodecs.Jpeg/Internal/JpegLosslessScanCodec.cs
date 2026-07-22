using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegLosslessScanCodec
    {
        private readonly JpegHuffmanTable _table;

        private JpegLosslessScanCodec(JpegHuffmanTable table)
        {
            _table = table;
        }

        public static JpegLosslessScanCodec CreateDefault()
        {
            return new JpegLosslessScanCodec(CreateDefaultHuffmanTable());
        }

        internal static JpegHuffmanTable CreateDefaultHuffmanTableForFrame()
        {
            return CreateDefaultHuffmanTable();
        }

        internal static JpegHuffmanTable CreateOptimalHuffmanTableForFrame(
            int[] samples,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            int selectionValue)
        {
            ValidateShape(samples, width, height, samplePrecision, width * height * componentCount);
            ValidateComponentCount(componentCount);

            var frequencies = new int[256];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var component = 0; component < componentCount; component++)
                    {
                        var index = GetInterleavedIndex(width, componentCount, x, y, component);
                        var sample = samples[index];
                        ValidateSample(sample, samplePrecision);

                        var prediction = PredictInterleaved(samples, width, componentCount, x, y, component, samplePrecision, selectionValue);
                        var difference = NormalizeDifferenceForEntropy(sample - prediction, samplePrecision);
                        frequencies[GetCategory(difference)]++;
                    }
                }
            }

            return JpegHuffmanTable.CreateOptimal(frequencies);
        }

        public static JpegLosslessScanCodec Create(JpegHuffmanTable table)
        {
            return new JpegLosslessScanCodec(table ?? throw new ArgumentNullException(nameof(table)));
        }

        public byte[] Encode(int[] samples, int width, int height, int samplePrecision, int selectionValue)
        {
            return EncodeInterleaved(samples, width, height, componentCount: 1, samplePrecision, selectionValue);
        }

        public byte[] EncodeInterleaved(
            int[] samples,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            int selectionValue)
        {
            ValidateShape(samples, width, height, samplePrecision, width * height * componentCount);
            ValidateComponentCount(componentCount);
            if (samples.Length != width * height * componentCount)
            {
                throw CreateException($"JPEG lossless scan sample count {samples.Length} does not match dimensions {width}x{height}x{componentCount}.");
            }

            var writer = new JpegEntropyBitWriter();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var component = 0; component < componentCount; component++)
                    {
                        var index = GetInterleavedIndex(width, componentCount, x, y, component);
                        var sample = samples[index];
                        ValidateSample(sample, samplePrecision);

                        var prediction = PredictInterleaved(samples, width, componentCount, x, y, component, samplePrecision, selectionValue);
                        var difference = NormalizeDifferenceForEntropy(sample - prediction, samplePrecision);
                        var category = GetCategory(difference);
                        _table.Encode(writer, category);
                        if (category > 0)
                        {
                            writer.WriteBits(EncodeMagnitude(difference, category), category);
                        }
                    }
                }
            }

            return writer.ToArray();
        }

        public int[] Decode(byte[] encoded, int width, int height, int samplePrecision, int selectionValue)
        {
            return DecodeInterleaved(encoded, width, height, componentCount: 1, samplePrecision, selectionValue);
        }

        public int[] DecodeInterleaved(
            byte[] encoded,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            int selectionValue)
        {
            ValidateDimensions(width, height, samplePrecision);
            ValidateComponentCount(componentCount);
            return DecodeInterleaved(
                encoded,
                width,
                height,
                componentCount,
                samplePrecision,
                selectionValue,
                new int[width * height * componentCount]);
        }

        public int[] DecodeInterleaved(
            byte[] encoded,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            int selectionValue,
            int[] samples)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            ValidateDimensions(width, height, samplePrecision);
            ValidateComponentCount(componentCount);
            var sampleCount = width * height * componentCount;
            if (samples.Length < sampleCount)
            {
                throw CreateException($"JPEG lossless scan sample workspace {samples.Length} is smaller than expected length {sampleCount}.");
            }

            var reader = new JpegEntropyBitReader(encoded);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var component = 0; component < componentCount; component++)
                    {
                        var category = _table.Decode(reader);
                        if (category < 0 || category > samplePrecision + 1)
                        {
                            throw CreateException($"JPEG lossless scan category {category} is outside the supported range.");
                        }

                        var difference = category == 0
                            ? 0
                            : category == 16
                                ? 1 << 15
                                : DecodeMagnitude(reader.ReadBits(category), category);
                        var prediction = PredictInterleaved(samples, width, componentCount, x, y, component, samplePrecision, selectionValue);
                        var sample = NormalizeSample(prediction + difference, samplePrecision);
                        ValidateSample(sample, samplePrecision);
                        samples[GetInterleavedIndex(width, componentCount, x, y, component)] = sample;
                    }
                }
            }

            return samples;
        }

        private static JpegHuffmanTable CreateDefaultHuffmanTable()
        {
            var counts = new byte[16];
            counts[7] = 17;

            var values = new byte[17];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = (byte)index;
            }

            return JpegHuffmanTable.Build(counts, values);
        }

        private static int Predict(int[] samples, int width, int x, int y, int samplePrecision, int selectionValue)
        {
            var left = x > 0 ? samples[y * width + x - 1] : 0;
            var above = y > 0 ? samples[(y - 1) * width + x] : 0;
            var upperLeft = x > 0 && y > 0 ? samples[(y - 1) * width + x - 1] : 0;
            return JpegLosslessPredictor.PredictSample(selectionValue, samplePrecision, x, y, left, above, upperLeft);
        }

        private static int PredictInterleaved(
            int[] samples,
            int width,
            int componentCount,
            int x,
            int y,
            int component,
            int samplePrecision,
            int selectionValue)
        {
            if (componentCount == 1)
            {
                return Predict(samples, width, x, y, samplePrecision, selectionValue);
            }

            var left = x > 0 ? samples[GetInterleavedIndex(width, componentCount, x - 1, y, component)] : 0;
            var above = y > 0 ? samples[GetInterleavedIndex(width, componentCount, x, y - 1, component)] : 0;
            var upperLeft = x > 0 && y > 0 ? samples[GetInterleavedIndex(width, componentCount, x - 1, y - 1, component)] : 0;
            return JpegLosslessPredictor.PredictSample(selectionValue, samplePrecision, x, y, left, above, upperLeft);
        }

        private static int GetInterleavedIndex(int width, int componentCount, int x, int y, int component)
        {
            return ((y * width) + x) * componentCount + component;
        }

        private static int GetCategory(int difference)
        {
            var magnitude = Math.Abs(difference);
            var category = 0;
            while (magnitude > 0)
            {
                category++;
                magnitude >>= 1;
            }

            return category;
        }

        private static int NormalizeDifferenceForEntropy(int difference, int samplePrecision)
        {
            // libijg16 stores lossless differences in a signed 16-bit JDIFF row.
            // At 16-bit precision, preserve that two's-complement wrap before
            // deriving the Huffman category and amplitude bits.
            return samplePrecision == 16 ? unchecked((short)difference) : difference;
        }

        private static int EncodeMagnitude(int difference, int category)
        {
            if (difference >= 0)
            {
                return difference;
            }

            return difference + ((1 << category) - 1);
        }

        private static int DecodeMagnitude(int encoded, int category)
        {
            var threshold = 1 << (category - 1);
            if (encoded >= threshold)
            {
                return encoded;
            }

            return encoded - ((1 << category) - 1);
        }

        private static int NormalizeSample(int sample, int samplePrecision)
        {
            var modulus = 1 << samplePrecision;
            if (sample < 0)
            {
                return sample + modulus;
            }

            if (sample >= modulus)
            {
                return sample - modulus;
            }

            return sample;
        }

        private static void ValidateShape(int[] samples, int width, int height, int samplePrecision, int expectedLength)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            ValidateDimensions(width, height, samplePrecision);
            if (samples.Length != expectedLength)
            {
                throw CreateException($"JPEG lossless scan sample count {samples.Length} does not match expected length {expectedLength}.");
            }
        }

        private static void ValidateDimensions(int width, int height, int samplePrecision)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (samplePrecision < 2 || samplePrecision > 16)
            {
                throw CreateException($"JPEG lossless scan sample precision {samplePrecision} is not supported.");
            }
        }

        private static void ValidateComponentCount(int componentCount)
        {
            if (componentCount != 1 && componentCount != 3)
            {
                throw CreateException($"JPEG lossless scan component count {componentCount} is not supported.");
            }
        }

        private static void ValidateSample(int sample, int samplePrecision)
        {
            var maximum = (1 << samplePrecision) - 1;
            if (sample < 0 || sample > maximum)
            {
                throw CreateException($"JPEG lossless scan sample {sample} is outside the valid range 0..{maximum}.");
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
