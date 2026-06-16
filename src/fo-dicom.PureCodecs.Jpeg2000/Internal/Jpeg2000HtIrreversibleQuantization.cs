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
            1.0000e+00f, 1.4021e+00f, 1.9669e+00f, 2.7593e+00f,
            3.8705e+00f, 5.4297e+00f, 7.6172e+00f, 1.0686e+01f,
            1.4992e+01f, 2.1035e+01f, 2.9516e+01f, 4.1418e+01f,
            5.8119e+01f, 8.1556e+01f, 1.1444e+02f, 1.6059e+02f,
            2.2535e+02f, 3.1623e+02f, 4.4376e+02f, 6.2273e+02f,
            8.7386e+02f, 1.2262e+03f, 1.7208e+03f, 2.4148e+03f,
            3.3886e+03f, 4.7550e+03f, 6.6724e+03f, 9.3631e+03f,
            1.3139e+04f, 1.8438e+04f, 2.5875e+04f, 3.6313e+04f,
            5.0962e+04f, 7.1520e+04f
        };

        private static readonly float[] Gain97H =
        {
            1.4425e+00f, 2.0228e+00f, 2.8374e+00f, 3.9801e+00f,
            5.5830e+00f, 7.8315e+00f, 1.0986e+01f, 1.5410e+01f,
            2.1617e+01f, 3.0325e+01f, 4.2541e+01f, 5.9679e+01f,
            8.3721e+01f, 1.1745e+02f, 1.6478e+02f, 2.3119e+02f,
            3.2436e+02f, 4.5507e+02f, 6.3845e+02f, 8.9574e+02f,
            1.2567e+03f, 1.7632e+03f, 2.4737e+03f, 3.4707e+03f,
            4.8696e+03f, 6.8323e+03f, 9.5861e+03f, 1.3450e+04f,
            1.8872e+04f, 2.6479e+04f, 3.7152e+04f, 5.2127e+04f,
            7.3138e+04f, 1.0262e+05f
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
