using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public class Jpeg2000CodingStyle
    {
        protected Jpeg2000CodingStyle(
            bool hasPrecinctSizes,
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform,
            int decompositionLevels,
            int codeBlockWidth,
            int codeBlockHeight,
            byte codeBlockStyle,
            byte transformation,
            byte[] precinctSizes)
        {
            HasPrecinctSizes = hasPrecinctSizes;
            ProgressionOrder = progressionOrder;
            LayerCount = layerCount;
            UsesMultipleComponentTransform = usesMultipleComponentTransform;
            DecompositionLevels = decompositionLevels;
            CodeBlockWidth = codeBlockWidth;
            CodeBlockHeight = codeBlockHeight;
            CodeBlockStyle = codeBlockStyle;
            Transformation = transformation;
            PrecinctSizes = precinctSizes;
        }

        public bool HasPrecinctSizes { get; }

        public Jpeg2000ProgressionOrder ProgressionOrder { get; }

        public int LayerCount { get; }

        public bool UsesMultipleComponentTransform { get; }

        public int DecompositionLevels { get; }

        public int CodeBlockWidth { get; }

        public int CodeBlockHeight { get; }

        public byte CodeBlockStyle { get; }

        public byte Transformation { get; }

        public IReadOnlyList<byte> PrecinctSizes { get; }

        internal static Jpeg2000CodingStyle ParseStyle(byte[] payload, int offset, int scod, Jpeg2000ProgressionOrder progressionOrder, int layerCount, bool usesMct)
        {
            if (payload.Length < offset + 5)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 coding style payload is too short.");
            }

            var decompositionLevels = payload[offset];
            var codeBlockWidth = 1 << (payload[offset + 1] + 2);
            var codeBlockHeight = 1 << (payload[offset + 2] + 2);
            var codeBlockStyle = payload[offset + 3];
            var transformation = payload[offset + 4];
            offset += 5;

            var hasPrecinctSizes = (scod & 0x01) != 0;
            var expectedPrecinctCount = hasPrecinctSizes ? decompositionLevels + 1 : 0;
            if (payload.Length != offset + expectedPrecinctCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 coding style precinct payload length is invalid.");
            }

            var precinctSizes = new byte[expectedPrecinctCount];
            if (expectedPrecinctCount > 0)
            {
                System.Buffer.BlockCopy(payload, offset, precinctSizes, 0, expectedPrecinctCount);
            }

            return new Jpeg2000CodingStyle(
                hasPrecinctSizes,
                progressionOrder,
                layerCount,
                usesMct,
                decompositionLevels,
                codeBlockWidth,
                codeBlockHeight,
                codeBlockStyle,
                transformation,
                precinctSizes);
        }

        protected static Jpeg2000ProgressionOrder ReadProgressionOrder(byte value)
        {
            if (value > 4)
            {
                throw Jpeg2000Binary.CreateException($"JPEG 2000 progression order {value} is not supported.");
            }

            return (Jpeg2000ProgressionOrder)value;
        }
    }
}
