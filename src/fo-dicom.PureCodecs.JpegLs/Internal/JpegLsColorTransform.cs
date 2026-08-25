using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    internal static class JpegLsColorTransform
    {
        public static void Validate(byte transform, JpegLsFrameInfo frameInfo)
        {
            if (transform == 0)
            {
                return;
            }

            if (frameInfo.Components.Count != 3)
            {
                throw CreateException("JPEG-LS HP color transforms require exactly three components.");
            }

            if (frameInfo.BitsPerSample != 8 && frameInfo.BitsPerSample != 16)
            {
                throw CreateException("JPEG-LS HP color transforms require 8-bit or 16-bit sample precision.");
            }
        }

        public static void ApplyInverse(int[] samples, byte transform, int bitsPerSample)
        {
            if (transform == 0)
            {
                return;
            }

            var range = bitsPerSample == 8 ? 256 : 65536;
            var mask = range - 1;
            var halfRange = range / 2;
            for (var index = 0; index < samples.Length; index += 3)
            {
                var value1 = samples[index];
                var value2 = samples[index + 1];
                var value3 = samples[index + 2];
                switch (transform)
                {
                    case 1:
                        samples[index] = Wrap(value1 + value2 - halfRange, mask);
                        samples[index + 1] = value2;
                        samples[index + 2] = Wrap(value3 + value2 - halfRange, mask);
                        break;
                    case 2:
                        var red = Wrap(value1 + value2 - halfRange, mask);
                        samples[index] = red;
                        samples[index + 1] = value2;
                        samples[index + 2] = Wrap(value3 + ((red + value2) >> 1) - halfRange, mask);
                        break;
                    case 3:
                        var green = Wrap(value1 - ((value3 + value2) >> 2) + range / 4, mask);
                        samples[index] = Wrap(value3 + green - halfRange, mask);
                        samples[index + 1] = green;
                        samples[index + 2] = Wrap(value2 + green - halfRange, mask);
                        break;
                    default:
                        throw CreateException($"JPEG-LS color transform {transform} is not supported.");
                }
            }
        }

        private static int Wrap(int value, int mask)
        {
            return value & mask;
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
