using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000ResolvedQuantization
    {
        public Jpeg2000ResolvedQuantization(int componentIndex, Jpeg2000QuantizationComponent component, int defaultGuardBits)
            : this(componentIndex, component.Style, component.GuardBits, defaultGuardBits, component.StepSizes)
        {
        }

        private Jpeg2000ResolvedQuantization(
            int componentIndex,
            Jpeg2000QuantizationStyle style,
            int guardBits,
            int defaultGuardBits,
            IReadOnlyList<ushort> stepSizes)
        {
            ComponentIndex = componentIndex;
            Style = style;
            GuardBits = guardBits;
            DefaultGuardBits = defaultGuardBits;

            var values = new ushort[stepSizes.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = stepSizes[i];
            }

            StepSizes = values;
        }

        public static Jpeg2000ResolvedQuantization FromDefault(
            int componentIndex,
            Jpeg2000QuantizationDefault defaults)
        {
            return new Jpeg2000ResolvedQuantization(
                componentIndex,
                defaults.Style,
                defaults.GuardBits,
                defaults.GuardBits,
                defaults.StepSizes);
        }

        public int ComponentIndex { get; }

        public Jpeg2000QuantizationStyle Style { get; }

        public int GuardBits { get; }

        public int DefaultGuardBits { get; }

        public IReadOnlyList<ushort> StepSizes { get; }
    }
}
