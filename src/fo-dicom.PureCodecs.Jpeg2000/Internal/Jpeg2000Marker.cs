namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000Marker
    {
        public const byte SOC = 0x4F;
        public const byte SIZ = 0x51;
        public const byte COD = 0x52;
        public const byte COC = 0x53;
        public const byte RGN = 0x5E;
        public const byte QCD = 0x5C;
        public const byte QCC = 0x5D;
        public const byte POC = 0x5F;
        public const byte CAP = 0x50;
        public const byte PPM = 0x60;
        public const byte PPT = 0x61;
        public const byte PLT = 0x58;
        public const byte CPF = 0x59;
        public const byte COM = 0x64;
        public const byte SOT = 0x90;
        public const byte SOP = 0x91;
        public const byte EPH = 0x92;
        public const byte SOD = 0x93;
        public const byte EOC = 0xD9;

        public static bool HasLength(byte code)
        {
            return code != SOC
                && code != SOD
                && code != EOC
                && code != EPH;
        }
    }
}
