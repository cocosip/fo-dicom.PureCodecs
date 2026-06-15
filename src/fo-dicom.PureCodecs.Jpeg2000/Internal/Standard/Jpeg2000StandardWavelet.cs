using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal static class Jpeg2000StandardWavelet
    {
        public static int[] Forward53(int[] samples, int width, int height, int levels, int x0, int y0)
        {
            if (samples == null || samples.Length != width * height)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 reversible wavelet transform dimensions are invalid.");
            }

            var data = new int[samples.Length];
            Buffer.BlockCopy(samples, 0, data, 0, samples.Length * sizeof(int));
            var currentWidth = width;
            var currentHeight = height;
            var currentX0 = x0;
            var currentY0 = y0;
            for (var level = 0; level < levels; level++)
            {
                if (currentWidth <= 1 && currentHeight <= 1)
                {
                    break;
                }

                Forward53_2D(
                    data,
                    currentWidth,
                    currentHeight,
                    width,
                    Jpeg2000StandardGeometry.IsEven(currentX0),
                    Jpeg2000StandardGeometry.IsEven(currentY0));
                currentWidth = Jpeg2000StandardGeometry.SplitLength(
                    currentWidth,
                    Jpeg2000StandardGeometry.IsEven(currentX0));
                currentHeight = Jpeg2000StandardGeometry.SplitLength(
                    currentHeight,
                    Jpeg2000StandardGeometry.IsEven(currentY0));
                currentX0 = (currentX0 + 1) >> 1;
                currentY0 = (currentY0 + 1) >> 1;
            }

            return data;
        }

        public static void Inverse53(int[] data, int width, int height, int levels, int x0, int y0)
        {
            var levelWidths = new int[levels + 1];
            var levelHeights = new int[levels + 1];
            var levelX0 = new int[levels + 1];
            var levelY0 = new int[levels + 1];
            levelWidths[0] = width;
            levelHeights[0] = height;
            levelX0[0] = x0;
            levelY0[0] = y0;

            for (var level = 1; level <= levels; level++)
            {
                levelWidths[level] = Jpeg2000StandardGeometry.SplitLength(
                    levelWidths[level - 1],
                    Jpeg2000StandardGeometry.IsEven(levelX0[level - 1]));
                levelHeights[level] = Jpeg2000StandardGeometry.SplitLength(
                    levelHeights[level - 1],
                    Jpeg2000StandardGeometry.IsEven(levelY0[level - 1]));
                levelX0[level] = (levelX0[level - 1] + 1) >> 1;
                levelY0[level] = (levelY0[level - 1] + 1) >> 1;
            }

            for (var level = levels - 1; level >= 0; level--)
            {
                Inverse53_2D(
                    data,
                    levelWidths[level],
                    levelHeights[level],
                    width,
                    Jpeg2000StandardGeometry.IsEven(levelX0[level]),
                    Jpeg2000StandardGeometry.IsEven(levelY0[level]));
            }
        }

        private static void Forward53_2D(int[] data, int width, int height, int stride, bool evenRow, bool evenColumn)
        {
            if (height > 1)
            {
                var column = new int[height];
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        column[y] = data[(y * stride) + x];
                    }

                    Forward53_1D(column, evenColumn);
                    for (var y = 0; y < height; y++)
                    {
                        data[(y * stride) + x] = column[y];
                    }
                }
            }

            if (width > 1)
            {
                var row = new int[width];
                for (var y = 0; y < height; y++)
                {
                    Array.Copy(data, y * stride, row, 0, width);
                    Forward53_1D(row, evenRow);
                    Array.Copy(row, 0, data, y * stride, width);
                }
            }
        }

        private static void Inverse53_2D(int[] data, int width, int height, int stride, bool evenRow, bool evenColumn)
        {
            if (width > 1)
            {
                var row = new int[width];
                for (var y = 0; y < height; y++)
                {
                    Array.Copy(data, y * stride, row, 0, width);
                    Inverse53_1D(row, evenRow);
                    Array.Copy(row, 0, data, y * stride, width);
                }
            }

            if (height > 1)
            {
                var column = new int[height];
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        column[y] = data[(y * stride) + x];
                    }

                    Inverse53_1D(column, evenColumn);
                    for (var y = 0; y < height; y++)
                    {
                        data[(y * stride) + x] = column[y];
                    }
                }
            }
        }

        private static void Inverse53_1D(int[] data, bool even)
        {
            var length = data.Length;
            if (even)
            {
                if (length <= 1)
                {
                    return;
                }

                var sn = (length + 1) >> 1;
                var output = new int[length];
                var s1n = data[0];
                var d1n = data[sn];
                var s0n = s1n - ((d1n + 1) >> 1);
                var i = 0;
                var j = 1;
                for (; i < length - 3; i += 2, j++)
                {
                    var d1c = d1n;
                    var s0c = s0n;
                    s1n = data[j];
                    d1n = data[sn + j];
                    s0n = s1n - ((d1c + d1n + 2) >> 2);
                    output[i] = s0c;
                    output[i + 1] = d1c + ((s0c + s0n) >> 1);
                }

                output[i] = s0n;
                if ((length & 1) != 0)
                {
                    output[length - 1] = data[(length - 1) >> 1] - ((d1n + 1) >> 1);
                    output[length - 2] = d1n + ((s0n + output[length - 1]) >> 1);
                }
                else
                {
                    output[length - 1] = d1n + s0n;
                }

                Array.Copy(output, data, length);
                return;
            }

            if (length == 1)
            {
                data[0] /= 2;
                return;
            }

            if (length == 2)
            {
                var out1 = data[0] - ((data[1] + 1) >> 1);
                var out0 = data[1] + out1;
                data[0] = out0;
                data[1] = out1;
                return;
            }

            var lowCount = length >> 1;
            var result = new int[length];
            var s1 = data[lowCount + 1];
            var dc = data[0] - ((data[lowCount] + s1 + 2) >> 2);
            result[0] = data[lowCount] + dc;
            var limit = length - 2 - ((length & 1) == 0 ? 1 : 0);
            var outputIndex = 1;
            var lowIndex = 1;
            for (; outputIndex < limit; outputIndex += 2, lowIndex++)
            {
                var s2 = data[lowCount + lowIndex + 1];
                var dn = data[lowIndex] - ((s1 + s2 + 2) >> 2);
                result[outputIndex] = dc;
                result[outputIndex + 1] = s1 + ((dn + dc) >> 1);
                dc = dn;
                s1 = s2;
            }

            result[outputIndex] = dc;
            if ((length & 1) == 0)
            {
                var dn = data[(length >> 1) - 1] - ((s1 + 1) >> 1);
                result[length - 2] = s1 + ((dn + dc) >> 1);
                result[length - 1] = dn;
            }
            else
            {
                result[length - 1] = s1 + dc;
            }

            Array.Copy(result, data, length);
        }

        private static void Forward53_1D(int[] data, bool even)
        {
            var length = data.Length;
            if (even)
            {
                if (length <= 1)
                {
                    return;
                }

                var sn = (length + 1) >> 1;
                var dn = length - sn;
                var output = new int[length];
                var i = 0;
                for (; i < sn - 1; i++)
                {
                    output[sn + i] = data[(2 * i) + 1] - ((data[2 * i] + data[2 * (i + 1)]) >> 1);
                }

                if ((length & 1) == 0)
                {
                    output[sn + i] = data[(2 * i) + 1] - data[2 * i];
                }

                output[0] = data[0] + ((output[sn] + output[sn] + 2) >> 2);
                for (i = 1; i < dn; i++)
                {
                    output[i] = data[2 * i] + ((output[sn + i - 1] + output[sn + i] + 2) >> 2);
                }

                if ((length & 1) != 0)
                {
                    output[i] = data[2 * i] + ((output[sn + i - 1] + output[sn + i - 1] + 2) >> 2);
                }

                Array.Copy(output, data, length);
                return;
            }

            if (length == 1)
            {
                data[0] *= 2;
                return;
            }

            var lowCount = length >> 1;
            var highCount = length - lowCount;
            var result = new int[length];
            result[lowCount] = data[0] - data[1];
            var index = 1;
            for (; index < lowCount; index++)
            {
                result[lowCount + index] = data[2 * index] - ((data[(2 * index) + 1] + data[(2 * (index - 1)) + 1]) >> 1);
            }

            if ((length & 1) != 0)
            {
                result[lowCount + index] = data[2 * index] - data[(2 * (index - 1)) + 1];
            }

            for (index = 0; index < highCount - 1; index++)
            {
                result[index] = data[(2 * index) + 1] + ((result[lowCount + index] + result[lowCount + index + 1] + 2) >> 2);
            }

            if ((length & 1) == 0)
            {
                result[index] = data[(2 * index) + 1] + ((result[lowCount + index] + result[lowCount + index] + 2) >> 2);
            }

            Array.Copy(result, data, length);
        }
    }
}
