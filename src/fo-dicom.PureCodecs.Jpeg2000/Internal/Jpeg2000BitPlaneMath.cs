using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000BitPlaneMath
    {
        public static int EffectiveBitDepth(int precision, Jpeg2000SubbandKind subband, int guardBits)
        {
            if (precision < 1 || guardBits < 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 bit-plane precision and guard bits must be non-negative.");
            }

            return precision + guardBits + SubbandGainBits(subband);
        }

        public static int ZeroBitPlanes(
            IReadOnlyList<int> coefficients,
            int precision,
            Jpeg2000SubbandKind subband,
            int guardBits)
        {
            var effectiveDepth = EffectiveBitDepth(precision, subband, guardBits);
            var maxMagnitude = 0;
            for (var i = 0; i < coefficients.Count; i++)
            {
                var magnitude = coefficients[i] < 0 ? -coefficients[i] : coefficients[i];
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
            }

            return effectiveDepth - MagnitudeBitLength(maxMagnitude);
        }

        private static int SubbandGainBits(Jpeg2000SubbandKind subband)
        {
            switch (subband)
            {
                case Jpeg2000SubbandKind.HL:
                case Jpeg2000SubbandKind.LH:
                    return 1;
                case Jpeg2000SubbandKind.HH:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int MagnitudeBitLength(int value)
        {
            var bits = 0;
            while (value > 0)
            {
                bits++;
                value >>= 1;
            }

            return bits;
        }
    }
}
