using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsContextModel
    {
        private readonly JpegLsRegularContextState[] _regularContexts;
        private readonly JpegLsGradientQuantizer _quantizer;

        public JpegLsContextModel(int maximumSampleValue, int nearLossless, int resetThreshold)
        {
            Traits = JpegLsTraits.CreateDefault(maximumSampleValue, nearLossless, resetThreshold);
            _quantizer = new JpegLsGradientQuantizer(Traits);

            var initialA = Math.Max(2, (Traits.Range + 32) / 64);
            _regularContexts = new JpegLsRegularContextState[365];
            for (var i = 0; i < _regularContexts.Length; i++)
            {
                _regularContexts[i] = new JpegLsRegularContextState(initialA);
            }
        }

        public JpegLsTraits Traits { get; }

        public JpegLsRegularContextState GetRegularContext(int index)
        {
            if (index < 0 || index >= _regularContexts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "JPEG-LS regular context index is out of range.");
            }

            return _regularContexts[index];
        }

        public JpegLsRegularContextState GetContext(int q1, int q2, int q3, out int sign)
        {
            var contextId = ComputeContextId(q1, q2, q3);
            sign = BitwiseSign(contextId);
            contextId = ApplySign(contextId, sign);
            return GetRegularContext(contextId);
        }

        public JpegLsRegularContextState GetContext(int left, int above, int aboveLeft, int aboveRight, out int sign, out int predicted)
        {
            _quantizer.ComputeContext(left, above, aboveLeft, aboveRight, out var q1, out var q2, out var q3);
            predicted = JpegLsPredictor.Predict(left, above, aboveLeft);
            return GetContext(q1, q2, q3, out sign);
        }

        internal static int ComputeContextId(int q1, int q2, int q3)
        {
            return (q1 * 9 + q2) * 9 + q3;
        }

        internal static int BitwiseSign(int value)
        {
            return value < 0 ? -1 : 0;
        }

        internal static int ApplySign(int value, int sign)
        {
            return (sign ^ value) - sign;
        }
    }
}
