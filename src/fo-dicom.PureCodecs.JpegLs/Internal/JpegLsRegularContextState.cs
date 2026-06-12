using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsRegularContextState
    {
        internal JpegLsRegularContextState(int initialA)
        {
            A = initialA;
            N = 1;
        }

        public int A { get; private set; }

        public int B { get; private set; }

        public int C { get; private set; }

        public int N { get; private set; }

        public int GetGolombParameter()
        {
            var parameter = 0;
            while ((N << parameter) < A && parameter < 16)
            {
                parameter++;
            }

            return parameter;
        }

        public int GetErrorCorrection(int parameter, int nearLossless)
        {
            if (parameter != 0 || nearLossless != 0)
            {
                return 0;
            }

            return 2 * B + N - 1 < 0 ? -1 : 0;
        }

        public void Update(int errorValue, int nearLossless)
        {
            A += Math.Abs(errorValue);
            B += errorValue * (2 * nearLossless + 1);

            if (N == 64)
            {
                A >>= 1;
                B >>= 1;
                N >>= 1;
            }

            N++;

            if (B + N <= 0)
            {
                B += N;
                if (B <= -N)
                {
                    B = -N + 1;
                }

                if (C > -128)
                {
                    C--;
                }
            }
            else if (B > 0)
            {
                B -= N;
                if (B > 0)
                {
                    B = 0;
                }

                if (C < 127)
                {
                    C++;
                }
            }
        }
    }
}
