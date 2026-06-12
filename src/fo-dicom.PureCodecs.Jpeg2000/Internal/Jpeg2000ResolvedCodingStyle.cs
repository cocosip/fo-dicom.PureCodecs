using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ResolvedCodingStyle
    {
        public Jpeg2000ResolvedCodingStyle(int componentIndex, Jpeg2000CodingStyleDefault defaults, Jpeg2000CodingStyle component)
        {
            ComponentIndex = componentIndex;
            ProgressionOrder = defaults.ProgressionOrder;
            LayerCount = defaults.LayerCount;
            UsesMultipleComponentTransform = defaults.UsesMultipleComponentTransform;
            HasPrecinctSizes = component.HasPrecinctSizes;
            DecompositionLevels = component.DecompositionLevels;
            CodeBlockWidth = component.CodeBlockWidth;
            CodeBlockHeight = component.CodeBlockHeight;
            CodeBlockStyle = component.CodeBlockStyle;
            Transformation = component.Transformation;

            var precincts = new byte[component.PrecinctSizes.Count];
            for (var i = 0; i < precincts.Length; i++)
            {
                precincts[i] = component.PrecinctSizes[i];
            }

            PrecinctSizes = precincts;
        }

        public int ComponentIndex { get; }

        public Jpeg2000ProgressionOrder ProgressionOrder { get; }

        public int LayerCount { get; }

        public bool UsesMultipleComponentTransform { get; }

        public bool HasPrecinctSizes { get; }

        public int DecompositionLevels { get; }

        public int CodeBlockWidth { get; }

        public int CodeBlockHeight { get; }

        public byte CodeBlockStyle { get; }

        public byte Transformation { get; }

        public IReadOnlyList<byte> PrecinctSizes { get; }
    }
}
