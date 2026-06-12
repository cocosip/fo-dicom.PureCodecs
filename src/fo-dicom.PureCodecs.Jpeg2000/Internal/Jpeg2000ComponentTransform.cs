using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000ComponentTransform
    {
        public static Jpeg2000IntRgb ForwardReversible(Jpeg2000IntRgb rgb, bool allowMultipleComponentTransform)
        {
            if (!allowMultipleComponentTransform)
            {
                return rgb;
            }

            var y = FloorDiv(rgb.R + (2 * rgb.G) + rgb.B, 4);
            return new Jpeg2000IntRgb(y, rgb.B - rgb.G, rgb.R - rgb.G);
        }

        public static Jpeg2000IntRgb InverseReversible(Jpeg2000IntRgb yDbDr, bool allowMultipleComponentTransform)
        {
            if (!allowMultipleComponentTransform)
            {
                return yDbDr;
            }

            var g = yDbDr.R - FloorDiv(yDbDr.G + yDbDr.B, 4);
            return new Jpeg2000IntRgb(yDbDr.B + g, g, yDbDr.G + g);
        }

        public static Jpeg2000FloatRgb ForwardIrreversible(Jpeg2000FloatRgb rgb, bool allowMultipleComponentTransform)
        {
            if (!allowMultipleComponentTransform)
            {
                return rgb;
            }

            var y = (0.299 * rgb.R) + (0.587 * rgb.G) + (0.114 * rgb.B);
            var cb = (-0.168736 * rgb.R) - (0.331264 * rgb.G) + (0.5 * rgb.B);
            var cr = (0.5 * rgb.R) - (0.418688 * rgb.G) - (0.081312 * rgb.B);
            return new Jpeg2000FloatRgb(y, cb, cr);
        }

        public static Jpeg2000FloatRgb InverseIrreversible(Jpeg2000FloatRgb yCbCr, bool allowMultipleComponentTransform)
        {
            if (!allowMultipleComponentTransform)
            {
                return yCbCr;
            }

            var r = yCbCr.R + (1.402 * yCbCr.B);
            var g = yCbCr.R - (0.344136286201022 * yCbCr.G) - (0.714136286201022 * yCbCr.B);
            var b = yCbCr.R + (1.772 * yCbCr.G);
            return new Jpeg2000FloatRgb(r, g, b);
        }

        private static int FloorDiv(int value, int divisor)
        {
            return (int)Math.Floor(value / (double)divisor);
        }
    }
}
