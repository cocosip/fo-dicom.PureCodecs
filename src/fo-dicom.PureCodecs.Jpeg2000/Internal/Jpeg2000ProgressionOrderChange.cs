using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ProgressionOrderChange
    {
        private Jpeg2000ProgressionOrderChange(
            int layerStart,
            int resolutionEnd,
            int componentStart,
            int layerEnd,
            int componentEnd,
            Jpeg2000ProgressionOrder progressionOrder)
        {
            LayerStart = layerStart;
            ResolutionEnd = resolutionEnd;
            ComponentStart = componentStart;
            LayerEnd = layerEnd;
            ComponentEnd = componentEnd;
            ProgressionOrder = progressionOrder;
        }

        public int LayerStart { get; }

        public int ResolutionEnd { get; }

        public int ComponentStart { get; }

        public int LayerEnd { get; }

        public int ComponentEnd { get; }

        public Jpeg2000ProgressionOrder ProgressionOrder { get; }

        public static IReadOnlyList<Jpeg2000ProgressionOrderChange> Parse(Jpeg2000MarkerSegment segment, int componentCount)
        {
            if (segment.Code != Jpeg2000Marker.POC)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 POC marker segment expected.");
            }

            var componentBytes = componentCount <= 256 ? 1 : 2;
            var entryLength = componentBytes == 1 ? 7 : 9;
            if (segment.Payload.Length == 0 || segment.Payload.Length % entryLength != 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 POC payload length is invalid.");
            }

            var changes = new Jpeg2000ProgressionOrderChange[segment.Payload.Length / entryLength];
            var offset = 0;
            for (var i = 0; i < changes.Length; i++)
            {
                var layerStart = segment.Payload[offset++];
                var resolutionEnd = segment.Payload[offset++];
                var componentStart = componentBytes == 1
                    ? segment.Payload[offset++]
                    : Jpeg2000Binary.ReadUInt16(segment.Payload, Advance(ref offset, 2));
                var layerEnd = Jpeg2000Binary.ReadUInt16(segment.Payload, offset);
                offset += 2;
                var componentEnd = componentBytes == 1
                    ? segment.Payload[offset++]
                    : Jpeg2000Binary.ReadUInt16(segment.Payload, Advance(ref offset, 2));
                var progression = segment.Payload[offset++];

                if (progression > 4)
                {
                    throw Jpeg2000Binary.CreateException($"JPEG 2000 progression order {progression} is not supported.");
                }

                changes[i] = new Jpeg2000ProgressionOrderChange(
                    layerStart,
                    resolutionEnd,
                    componentStart,
                    layerEnd,
                    componentEnd,
                    (Jpeg2000ProgressionOrder)progression);
            }

            return changes;
        }

        private static int Advance(ref int offset, int count)
        {
            var current = offset;
            offset += count;
            return current;
        }
    }
}
