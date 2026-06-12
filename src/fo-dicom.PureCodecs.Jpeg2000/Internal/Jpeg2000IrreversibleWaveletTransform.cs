using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000IrreversibleWaveletTransform
    {
        private const double Alpha = -1.586134342059924;
        private const double Beta = -0.052980118572961;
        private const double Gamma = 0.882911075530934;
        private const double Delta = 0.443506852043971;
        private const double LowScale = 1.230174104914001;
        private const double HighScale = 1.0 / LowScale;

        public static double[] Forward2D(double[] samples, int width, int height, int levels)
        {
            Validate(samples, width, height, levels);
            var data = Copy(samples);
            var currentWidth = width;
            var currentHeight = height;

            for (var level = 0; level < levels; level++)
            {
                for (var y = 0; y < currentHeight; y++)
                {
                    var row = ReadRow(data, width, y, currentWidth);
                    Forward1D(row);
                    WriteRow(data, width, y, row);
                }

                for (var x = 0; x < currentWidth; x++)
                {
                    var column = ReadColumn(data, width, x, currentHeight);
                    Forward1D(column);
                    WriteColumn(data, width, x, column);
                }

                currentWidth = (currentWidth + 1) / 2;
                currentHeight = (currentHeight + 1) / 2;
            }

            return data;
        }

        public static double[] Inverse2D(double[] coefficients, int width, int height, int levels)
        {
            Validate(coefficients, width, height, levels);
            var data = Copy(coefficients);
            var widths = new int[levels];
            var heights = new int[levels];
            var currentWidth = width;
            var currentHeight = height;
            for (var level = 0; level < levels; level++)
            {
                widths[level] = currentWidth;
                heights[level] = currentHeight;
                currentWidth = (currentWidth + 1) / 2;
                currentHeight = (currentHeight + 1) / 2;
            }

            for (var level = levels - 1; level >= 0; level--)
            {
                currentWidth = widths[level];
                currentHeight = heights[level];

                for (var x = 0; x < currentWidth; x++)
                {
                    var column = ReadColumn(data, width, x, currentHeight);
                    Inverse1D(column);
                    WriteColumn(data, width, x, column);
                }

                for (var y = 0; y < currentHeight; y++)
                {
                    var row = ReadRow(data, width, y, currentWidth);
                    Inverse1D(row);
                    WriteRow(data, width, y, row);
                }
            }

            return data;
        }

        private static void Forward1D(double[] values)
        {
            if (values.Length < 2)
            {
                return;
            }

            var lowCount = (values.Length + 1) / 2;
            var highCount = values.Length / 2;
            var low = new double[lowCount];
            var high = new double[highCount];

            for (var i = 0; i < lowCount; i++)
            {
                low[i] = values[i * 2];
            }

            for (var i = 0; i < highCount; i++)
            {
                high[i] = values[(i * 2) + 1];
            }

            LiftHigh(high, low, Alpha);
            LiftLow(low, high, Beta);
            LiftHigh(high, low, Gamma);
            LiftLow(low, high, Delta);

            for (var i = 0; i < low.Length; i++)
            {
                low[i] *= LowScale;
            }

            for (var i = 0; i < high.Length; i++)
            {
                high[i] *= HighScale;
            }

            Pack(values, low, high);
        }

        private static void Inverse1D(double[] values)
        {
            if (values.Length < 2)
            {
                return;
            }

            var lowCount = (values.Length + 1) / 2;
            var highCount = values.Length / 2;
            var low = new double[lowCount];
            var high = new double[highCount];
            Unpack(values, low, high);

            for (var i = 0; i < low.Length; i++)
            {
                low[i] /= LowScale;
            }

            for (var i = 0; i < high.Length; i++)
            {
                high[i] /= HighScale;
            }

            LiftLow(low, high, -Delta);
            LiftHigh(high, low, -Gamma);
            LiftLow(low, high, -Beta);
            LiftHigh(high, low, -Alpha);

            Interleave(values, low, high);
        }

        private static void LiftHigh(double[] high, double[] low, double coefficient)
        {
            for (var i = 0; i < high.Length; i++)
            {
                high[i] += coefficient * (GetLow(low, i) + GetLow(low, i + 1));
            }
        }

        private static void LiftLow(double[] low, double[] high, double coefficient)
        {
            for (var i = 0; i < low.Length; i++)
            {
                low[i] += coefficient * (GetHigh(high, i - 1) + GetHigh(high, i));
            }
        }

        private static double GetLow(double[] low, int index)
        {
            return low[Math.Max(0, Math.Min(index, low.Length - 1))];
        }

        private static double GetHigh(double[] high, int index)
        {
            if (high.Length == 0)
            {
                return 0;
            }

            return high[Math.Max(0, Math.Min(index, high.Length - 1))];
        }

        private static void Pack(double[] values, double[] low, double[] high)
        {
            Array.Copy(low, 0, values, 0, low.Length);
            Array.Copy(high, 0, values, low.Length, high.Length);
        }

        private static void Unpack(double[] values, double[] low, double[] high)
        {
            Array.Copy(values, 0, low, 0, low.Length);
            Array.Copy(values, low.Length, high, 0, high.Length);
        }

        private static void Interleave(double[] values, double[] low, double[] high)
        {
            for (var i = 0; i < low.Length; i++)
            {
                values[i * 2] = low[i];
            }

            for (var i = 0; i < high.Length; i++)
            {
                values[(i * 2) + 1] = high[i];
            }
        }

        private static double[] Copy(double[] values)
        {
            var copy = new double[values.Length];
            Buffer.BlockCopy(values, 0, copy, 0, values.Length * sizeof(double));
            return copy;
        }

        private static double[] ReadRow(double[] data, int stride, int y, int count)
        {
            var row = new double[count];
            Array.Copy(data, y * stride, row, 0, count);
            return row;
        }

        private static void WriteRow(double[] data, int stride, int y, double[] row)
        {
            Array.Copy(row, 0, data, y * stride, row.Length);
        }

        private static double[] ReadColumn(double[] data, int stride, int x, int count)
        {
            var column = new double[count];
            for (var y = 0; y < count; y++)
            {
                column[y] = data[(y * stride) + x];
            }

            return column;
        }

        private static void WriteColumn(double[] data, int stride, int x, double[] column)
        {
            for (var y = 0; y < column.Length; y++)
            {
                data[(y * stride) + x] = column[y];
            }
        }

        private static void Validate(double[] values, int width, int height, int levels)
        {
            if (values == null || width <= 0 || height <= 0 || values.Length != width * height || levels < 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 irreversible wavelet transform dimensions are invalid.");
            }
        }
    }
}
