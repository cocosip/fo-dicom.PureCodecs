using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    internal sealed class JpegLosslessScanCodec
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
                        // JPEG Lossless category 16 represents the sole magnitude
                        // value 32768 and has no following amplitude bits.
                        if (category > 0 && category != 16)
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
            return DecodeInterleaved(
                encoded,
                width,
                height,
                componentCount,
                samplePrecision,
                selectionValue,
                samples,
                restartInterval: 0);
        }

        internal int[] DecodeInterleaved(
            byte[] encoded,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            int selectionValue,
            int[] samples,
            int restartInterval)
        {
            var componentIndices = new int[componentCount];
            var huffmanTables = new JpegHuffmanTable[componentCount];
            for (var component = 0; component < componentCount; component++)
            {
                componentIndices[component] = component;
                huffmanTables[component] = _table;
            }

            return DecodeInterleavedComponents(
                encoded,
                width,
                height,
                componentCount,
                componentIndices,
                huffmanTables,
                samplePrecision,
                selectionValue,
                samples,
                restartInterval);
        }

        internal static int[] DecodeInterleavedComponents(
            byte[] encoded,
            int width,
            int height,
            int frameComponentCount,
            int[] componentIndices,
            JpegHuffmanTable[] huffmanTables,
            int samplePrecision,
            int selectionValue,
            int[] samples,
            int restartInterval)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (componentIndices == null)
            {
                throw new ArgumentNullException(nameof(componentIndices));
            }

            if (huffmanTables == null)
            {
                throw new ArgumentNullException(nameof(huffmanTables));
            }

            ValidateDimensions(width, height, samplePrecision);
            ValidateComponentCount(frameComponentCount);
            if (componentIndices.Length == 0 || componentIndices.Length != huffmanTables.Length)
            {
                throw CreateException("JPEG lossless scan component and Huffman table mappings do not match.");
            }

            for (var scanComponent = 0; scanComponent < componentIndices.Length; scanComponent++)
            {
                if (componentIndices[scanComponent] < 0 || componentIndices[scanComponent] >= frameComponentCount)
                {
                    throw CreateException($"JPEG lossless scan component index {componentIndices[scanComponent]} is outside the frame component range.");
                }

                if (huffmanTables[scanComponent] == null)
                {
                    throw CreateException($"JPEG lossless scan component {scanComponent} is missing its DC Huffman table.");
                }
            }

            var sampleCount = width * height * frameComponentCount;
            if (samples.Length < sampleCount)
            {
                throw CreateException($"JPEG lossless scan sample workspace {samples.Length} is smaller than expected length {sampleCount}.");
            }

            var reader = new JpegEntropyBitReader(encoded);
            var mcuCount = width * height;
            var mcuIndex = 0;
            var restartIndex = 0;
            var restartStartMcu = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var scanComponent = 0; scanComponent < componentIndices.Length; scanComponent++)
                    {
                        var component = componentIndices[scanComponent];
                        var category = huffmanTables[scanComponent].Decode(reader);
                        if (category < 0 || category > samplePrecision + 1)
                        {
                            throw CreateException($"JPEG lossless scan category {category} is outside the supported range.");
                        }

                        var difference = category == 0
                            ? 0
                            : category == 16
                                ? 1 << 15
                                : DecodeMagnitude(reader.ReadBits(category), category);
                        var prediction = PredictInterleaved(
                            samples,
                            width,
                            frameComponentCount,
                            x,
                            y,
                            component,
                            samplePrecision,
                            selectionValue,
                            restartStartMcu);
                        var sample = NormalizeSample(prediction + difference, samplePrecision);
                        ValidateSample(sample, samplePrecision);
                        samples[GetInterleavedIndex(width, frameComponentCount, x, y, component)] = sample;
                    }

                    mcuIndex++;
                    if (restartInterval > 0
                        && mcuIndex < mcuCount
                        && mcuIndex % restartInterval == 0)
                    {
                        byte marker;
                        try
                        {
                            marker = reader.ReadRestartMarker();
                        }
                        catch (DicomCodecException exception)
                        {
                            throw CreateException($"JPEG Lossless restart marker at MCU {mcuIndex} could not be read: {exception.Message}");
                        }

                        var expectedMarker = (byte)(JpegMarker.RST0 + restartIndex);
                        if (marker != expectedMarker)
                        {
                            throw CreateException(
                                $"JPEG Lossless restart marker sequence at MCU {mcuIndex} expected RST{restartIndex} but found RST{marker - JpegMarker.RST0}.");
                        }

                        restartStartMcu = mcuIndex;
                        restartIndex = (restartIndex + 1) & 7;
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

        private static int PredictInterleaved(
            int[] samples,
            int width,
            int componentCount,
            int x,
            int y,
            int component,
            int samplePrecision,
            int selectionValue,
            int restartStartMcu = 0)
        {
            var mcuIndex = y * width + x;
            var hasLeft = x > 0 && mcuIndex - 1 >= restartStartMcu;
            var hasAbove = y > 0 && mcuIndex - width >= restartStartMcu;
            if (!hasLeft && !hasAbove)
            {
                return 1 << (samplePrecision - 1);
            }

            var left = hasLeft ? samples[GetInterleavedIndex(width, componentCount, x - 1, y, component)] : 0;
            if (!hasAbove)
            {
                return left;
            }

            var above = samples[GetInterleavedIndex(width, componentCount, x, y - 1, component)];
            if (!hasLeft)
            {
                return above;
            }

            var hasUpperLeft = x > 0 && y > 0 && mcuIndex - width - 1 >= restartStartMcu;
            var upperLeft = hasUpperLeft
                ? samples[GetInterleavedIndex(width, componentCount, x - 1, y - 1, component)]
                : 0;
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
            if (samples.Length < expectedLength)
            {
                throw CreateException($"JPEG lossless scan sample count {samples.Length} is smaller than expected length {expectedLength}.");
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

            if (samplePrecision < 1 || samplePrecision > 16)
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
