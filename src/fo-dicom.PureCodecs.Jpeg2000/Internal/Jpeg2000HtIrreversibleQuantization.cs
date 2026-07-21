using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000HtIrreversibleQuantization
    {
        public static ushort[] CreateOpenJphScalarExpoundedSteps(int decompositionLevels, int bitDepth, float baseDelta)
        {
            if (baseDelta <= 0)
            {
                baseDelta = 1.0f / (1 << bitDepth);
            }

            var steps = new ushort[(3 * decompositionLevels) + 1];
            var index = 0;
            var gainL = GetGainL(decompositionLevels);
            steps[index++] = EncodeOpenJphStep(baseDelta / (gainL * gainL));
            for (var d = decompositionLevels; d > 0; d--)
            {
                gainL = GetGainL(d);
                var gainH = GetGainH(d - 1);
                var lhHLLH = EncodeOpenJphStep(baseDelta / (gainL * gainH));
                steps[index++] = lhHLLH;
                steps[index++] = lhHLLH;
                steps[index++] = EncodeOpenJphStep(baseDelta / (gainH * gainH));
            }

            return steps;
        }

        public static ushort[] CreateQualityScalarExpoundedSteps(int decompositionLevels, int bitDepth, int quality)
        {
            var scale = QualityScale(quality);
            var steps = new ushort[(3 * decompositionLevels) + 1];
            for (var index = 0; index < steps.Length; index++)
            {
                var resolution = index == 0 ? 0 : ((index - 1) / 3) + 1;
                var orientation = index == 0 ? 0 : ((index - 1) % 3) + 1;
                var level = decompositionLevels - resolution;
                if (level < 0)
                {
                    level = 0;
                }

                var norm = DwtNorm97(level, orientation);
                var step = norm <= 0 ? scale : scale / norm;
                steps[index] = Jpeg2000QuantizationTable.EncodeStepSize(step, bitDepth);
            }

            return steps;
        }

        public static int Kmax(ushort encodedStep, int guardBits)
        {
            return ((encodedStep >> 11) & 0x1F) - 1 + guardBits;
        }

        public static double DecodeDelta(ushort encodedStep, int orientation, int kMax)
        {
            var exponent = (encodedStep >> 11) & 0x1F;
            var mantissa = (double)((encodedStep & 0x7FF) | 0x800) * OrientationGain(orientation);
            mantissa /= 1 << 11;
            mantissa /= 1u << exponent;
            return mantissa / Math.Pow(2, 31 - kMax);
        }

        public static double[] FromSignMagnitude(IReadOnlyList<int> coefficients, double delta)
        {
            var samples = new double[coefficients.Count];
            for (var i = 0; i < coefficients.Count; i++)
            {
                var value = unchecked((uint)coefficients[i]);
                var magnitude = (value & 0x7FFFFFFFu) * delta;
                samples[i] = (value & 0x80000000u) != 0 ? -magnitude : magnitude;
            }

            return samples;
        }

        public static int[] ToSignMagnitude(double[] coefficients, double delta)
        {
            var scaled = new int[coefficients.Length];
            var deltaInv = delta > 0 ? 1.0 / delta : 1.0;
            for (var i = 0; i < coefficients.Length; i++)
            {
                var t = TruncateToInt32((float)(coefficients[i] * deltaInv));
                var sign = t < 0 ? 0x80000000u : 0u;
                var magnitude = (uint)(t < 0 ? -t : t);
                scaled[i] = unchecked((int)(sign | magnitude));
            }

            return scaled;
        }

        private static ushort EncodeOpenJphStep(float delta)
        {
            var exponent = 0;
            while (delta < 1.0f)
            {
                exponent++;
                delta *= 2.0f;
            }

            if (exponent > 29)
            {
                exponent = 29;
                delta = 1.0f;
            }

            var mantissa = (int)Math.Round(delta * (1 << 11)) - (1 << 11);
            if (mantissa >= 1 << 11)
            {
                mantissa = 0x7FF;
            }
            else if (mantissa < 0)
            {
                mantissa = 0;
            }

            return (ushort)((exponent << 11) | mantissa);
        }

        private static int TruncateToInt32(float value)
        {
            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value <= int.MinValue)
            {
                return int.MinValue;
            }

            return value >= 0 ? (int)Math.Floor(value) : (int)Math.Ceiling(value);
        }

        private static double OrientationGain(int orientation)
        {
            return orientation == 3 ? 4.0 : orientation == 0 ? 1.0 : 2.0;
        }

        private static double QualityScale(int quality)
        {
            if (quality < 1)
            {
                quality = 1;
            }
            else if (quality > 100)
            {
                quality = 100;
            }

            var scale = Math.Pow(2.0, (100.0 - quality) / 12.5);
            if (scale < 0.01)
            {
                scale = 0.01;
            }

            return scale * 0.18;
        }

        private static double DwtNorm97(int level, int orientation)
        {
            if (orientation < 0 || orientation > 3)
            {
                return 1.0;
            }

            if (orientation == 0 && level >= DwtNorms97.GetLength(1))
            {
                level = DwtNorms97.GetLength(1) - 1;
            }
            else if (orientation > 0 && level >= DwtNorms97.GetLength(1) - 1)
            {
                level = DwtNorms97.GetLength(1) - 2;
            }

            if (level < 0)
            {
                level = 0;
            }

            return DwtNorms97[orientation, level];
        }

        private static float GetGainL(int decompositions)
        {
            if (decompositions < 0)
            {
                decompositions = 0;
            }

            if (decompositions >= Gain97L.Length)
            {
                decompositions = Gain97L.Length - 1;
            }

            return Gain97L[decompositions];
        }

        private static float GetGainH(int decompositions)
        {
            if (decompositions < 0)
            {
                decompositions = 0;
            }

            if (decompositions >= Gain97H.Length)
            {
                decompositions = Gain97H.Length - 1;
            }

            return Gain97H[decompositions];
        }

        private static readonly float[] Gain97L =
        {
            1.0000e+00f, 1.4021e+00f, 2.0304e+00f, 2.9012e+00f,
            4.1153e+00f, 5.8245e+00f, 8.2388e+00f, 1.1652e+01f,
            1.6479e+01f, 2.3304e+01f, 3.2957e+01f, 4.6609e+01f,
            6.5915e+01f, 9.3217e+01f, 1.3183e+02f, 1.8643e+02f,
            2.6366e+02f, 3.7287e+02f, 5.2732e+02f, 7.4574e+02f,
            1.0546e+03f, 1.4915e+03f, 2.1093e+03f, 2.9830e+03f,
            4.2185e+03f, 5.9659e+03f, 8.4371e+03f, 1.1932e+04f,
            1.6874e+04f, 2.3864e+04f, 3.3748e+04f, 4.7727e+04f,
            6.7496e+04f, 9.5454e+04f
        };

        private static readonly float[] Gain97H =
        {
            1.4425e+00f, 1.9669e+00f, 2.8839e+00f, 4.1475e+00f,
            5.8946e+00f, 8.3472e+00f, 1.1809e+01f, 1.6701e+01f,
            2.3620e+01f, 3.3403e+01f, 4.7240e+01f, 6.6807e+01f,
            9.4479e+01f, 1.3361e+02f, 1.8896e+02f, 2.6723e+02f,
            3.7792e+02f, 5.3446e+02f, 7.5583e+02f, 1.0689e+03f,
            1.5117e+03f, 2.1378e+03f, 3.0233e+03f, 4.2756e+03f,
            6.0467e+03f, 8.5513e+03f, 1.2093e+04f, 1.7103e+04f,
            2.4187e+04f, 3.4205e+04f, 4.8373e+04f, 6.8410e+04f,
            9.6747e+04f, 1.3682e+05f
        };

        private static readonly double[,] DwtNorms97 =
        {
            { 1.000, 1.965, 4.177, 8.403, 16.90, 33.84, 67.69, 135.3, 270.6, 540.9 },
            { 2.022, 3.989, 8.355, 17.04, 34.27, 68.63, 137.3, 274.6, 549.0, 0.0 },
            { 2.022, 3.989, 8.355, 17.04, 34.27, 68.63, 137.3, 274.6, 549.0, 0.0 },
            { 2.080, 3.865, 8.307, 17.18, 34.71, 69.59, 139.3, 278.6, 557.2, 0.0 }
        };
    }
}
