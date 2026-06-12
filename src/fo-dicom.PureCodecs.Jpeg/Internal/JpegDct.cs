using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public static class JpegDct
    {
        public static JpegBlock8x8 Forward(JpegBlock8x8 samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var coefficients = new JpegBlock8x8();
            for (var v = 0; v < JpegBlock8x8.Size; v++)
            {
                for (var u = 0; u < JpegBlock8x8.Size; u++)
                {
                    var sum = 0.0;
                    for (var y = 0; y < JpegBlock8x8.Size; y++)
                    {
                        for (var x = 0; x < JpegBlock8x8.Size; x++)
                        {
                            sum += samples[y, x]
                                * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0)
                                * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                        }
                    }

                    coefficients[v, u] = 0.25 * Scale(u) * Scale(v) * sum;
                }
            }

            return coefficients;
        }

        public static JpegBlock8x8 Inverse(JpegBlock8x8 coefficients)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }

            var samples = new JpegBlock8x8();
            for (var y = 0; y < JpegBlock8x8.Size; y++)
            {
                for (var x = 0; x < JpegBlock8x8.Size; x++)
                {
                    var sum = 0.0;
                    for (var v = 0; v < JpegBlock8x8.Size; v++)
                    {
                        for (var u = 0; u < JpegBlock8x8.Size; u++)
                        {
                            sum += Scale(u)
                                * Scale(v)
                                * coefficients[v, u]
                                * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0)
                                * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                        }
                    }

                    samples[y, x] = 0.25 * sum;
                }
            }

            return samples;
        }

        private static double Scale(int index)
        {
            return index == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
        }
    }
}
