using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000ReversibleWaveletTransform
    {
        public static int[] Forward2D(int[] samples, int width, int height, int levels)
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

        public static int[] Inverse2D(int[] coefficients, int width, int height, int levels)
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

        private static void Forward1D(int[] values)
        {
            if (values.Length < 2)
            {
                return;
            }

            var lowCount = (values.Length + 1) / 2;
            var highCount = values.Length / 2;
            var low = new int[lowCount];
            var high = new int[highCount];

            for (var i = 0; i < lowCount; i++)
            {
                low[i] = values[i * 2];
            }

            for (var i = 0; i < highCount; i++)
            {
                high[i] = values[(i * 2) + 1];
            }

            for (var i = 0; i < highCount; i++)
            {
                high[i] -= FloorDiv(GetLow(low, i) + GetLow(low, i + 1), 2);
            }

            for (var i = 0; i < lowCount; i++)
            {
                low[i] += FloorDiv(GetHigh(high, i - 1) + GetHigh(high, i) + 2, 4);
            }

            Pack(values, low, high);
        }

        private static void Inverse1D(int[] values)
        {
            if (values.Length < 2)
            {
                return;
            }

            var lowCount = (values.Length + 1) / 2;
            var highCount = values.Length / 2;
            var low = new int[lowCount];
            var high = new int[highCount];
            Unpack(values, low, high);

            for (var i = 0; i < lowCount; i++)
            {
                low[i] -= FloorDiv(GetHigh(high, i - 1) + GetHigh(high, i) + 2, 4);
            }

            for (var i = 0; i < highCount; i++)
            {
                high[i] += FloorDiv(GetLow(low, i) + GetLow(low, i + 1), 2);
            }

            Interleave(values, low, high);
        }

        private static int GetLow(int[] low, int index)
        {
            return low[Math.Max(0, Math.Min(index, low.Length - 1))];
        }

        private static int GetHigh(int[] high, int index)
        {
            if (high.Length == 0)
            {
                return 0;
            }

            return high[Math.Max(0, Math.Min(index, high.Length - 1))];
        }

        private static int FloorDiv(int value, int divisor)
        {
            return (int)Math.Floor(value / (double)divisor);
        }

        private static int[] Copy(int[] values)
        {
            var copy = new int[values.Length];
            Buffer.BlockCopy(values, 0, copy, 0, values.Length * sizeof(int));
            return copy;
        }

        private static int[] ReadRow(int[] data, int stride, int y, int count)
        {
            var row = new int[count];
            Array.Copy(data, y * stride, row, 0, count);
            return row;
        }

        private static void WriteRow(int[] data, int stride, int y, int[] row)
        {
            Array.Copy(row, 0, data, y * stride, row.Length);
        }

        private static int[] ReadColumn(int[] data, int stride, int x, int count)
        {
            var column = new int[count];
            for (var y = 0; y < count; y++)
            {
                column[y] = data[(y * stride) + x];
            }

            return column;
        }

        private static void WriteColumn(int[] data, int stride, int x, int[] column)
        {
            for (var y = 0; y < column.Length; y++)
            {
                data[(y * stride) + x] = column[y];
            }
        }

        private static void Pack(int[] values, int[] low, int[] high)
        {
            Array.Copy(low, 0, values, 0, low.Length);
            Array.Copy(high, 0, values, low.Length, high.Length);
        }

        private static void Unpack(int[] values, int[] low, int[] high)
        {
            Array.Copy(values, 0, low, 0, low.Length);
            Array.Copy(values, low.Length, high, 0, high.Length);
        }

        private static void Interleave(int[] values, int[] low, int[] high)
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

        private static void Validate(int[] values, int width, int height, int levels)
        {
            if (values == null || width <= 0 || height <= 0 || values.Length != width * height || levels < 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 reversible wavelet transform dimensions are invalid.");
            }
        }
    }
}
