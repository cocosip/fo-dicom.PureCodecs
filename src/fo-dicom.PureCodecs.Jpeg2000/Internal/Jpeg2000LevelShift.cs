namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000LevelShift
    {
        public static int Forward(int sample, int precision, bool isSigned)
        {
            return isSigned ? sample : sample - Offset(precision);
        }

        public static int Inverse(int shiftedSample, int precision, bool isSigned)
        {
            return isSigned ? shiftedSample : shiftedSample + Offset(precision);
        }

        private static int Offset(int precision)
        {
            if (precision < 1 || precision > 30)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 level shift precision is unsupported.");
            }

            return 1 << (precision - 1);
        }
    }
}
