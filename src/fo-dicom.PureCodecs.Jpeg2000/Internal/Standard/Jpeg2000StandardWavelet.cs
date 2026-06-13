using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal static class Jpeg2000StandardWavelet
    {
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
            var sn = even ? (length + 1) >> 1 : length >> 1;
            var dn = length - sn;
            var interleaved = new int[length];

            if (even)
            {
                for (var i = 0; i < sn; i++)
                {
                    interleaved[i * 2] = data[i];
                }

                for (var i = 0; i < dn; i++)
                {
                    interleaved[(i * 2) + 1] = data[sn + i];
                }

                if (dn > 0 || sn > 1)
                {
                    for (var i = 0; i < sn; i++)
                    {
                        interleaved[i * 2] -= (GetHigh(interleaved, dn, i - 1) + GetHigh(interleaved, dn, i) + 2) >> 2;
                    }

                    for (var i = 0; i < dn; i++)
                    {
                        interleaved[(i * 2) + 1] += (GetLow(interleaved, sn, i) + GetLow(interleaved, sn, i + 1)) >> 1;
                    }
                }
            }
            else
            {
                for (var i = 0; i < sn; i++)
                {
                    interleaved[(i * 2) + 1] = data[i];
                }

                for (var i = 0; i < dn; i++)
                {
                    interleaved[i * 2] = data[sn + i];
                }

                if (sn == 0 && dn == 1)
                {
                    interleaved[0] /= 2;
                }
                else
                {
                    for (var i = 0; i < sn; i++)
                    {
                        interleaved[(i * 2) + 1] -= (GetLowForOdd(interleaved, dn, i) + GetLowForOdd(interleaved, dn, i + 1) + 2) >> 2;
                    }

                    for (var i = 0; i < dn; i++)
                    {
                        interleaved[i * 2] += (GetHighForOdd(interleaved, sn, i) + GetHighForOdd(interleaved, sn, i - 1)) >> 1;
                    }
                }
            }

            Array.Copy(interleaved, data, length);
        }

        private static int GetLow(int[] data, int count, int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index >= count)
            {
                index = count - 1;
            }

            return data[index * 2];
        }

        private static int GetHigh(int[] data, int count, int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index >= count)
            {
                index = count - 1;
            }

            return data[(index * 2) + 1];
        }

        private static int GetLowForOdd(int[] data, int lowCount, int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index >= lowCount)
            {
                index = lowCount - 1;
            }

            return data[index * 2];
        }

        private static int GetHighForOdd(int[] data, int highCount, int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index >= highCount)
            {
                index = highCount - 1;
            }

            return data[(index * 2) + 1];
        }
    }
}
