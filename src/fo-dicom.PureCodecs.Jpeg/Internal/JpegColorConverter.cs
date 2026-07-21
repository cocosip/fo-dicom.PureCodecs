using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public static class JpegColorConverter
    {
        private const int RgbYScale = 19595;
        private const int GbgYScale = 38470;
        private const int BgbYScale = 7471;
        private const int RgbCbScale = 11059;
        private const int GbgCbScale = 21709;
        private const int BgbCbScale = 32768;
        private const int RgbCrScale = 32768;
        private const int GbgCrScale = 27439;
        private const int BgbCrScale = 5329;
        private const int ColorScaleBits = 16;
        private const int ColorHalf = 1 << (ColorScaleBits - 1);
        private const int CbCrOffset = 128 << ColorScaleBits;

        public static byte[] RgbToYbrFull(byte[] rgb)
        {
            if (rgb == null)
            {
                throw new ArgumentNullException(nameof(rgb));
            }

            if (rgb.Length % 3 != 0)
            {
                throw JpegMarkerReader.CreateException("JPEG RGB frame length must be divisible by 3.");
            }

            var ybr = new byte[rgb.Length];
            for (var index = 0; index < rgb.Length; index += 3)
            {
                var red = rgb[index];
                var green = rgb[index + 1];
                var blue = rgb[index + 2];
                ybr[index] = (byte)((RgbYScale * red + GbgYScale * green + BgbYScale * blue + ColorHalf) >> ColorScaleBits);
                ybr[index + 1] = (byte)((-RgbCbScale * red - GbgCbScale * green + BgbCbScale * blue + CbCrOffset + ColorHalf - 1) >> ColorScaleBits);
                ybr[index + 2] = (byte)((RgbCrScale * red - GbgCrScale * green - BgbCrScale * blue + CbCrOffset + ColorHalf - 1) >> ColorScaleBits);
            }

            return ybr;
        }

        public static byte[] YbrFullToRgb(byte[] ybr)
        {
            if (ybr == null)
            {
                throw new ArgumentNullException(nameof(ybr));
            }

            if (ybr.Length % 3 != 0)
            {
                throw JpegMarkerReader.CreateException("JPEG YBR_FULL frame length must be divisible by 3.");
            }

            var rgb = new byte[ybr.Length];
            for (var index = 0; index < ybr.Length; index += 3)
            {
                WriteRgb(ybr[index], ybr[index + 1], ybr[index + 2], rgb, index);
            }

            return rgb;
        }

        public static byte[] YbrFull422ToRgb(byte[] ybr)
        {
            if (ybr == null)
            {
                throw new ArgumentNullException(nameof(ybr));
            }

            if (ybr.Length % 4 != 0)
            {
                throw JpegMarkerReader.CreateException("JPEG YBR_FULL_422 frame length must be divisible by 4.");
            }

            var rgb = new byte[(ybr.Length / 4) * 6];
            var output = 0;
            for (var index = 0; index < ybr.Length; index += 4)
            {
                var y1 = ybr[index];
                var y2 = ybr[index + 1];
                var cb = ybr[index + 2];
                var cr = ybr[index + 3];
                WriteRgb(y1, cb, cr, rgb, output);
                WriteRgb(y2, cb, cr, rgb, output + 3);
                output += 6;
            }

            return rgb;
        }

        public static byte[] PlanarRgbToInterleaved(byte[] planar, int pixelCount)
        {
            if (planar == null)
            {
                throw new ArgumentNullException(nameof(planar));
            }

            if (pixelCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelCount));
            }

            if (planar.Length != pixelCount * 3)
            {
                throw JpegMarkerReader.CreateException("JPEG planar RGB frame length does not match pixel count.");
            }

            var interleaved = new byte[planar.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                interleaved[pixel * 3] = planar[pixel];
                interleaved[pixel * 3 + 1] = planar[pixelCount + pixel];
                interleaved[pixel * 3 + 2] = planar[pixelCount * 2 + pixel];
            }

            return interleaved;
        }

        public static byte[] InterleavedRgbToPlanar(byte[] interleaved, int pixelCount)
        {
            if (interleaved == null)
            {
                throw new ArgumentNullException(nameof(interleaved));
            }

            if (interleaved.Length != pixelCount * 3)
            {
                throw JpegMarkerReader.CreateException("JPEG interleaved RGB frame length does not match pixel count.");
            }

            var planar = new byte[interleaved.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                planar[pixel] = interleaved[pixel * 3];
                planar[pixelCount + pixel] = interleaved[pixel * 3 + 1];
                planar[pixelCount * 2 + pixel] = interleaved[pixel * 3 + 2];
            }

            return planar;
        }

        private static void WriteRgb(byte y, byte cb, byte cr, byte[] rgb, int offset)
        {
            var cB = cb - 128;
            var cR = cr - 128;
            rgb[offset] = Clamp(y + 1.402 * cR);
            rgb[offset + 1] = Clamp(y - 0.344136 * cB - 0.714136 * cR);
            rgb[offset + 2] = Clamp(y + 1.772 * cB);
        }

        private static byte Clamp(double value)
        {
            var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 0)
            {
                return 0;
            }

            if (rounded > 255)
            {
                return 255;
            }

            return (byte)rounded;
        }
    }
}
