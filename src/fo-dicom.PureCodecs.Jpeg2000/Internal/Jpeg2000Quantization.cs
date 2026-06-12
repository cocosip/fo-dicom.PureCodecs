using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public class Jpeg2000Quantization
    {
        protected Jpeg2000Quantization(Jpeg2000QuantizationStyle style, int guardBits, ushort[] stepSizes)
        {
            Style = style;
            GuardBits = guardBits;
            StepSizes = stepSizes;
        }

        public Jpeg2000QuantizationStyle Style { get; }

        public int GuardBits { get; }

        public IReadOnlyList<ushort> StepSizes { get; }

        protected static Jpeg2000Quantization ParsePayload(byte[] payload, int offset)
        {
            if (payload.Length <= offset)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 quantization payload is too short.");
            }

            var sqcd = payload[offset++];
            var style = (Jpeg2000QuantizationStyle)(sqcd & 0x1F);
            if (style != Jpeg2000QuantizationStyle.None
                && style != Jpeg2000QuantizationStyle.ScalarDerived
                && style != Jpeg2000QuantizationStyle.ScalarExpounded)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 quantization style is invalid.");
            }

            var guardBits = sqcd >> 5;
            var remaining = payload.Length - offset;
            if (style == Jpeg2000QuantizationStyle.None)
            {
                var values = new ushort[remaining];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = payload[offset + i];
                }

                return new Jpeg2000Quantization(style, guardBits, values);
            }

            if (remaining % 2 != 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 quantization step-size payload length is invalid.");
            }

            var stepSizes = new ushort[remaining / 2];
            for (var i = 0; i < stepSizes.Length; i++)
            {
                stepSizes[i] = (ushort)Jpeg2000Binary.ReadUInt16(payload, offset + i * 2);
            }

            return new Jpeg2000Quantization(style, guardBits, stepSizes);
        }

        protected static ushort[] CopySteps(Jpeg2000Quantization quantization)
        {
            var values = new ushort[quantization.StepSizes.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = quantization.StepSizes[i];
            }

            return values;
        }
    }
}
