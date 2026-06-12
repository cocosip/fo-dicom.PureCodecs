using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public static class JpegLsPredictor
    {
        public static int Predict(int left, int above, int aboveLeft)
        {
            if (aboveLeft >= Math.Max(left, above))
            {
                return Math.Min(left, above);
            }

            if (aboveLeft <= Math.Min(left, above))
            {
                return Math.Max(left, above);
            }

            return left + above - aboveLeft;
        }
    }
}
