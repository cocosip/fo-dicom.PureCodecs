using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000QuantizationModel
    {
        private readonly double[] _stepSizes;

        private Jpeg2000QuantizationModel(Jpeg2000QuantizationStyle style, int guardBits, double[] stepSizes)
        {
            Style = style;
            GuardBits = guardBits;
            _stepSizes = stepSizes;
        }

        public Jpeg2000QuantizationStyle Style { get; }

        public int GuardBits { get; }

        public int SubbandCount => _stepSizes.Length;

        public static Jpeg2000QuantizationModel From(
            Jpeg2000Quantization quantization,
            int precision,
            int decompositionLevels)
        {
            if (quantization == null || precision < 1 || decompositionLevels < 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 quantization model inputs are invalid.");
            }

            var subbandCount = decompositionLevels == 0 ? 1 : 1 + (decompositionLevels * 3);
            var steps = new double[subbandCount];

            switch (quantization.Style)
            {
                case Jpeg2000QuantizationStyle.None:
                    for (var i = 0; i < steps.Length; i++)
                    {
                        steps[i] = 1.0;
                    }

                    break;
                case Jpeg2000QuantizationStyle.ScalarDerived:
                    if (quantization.StepSizes.Count != 1)
                    {
                        throw Jpeg2000Binary.CreateException("JPEG 2000 scalar-derived quantization requires one base step size.");
                    }

                    var baseStep = Jpeg2000QuantizationTable.DecodeStepSize(quantization.StepSizes[0], precision);
                    steps[0] = baseStep;
                    for (var i = 1; i < steps.Length; i++)
                    {
                        var resolutionLevel = ((i - 1) / 3) + 1;
                        steps[i] = baseStep / (1 << resolutionLevel);
                    }

                    break;
                case Jpeg2000QuantizationStyle.ScalarExpounded:
                    if (quantization.StepSizes.Count != subbandCount)
                    {
                        throw Jpeg2000Binary.CreateException("JPEG 2000 scalar-expounded quantization subband count is invalid.");
                    }

                    for (var i = 0; i < steps.Length; i++)
                    {
                        steps[i] = Jpeg2000QuantizationTable.DecodeStepSize(quantization.StepSizes[i], precision);
                    }

                    break;
                default:
                    throw Jpeg2000Binary.CreateException("JPEG 2000 quantization style is unsupported.");
            }

            return new Jpeg2000QuantizationModel(quantization.Style, quantization.GuardBits, steps);
        }

        public double GetStepSize(int subbandIndex)
        {
            if (subbandIndex < 0 || subbandIndex >= _stepSizes.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 quantization subband index is out of range.");
            }

            return _stepSizes[subbandIndex];
        }

        public double InverseQuantize(int coefficient, int subbandIndex)
        {
            return coefficient * GetStepSize(subbandIndex);
        }

    }
}
