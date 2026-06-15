using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000QuantizationTable
    {
        private static readonly double[,] DwtNorms97 =
        {
            { 1.000, 1.965, 4.177, 8.403, 16.90, 33.84, 67.69, 135.3, 270.6, 540.9 },
            { 2.022, 3.989, 8.355, 17.04, 34.27, 68.63, 137.3, 274.6, 549.0, 0.0 },
            { 2.022, 3.989, 8.355, 17.04, 34.27, 68.63, 137.3, 274.6, 549.0, 0.0 },
            { 2.080, 3.865, 8.307, 17.18, 34.71, 69.59, 139.3, 278.6, 557.2, 0.0 }
        };

        public static double[] CreateIrreversibleSteps(int levels, int quality)
        {
            var scale = QualityScale(quality);
            var steps = new double[(3 * levels) + 1];
            for (var index = 0; index < steps.Length; index++)
            {
                var orientation = SubbandOrientation(index);
                var level = levels - SubbandResolution(index);
                if (level < 0)
                {
                    level = 0;
                }

                var norm = DwtNorm97(level, orientation);
                steps[index] = norm <= 0 ? scale : scale / norm;
            }

            return steps;
        }

        public static ushort EncodeStepSize(double stepSize, int precision)
        {
            if (stepSize <= 0)
            {
                return 0;
            }

            var fixedPoint = (int)Math.Floor(stepSize * 8192.0);
            if (fixedPoint <= 0)
            {
                fixedPoint = 1;
            }

            var log2 = FloorLog2(fixedPoint);
            var power = log2 - 13;
            var shift = 11 - log2;
            var mantissa = shift < 0 ? fixedPoint >> -shift : fixedPoint << shift;
            mantissa &= 0x7FF;
            var exponent = precision - power;
            if (exponent < 0)
            {
                exponent = 0;
            }
            else if (exponent > 0x1F)
            {
                exponent = 0x1F;
            }

            return (ushort)((exponent << 11) | mantissa);
        }

        public static double DecodeStepSize(ushort encoded, int precision)
        {
            var exponent = (encoded >> 11) & 0x1F;
            var mantissa = encoded & 0x7FF;
            return (1.0 + (mantissa / 2048.0)) * Math.Pow(2, precision - exponent);
        }

        public static int BitPlaneDepth(ushort encoded, int guardBits)
        {
            var exponent = (encoded >> 11) & 0x1F;
            return exponent + guardBits - 1;
        }

        public static int SubbandIndex(int resolution, int orientation)
        {
            if (resolution == 0)
            {
                return orientation == 0 ? 0 : -1;
            }

            if (orientation < 1 || orientation > 3)
            {
                return -1;
            }

            return 1 + ((resolution - 1) * 3) + (orientation - 1);
        }

        private static int SubbandResolution(int index)
        {
            return index == 0 ? 0 : ((index - 1) / 3) + 1;
        }

        private static int SubbandOrientation(int index)
        {
            return index == 0 ? 0 : ((index - 1) % 3) + 1;
        }

        private static double QualityScale(int quality)
        {
            if (quality < 1)
            {
                quality = 1;
            }
            else if (quality > 100)
            {
                quality = 100;
            }

            _ = quality;
            return 1.0;
        }

        private static double DwtNorm97(int level, int orientation)
        {
            if (orientation < 0 || orientation > 3)
            {
                return 1.0;
            }

            var maxLevel = orientation == 0 ? 9 : 8;
            if (level > maxLevel)
            {
                level = maxLevel;
            }

            return DwtNorms97[orientation, level];
        }

        private static int FloorLog2(int value)
        {
            var result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }

            return result;
        }
    }
}
