using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public static class JpegLsNearLossless
    {
        public static int ClampSample(int sample, int bitsStored)
        {
            if (bitsStored <= 0 || bitsStored > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(bitsStored), "JPEG-LS bits stored is out of range.");
            }

            var maximum = (1 << bitsStored) - 1;
            if (sample < 0)
            {
                return 0;
            }

            return sample > maximum ? maximum : sample;
        }

        public static bool IsWithinTolerance(int expected, int actual, int allowedError)
        {
            if (allowedError < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(allowedError), "JPEG-LS allowed error cannot be negative.");
            }

            return Math.Abs(expected - actual) <= allowedError;
        }
    }
}
