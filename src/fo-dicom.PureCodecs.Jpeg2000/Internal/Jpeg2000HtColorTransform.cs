using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000HtColorTransform
    {
        private const float AlphaR = 0.299f;
        private const float AlphaG = 0.587f;
        private const float AlphaB = 0.114f;
        private static readonly float BetaCb = (float)(0.5 / (1.0 - (double)AlphaB));
        private static readonly float BetaCr = (float)(0.5 / (1.0 - (double)AlphaR));

        public static double[][] CreateNormalizedIrreversibleComponents(
            int[][] components,
            int precision,
            bool isSigned,
            bool applyColorTransform)
        {
            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            var multiplier = (float)(1.0 / (double)(1UL << precision));
            var half = 1 << (precision - 1);
            var values = new double[components.Length][];
            for (var component = 0; component < components.Length; component++)
            {
                values[component] = new double[components[component].Length];
                for (var i = 0; i < components[component].Length; i++)
                {
                    var sample = isSigned
                        ? components[component][i]
                        : components[component][i] - half;
                    values[component][i] = (float)sample * multiplier;
                }
            }

            if (!applyColorTransform)
            {
                return values;
            }

            if (values.Length != 3)
            {
                throw new ArgumentException("HTJ2K irreversible color transform requires exactly three components.", nameof(components));
            }

            for (var i = 0; i < values[0].Length; i++)
            {
                var r = (float)values[0][i];
                var g = (float)values[1][i];
                var b = (float)values[2][i];
                var y = (AlphaR * r) + (AlphaG * g) + (AlphaB * b);
                values[0][i] = y;
                values[1][i] = BetaCb * (b - y);
                values[2][i] = BetaCr * (r - y);
            }

            return values;
        }
    }
}
