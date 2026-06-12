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

        public byte[] Encode(int[] samples, int width, int height, int samplePrecision, int selectionValue)
        {
            ValidateShape(samples, width, height, samplePrecision);

            var writer = new JpegEntropyBitWriter();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var sample = samples[index];
                    ValidateSample(sample, samplePrecision);

                    var prediction = Predict(samples, width, x, y, samplePrecision, selectionValue);
                    var difference = sample - prediction;
                    var category = GetCategory(difference);
                    _table.Encode(writer, category);
                    if (category > 0)
                    {
                        writer.WriteBits(EncodeMagnitude(difference, category), category);
                    }
                }
            }

            return writer.ToArray();
        }

        public int[] Decode(byte[] encoded, int width, int height, int samplePrecision, int selectionValue)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            ValidateShape(new int[width * height], width, height, samplePrecision);

            var samples = new int[width * height];
            var reader = new JpegEntropyBitReader(encoded);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var category = _table.Decode(reader);
                    if (category < 0 || category > samplePrecision + 1)
                    {
                        throw CreateException($"JPEG lossless scan category {category} is outside the supported range.");
                    }

                    var difference = category == 0
                        ? 0
                        : DecodeMagnitude(reader.ReadBits(category), category);
                    var prediction = Predict(samples, width, x, y, samplePrecision, selectionValue);
                    var sample = prediction + difference;
                    ValidateSample(sample, samplePrecision);
                    samples[y * width + x] = sample;
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

        private static void ValidateShape(int[] samples, int width, int height, int samplePrecision)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

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

            if (samples.Length != width * height)
            {
                throw CreateException($"JPEG lossless scan sample count {samples.Length} does not match dimensions {width}x{height}.");
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
