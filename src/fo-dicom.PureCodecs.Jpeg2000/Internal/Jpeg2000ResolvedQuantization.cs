using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ResolvedQuantization
    {
        public Jpeg2000ResolvedQuantization(int componentIndex, Jpeg2000QuantizationComponent component, int defaultGuardBits)
        {
            ComponentIndex = componentIndex;
            Style = component.Style;
            GuardBits = component.GuardBits;
            DefaultGuardBits = defaultGuardBits;

            var values = new ushort[component.StepSizes.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = component.StepSizes[i];
            }

            StepSizes = values;
        }

        public int ComponentIndex { get; }

        public Jpeg2000QuantizationStyle Style { get; }

        public int GuardBits { get; }

        public int DefaultGuardBits { get; }

        public IReadOnlyList<ushort> StepSizes { get; }
    }
}
