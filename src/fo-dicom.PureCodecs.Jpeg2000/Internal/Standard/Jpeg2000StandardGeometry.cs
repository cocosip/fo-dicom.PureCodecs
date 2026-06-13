using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal static class Jpeg2000StandardGeometry
    {
        public static int CeilDiv(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            return value >= 0 ? (value + divisor - 1) / divisor : value / divisor;
        }

        public static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            return value >= 0 ? value / divisor : -(((-value) + divisor - 1) / divisor);
        }

        public static int FloorLog2(int value)
        {
            var result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }

            return result;
        }

        public static int CeilDivPow2(int value, int power)
        {
            return (int)(((long)value + (1L << power) - 1) >> power);
        }

        public static int FloorDivPow2(int value, int power)
        {
            return value >> power;
        }

        public static ResolutionGeometry GetResolution(int width, int height, int x0, int y0, int levels, int resolution)
        {
            var levelNo = levels - resolution;
            var resWidth = width;
            var resHeight = height;
            var resX0 = x0;
            var resY0 = y0;

            for (var i = 0; i < levelNo; i++)
            {
                resWidth = SplitLength(resWidth, IsEven(resX0));
                resHeight = SplitLength(resHeight, IsEven(resY0));
                resX0 = NextCoord(resX0);
                resY0 = NextCoord(resY0);
            }

            return new ResolutionGeometry(resWidth, resHeight, resX0, resY0);
        }

        public static IReadOnlyList<SubbandGeometry> GetSubbands(int width, int height, int x0, int y0, int levels, int resolution)
        {
            var current = GetResolution(width, height, x0, y0, levels, resolution);
            var levelNo = levels - resolution;
            if (resolution == 0)
            {
                return new[]
                {
                    new SubbandGeometry(
                        0,
                        0,
                        0,
                        current.Width,
                        current.Height,
                        CeilDivPow2(x0, levelNo),
                        CeilDivPow2(y0, levelNo),
                        CeilDivPow2(x0 + width, levelNo),
                        CeilDivPow2(y0 + height, levelNo))
                };
            }

            var previous = GetResolution(width, height, x0, y0, levels, resolution - 1);
            var highWidth = current.Width - previous.Width;
            var highHeight = current.Height - previous.Height;
            return new[]
            {
                CreateHighSubband(1, previous.Width, 0, highWidth, previous.Height, x0, y0, width, height, levelNo),
                CreateHighSubband(2, 0, previous.Height, previous.Width, highHeight, x0, y0, width, height, levelNo),
                CreateHighSubband(3, previous.Width, previous.Height, highWidth, highHeight, x0, y0, width, height, levelNo)
            };
        }

        public static int SplitLength(int length, bool even)
        {
            return even ? (length + 1) / 2 : length / 2;
        }

        public static bool IsEven(int value)
        {
            return (value & 1) == 0;
        }

        private static int NextCoord(int value)
        {
            return (value + 1) >> 1;
        }

        private static SubbandGeometry CreateHighSubband(
            int orientation,
            int offsetX,
            int offsetY,
            int width,
            int height,
            int tileX0,
            int tileY0,
            int tileWidth,
            int tileHeight,
            int levelNo)
        {
            var x0b = orientation & 1;
            var y0b = orientation >> 1;
            var bandX0 = CeilDivPow2(tileX0 - (x0b << levelNo), levelNo + 1);
            var bandY0 = CeilDivPow2(tileY0 - (y0b << levelNo), levelNo + 1);
            var bandX1 = CeilDivPow2(tileX0 + tileWidth - (x0b << levelNo), levelNo + 1);
            var bandY1 = CeilDivPow2(tileY0 + tileHeight - (y0b << levelNo), levelNo + 1);
            return new SubbandGeometry(orientation, offsetX, offsetY, width, height, bandX0, bandY0, bandX1, bandY1);
        }
    }

    internal readonly struct ResolutionGeometry
    {
        public ResolutionGeometry(int width, int height, int x0, int y0)
        {
            Width = width;
            Height = height;
            X0 = x0;
            Y0 = y0;
        }

        public int Width { get; }

        public int Height { get; }

        public int X0 { get; }

        public int Y0 { get; }
    }

    internal readonly struct SubbandGeometry
    {
        public SubbandGeometry(int orientation, int offsetX, int offsetY, int width, int height, int bandX0, int bandY0, int bandX1, int bandY1)
        {
            Orientation = orientation;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            BandX0 = bandX0;
            BandY0 = bandY0;
            BandX1 = bandX1;
            BandY1 = bandY1;
        }

        public int Orientation { get; }

        public int OffsetX { get; }

        public int OffsetY { get; }

        public int Width { get; }

        public int Height { get; }

        public int BandX0 { get; }

        public int BandY0 { get; }

        public int BandX1 { get; }

        public int BandY1 { get; }
    }
}
