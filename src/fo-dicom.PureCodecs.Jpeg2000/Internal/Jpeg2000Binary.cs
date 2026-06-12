using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000Binary
    {
        public static int ReadUInt16(byte[] bytes, int offset)
        {
            return (bytes[offset] << 8) | bytes[offset + 1];
        }

        public static uint ReadUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        public static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
