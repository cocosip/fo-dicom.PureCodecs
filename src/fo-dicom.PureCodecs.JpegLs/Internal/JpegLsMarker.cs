namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public static class JpegLsMarker
    {
        public const byte SOF55 = 0xF7;
        public const byte SOI = 0xD8;
        public const byte EOI = 0xD9;
        public const byte SOS = 0xDA;
        public const byte LSE = 0xF8;
        public const byte RST0 = 0xD0;
        public const byte RST7 = 0xD7;
        public const byte APP0 = 0xE0;
        public const byte APP15 = 0xEF;
        public const byte COM = 0xFE;

        public static bool HasLength(byte code)
        {
            return code != SOI
                && code != EOI
                && (code < RST0 || code > RST7);
        }

        public static bool IsMetadata(byte code)
        {
            return (code >= APP0 && code <= APP15) || code == COM;
        }
    }
}
