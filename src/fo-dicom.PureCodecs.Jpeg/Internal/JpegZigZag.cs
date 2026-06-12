using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public static class JpegZigZag
    {
        private static readonly int[] Order =
        {
            0, 1, 8, 16, 9, 2, 3, 10,
            17, 24, 32, 25, 18, 11, 4, 5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13, 6, 7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63,
        };

        public static double[] ToZigZag(JpegBlock8x8 block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var values = new double[JpegBlock8x8.CoefficientCount];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = block[Order[index]];
            }

            return values;
        }

        public static JpegBlock8x8 FromZigZag(double[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.Length != JpegBlock8x8.CoefficientCount)
            {
                throw JpegMarkerReader.CreateException("JPEG zigzag data requires 64 coefficients.");
            }

            var block = new JpegBlock8x8();
            for (var index = 0; index < values.Length; index++)
            {
                block[Order[index]] = values[index];
            }

            return block;
        }
    }
}
