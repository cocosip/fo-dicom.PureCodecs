using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000DecodedFramePayload
    {
        public Jpeg2000DecodedFramePayload(int width, int height, int bitsAllocated, int bitsStored, bool isSigned, int samplesPerPixel, byte[] frame)
        {
            Width = width;
            Height = height;
            BitsAllocated = bitsAllocated;
            BitsStored = bitsStored;
            IsSigned = isSigned;
            SamplesPerPixel = samplesPerPixel;
            Frame = frame;
        }

        public int Width { get; }

        public int Height { get; }

        public int BitsAllocated { get; }

        public int BitsStored { get; }

        public bool IsSigned { get; }

        public int SamplesPerPixel { get; }

        public byte[] Frame { get; }
    }

    internal static class Jpeg2000FrameMetadata
    {
        public static void ValidateFrameShape(DicomPixelData pixelData, byte[] frame, string codecName)
        {
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException(codecName + " codec supports only monochrome or RGB frames.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw Jpeg2000Binary.CreateException(codecName + " codec supports only 8-bit and 16-bit allocated samples.");
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * (pixelData.BitsAllocated / 8);
            if (frame == null || frame.Length != expectedLength)
            {
                throw Jpeg2000Binary.CreateException(codecName + " codec frame length does not match DICOM metadata.");
            }
        }

        public static void ValidateDecodedMetadata(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000DecodedFramePayload decoded,
            string codestreamName)
        {
            if (targetPixelData.Width != decoded.Width
                || targetPixelData.Height != decoded.Height
                || targetPixelData.BitsAllocated != decoded.BitsAllocated
                || targetPixelData.BitsStored != decoded.BitsStored
                || targetPixelData.SamplesPerPixel != decoded.SamplesPerPixel
                || siz.Components.Count != decoded.SamplesPerPixel)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream metadata conflicts with DICOM pixel metadata.");
            }
        }
    }
}
