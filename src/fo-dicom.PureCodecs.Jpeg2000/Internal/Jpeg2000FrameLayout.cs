using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000FrameLayout
    {
        public static byte[] PlanarToInterleaved(byte[] planar, int pixelCount, int bytesPerSample)
        {
            return Reorder(planar, pixelCount, bytesPerSample, planarToInterleaved: true);
        }

        public static byte[] InterleavedToPlanar(byte[] interleaved, int pixelCount, int bytesPerSample)
        {
            return Reorder(interleaved, pixelCount, bytesPerSample, planarToInterleaved: false);
        }

        private static byte[] Reorder(byte[] source, int pixelCount, int bytesPerSample, bool planarToInterleaved)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (bytesPerSample != 1 && bytesPerSample != 2)
            {
                throw new DicomCodecException("JPEG 2000 planar conversion supports only 8-bit and 16-bit allocated samples.");
            }

            var expectedLength = checked(pixelCount * 3 * bytesPerSample);
            if (source.Length != expectedLength)
            {
                throw new DicomCodecException("JPEG 2000 planar RGB frame length does not match pixel metadata.");
            }

            var result = new byte[source.Length];
            for (var component = 0; component < 3; component++)
            {
                for (var pixel = 0; pixel < pixelCount; pixel++)
                {
                    var planarOffset = (component * pixelCount + pixel) * bytesPerSample;
                    var interleavedOffset = (pixel * 3 + component) * bytesPerSample;
                    var sourceOffset = planarToInterleaved ? planarOffset : interleavedOffset;
                    var targetOffset = planarToInterleaved ? interleavedOffset : planarOffset;
                    Buffer.BlockCopy(source, sourceOffset, result, targetOffset, bytesPerSample);
                }
            }

            return result;
        }
    }
}
