namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsGradientQuantizer
    {
        private readonly JpegLsTraits _traits;

        public JpegLsGradientQuantizer(JpegLsTraits traits)
        {
            _traits = traits;
        }

        public void ComputeContext(int left, int above, int aboveLeft, int aboveRight, out int q1, out int q2, out int q3)
        {
            q1 = Quantize(aboveRight - above);
            q2 = Quantize(above - aboveLeft);
            q3 = Quantize(aboveLeft - left);
        }

        private int Quantize(int gradient)
        {
            if (gradient <= -_traits.Threshold3)
            {
                return -4;
            }

            if (gradient <= -_traits.Threshold2)
            {
                return -3;
            }

            if (gradient <= -_traits.Threshold1)
            {
                return -2;
            }

            if (gradient < -_traits.NearLossless)
            {
                return -1;
            }

            if (gradient <= _traits.NearLossless)
            {
                return 0;
            }

            if (gradient < _traits.Threshold1)
            {
                return 1;
            }

            if (gradient < _traits.Threshold2)
            {
                return 2;
            }

            return gradient < _traits.Threshold3 ? 3 : 4;
        }
    }
}
