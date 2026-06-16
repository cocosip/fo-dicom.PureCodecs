using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000ReversibleBiboGains
    {
        private static readonly double[] Low =
        {
            1.0000, 1.5000, 1.6250, 1.6875, 1.6963, 1.7067, 1.7116,
            1.7129, 1.7141, 1.7145, 1.7151, 1.7152, 1.7155, 1.7155,
            1.7156, 1.7156, 1.7156, 1.7156, 1.7156, 1.7156, 1.7156,
            1.7156, 1.7156, 1.7156, 1.7156, 1.7156, 1.7156, 1.7156,
            1.7156, 1.7156, 1.7156, 1.7156, 1.7156, 1.7156
        };

        private static readonly double[] High =
        {
            2.0000, 2.5000, 2.7500, 2.8047, 2.8198, 2.8410, 2.8558,
            2.8601, 2.8628, 2.8656, 2.8662, 2.8667, 2.8669, 2.8670,
            2.8671, 2.8671, 2.8671, 2.8671, 2.8671, 2.8671, 2.8671,
            2.8671, 2.8671, 2.8671, 2.8671, 2.8671, 2.8671, 2.8671,
            2.8671, 2.8671, 2.8671, 2.8671, 2.8671, 2.8671
        };

        public static int[] CreateReversibleExponentBits(int bitDepth, bool usesMultipleComponentTransform, int decompositionLevels)
        {
            var baseBits = bitDepth + (usesMultipleComponentTransform ? 1 : 0);
            var result = new int[(decompositionLevels * 3) + 1];
            var index = 0;
            result[index++] = baseBits + GainBits(LowGain(decompositionLevels) * LowGain(decompositionLevels));
            for (var level = decompositionLevels; level > 0; level--)
            {
                var low = LowGain(level);
                var high = HighGain(level - 1);
                result[index++] = baseBits + GainBits(high * low);
                result[index++] = baseBits + GainBits(high * low);
                result[index++] = baseBits + GainBits(high * high);
            }

            return result;
        }

        public static int BandKmax(int bitDepth, bool usesMultipleComponentTransform, int decompositionLevels, int resolution, int orientation)
        {
            var exponents = CreateReversibleExponentBits(bitDepth, usesMultipleComponentTransform, decompositionLevels);
            var index = Jpeg2000QuantizationTable.SubbandIndex(resolution, orientation);
            if (index < 0 || index >= exponents.Length)
            {
                index = Math.Max(0, Math.Min(index, exponents.Length - 1));
            }

            var max = 0;
            for (var i = 0; i < exponents.Length; i++)
            {
                if (exponents[i] > max)
                {
                    max = exponents[i];
                }
            }

            var guardBits = Math.Max(1, max - 31);
            return (exponents[index] - guardBits - 1) + guardBits;
        }

        private static int GainBits(double gain)
        {
            return (int)Math.Ceiling(Math.Log(gain, 2.0));
        }

        private static double LowGain(int level)
        {
            return Low[Math.Min(Math.Max(level, 0), Low.Length - 1)];
        }

        private static double HighGain(int level)
        {
            return High[Math.Min(Math.Max(level, 0), High.Length - 1)];
        }
    }
}
