using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal static class Jpeg2000StandardIrreversibleWavelet
    {
        private const float Alpha97 = -1.586134342f;
        private const float Beta97 = -0.052980118f;
        private const float Gamma97 = 0.882911075f;
        private const float Delta97 = 0.443506852f;
        private const float K97 = 1.230174105f;
        private const float InvK97 = (float)(1.0 / 1.230174105);
        private const double Alpha97Inverse = -1.586134342;
        private const double Beta97Inverse = -0.052980118;
        private const double Gamma97Inverse = 0.882911075;
        private const double Delta97Inverse = 0.443506852;
        private const double InvK97Inverse = 0.812893066;
        private const double OpenJpegTwoInvK97 = 1.625732422;

        public static void Forward97(double[] data, int width, int height, int levels)
        {
            var single = new float[data.Length];
            for (var i = 0; i < data.Length; i++)
            {
                single[i] = (float)data[i];
            }

            var currentWidth = width;
            var currentHeight = height;
            for (var level = 0; level < levels; level++)
            {
                if (currentWidth <= 1 && currentHeight <= 1)
                {
                    break;
                }

                Forward97_2D(single, currentWidth, currentHeight, width);
                currentWidth = (currentWidth + 1) >> 1;
                currentHeight = (currentHeight + 1) >> 1;
            }

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = single[i];
            }
        }

        public static void Inverse97(double[] data, int width, int height, int levels)
        {
            var widths = new int[levels + 1];
            var heights = new int[levels + 1];
            widths[0] = width;
            heights[0] = height;
            for (var i = 1; i <= levels; i++)
            {
                widths[i] = (widths[i - 1] + 1) >> 1;
                heights[i] = (heights[i - 1] + 1) >> 1;
            }

            for (var level = levels - 1; level >= 0; level--)
            {
                Inverse97_2D(data, widths[level], heights[level], width);
            }
        }

        private static void Forward97_2D(float[] data, int width, int height, int stride)
        {
            if (height > 1)
            {
                var column = new float[height];
                var columnWorkspace = new float[height];
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        column[y] = data[(y * stride) + x];
                    }

                    Forward97_1DWithWorkspace(column, columnWorkspace);
                    for (var y = 0; y < height; y++)
                    {
                        data[(y * stride) + x] = column[y];
                    }
                }
            }

            if (width > 1)
            {
                var row = new float[width];
                var rowWorkspace = new float[width];
                for (var y = 0; y < height; y++)
                {
                    Array.Copy(data, y * stride, row, 0, width);
                    Forward97_1DWithWorkspace(row, rowWorkspace);
                    Array.Copy(row, 0, data, y * stride, width);
                }
            }
        }

        private static void Forward97_1D(float[] data)
        {
            Forward97_1DWithWorkspace(data, new float[data.Length]);
        }

        private static void Forward97_1DWithWorkspace(float[] data, float[] workspace)
        {
            var width = data.Length;
            if (width <= 1)
            {
                return;
            }

            var lowCount = (width + 1) >> 1;
            var highCount = width - lowCount;
            EncodeStep97(data, 0, 2, highCount, Math.Min(highCount, lowCount - 1), Alpha97);
            EncodeStep97(data, 1, 1, lowCount, Math.Min(lowCount, highCount), Beta97);
            EncodeStep97(data, 0, 2, highCount, Math.Min(highCount, lowCount - 1), Gamma97);
            EncodeStep97(data, 1, 1, lowCount, Math.Min(lowCount, highCount), Delta97);

            for (var i = 0; i < Math.Min(lowCount, highCount); i++)
            {
                data[2 * i] *= InvK97;
                data[(2 * i) + 1] *= K97;
            }

            if (lowCount > highCount)
            {
                data[(lowCount - 1) * 2] *= InvK97;
            }

            for (var i = 0; i < lowCount; i++)
            {
                workspace[i] = data[2 * i];
            }

            for (var i = 0; i < highCount; i++)
            {
                workspace[lowCount + i] = data[(2 * i) + 1];
            }

            Array.Copy(workspace, data, width);
        }

        private static void Inverse97_2D(double[] data, int width, int height, int stride)
        {
            if (width > 1)
            {
                var row = new double[width];
                for (var y = 0; y < height; y++)
                {
                    Array.Copy(data, y * stride, row, 0, width);
                    Inverse97_1D(row);
                    Array.Copy(row, 0, data, y * stride, width);
                }
            }

            if (height > 1)
            {
                var column = new double[height];
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        column[y] = data[(y * stride) + x];
                    }

                    Inverse97_1D(column);
                    for (var y = 0; y < height; y++)
                    {
                        data[(y * stride) + x] = column[y];
                    }
                }
            }
        }

        private static void Inverse97_1D(double[] data)
        {
            var width = data.Length;
            if (width <= 1)
            {
                return;
            }

            var lowCount = (width + 1) >> 1;
            var highCount = width - lowCount;
            var temp = new double[width];
            for (var i = 0; i < lowCount; i++)
            {
                temp[2 * i] = data[i];
            }

            for (var i = 0; i < highCount; i++)
            {
                temp[(2 * i) + 1] = data[lowCount + i];
            }

            Array.Copy(temp, data, width);
            for (var i = 0; i < Math.Min(lowCount, highCount); i++)
            {
                data[2 * i] /= InvK97Inverse;
                data[(2 * i) + 1] *= OpenJpegTwoInvK97;
            }

            if (lowCount > highCount)
            {
                data[(lowCount - 1) * 2] /= InvK97Inverse;
            }

            EncodeStep97Inverse(data, 1, 1, lowCount, Math.Min(lowCount, highCount), -Delta97Inverse);
            EncodeStep97Inverse(data, 0, 2, highCount, Math.Min(highCount, lowCount - 1), -Gamma97Inverse);
            EncodeStep97Inverse(data, 1, 1, lowCount, Math.Min(lowCount, highCount), -Beta97Inverse);
            EncodeStep97Inverse(data, 0, 2, highCount, Math.Min(highCount, lowCount - 1), -Alpha97Inverse);
        }

        private static void EncodeStep97(float[] data, int flStart, int fwStart, int end, int middle, float coefficient)
        {
            if (middle > 0)
            {
                var fw = fwStart;
                var fl = flStart;
                data[fw - 1] += (data[fl] + data[fw]) * coefficient;
                fw += 2;
                for (var i = 1; i < middle; i++)
                {
                    data[fw - 1] += (data[fw - 2] + data[fw]) * coefficient;
                    fw += 2;
                }
            }

            if (middle < end)
            {
                var fw = fwStart + (2 * middle);
                data[fw - 1] += (2 * data[fw - 2]) * coefficient;
            }
        }

        private static void EncodeStep97Inverse(double[] data, int flStart, int fwStart, int end, int middle, double coefficient)
        {
            if (middle > 0)
            {
                var fw = fwStart;
                var fl = flStart;
                data[fw - 1] += (data[fl] + data[fw]) * coefficient;
                fw += 2;
                for (var i = 1; i < middle; i++)
                {
                    data[fw - 1] += (data[fw - 2] + data[fw]) * coefficient;
                    fw += 2;
                }
            }

            if (middle < end)
            {
                var fw = fwStart + (2 * middle);
                data[fw - 1] += (2 * data[fw - 2]) * coefficient;
            }
        }
    }
}
