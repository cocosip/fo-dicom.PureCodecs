namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000CodingStyleComponent
    {
        private Jpeg2000CodingStyleComponent(int componentIndex, Jpeg2000CodingStyle style)
        {
            ComponentIndex = componentIndex;
            Style = style;
        }

        public int ComponentIndex { get; }

        private Jpeg2000CodingStyle Style { get; }

        public Jpeg2000ResolvedCodingStyle InheritFrom(Jpeg2000CodingStyleDefault defaults)
        {
            return new Jpeg2000ResolvedCodingStyle(ComponentIndex, defaults, Style);
        }

        public static Jpeg2000CodingStyleComponent Parse(Jpeg2000MarkerSegment segment, int componentCount)
        {
            if (segment.Code != Jpeg2000Marker.COC)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COC marker segment expected.");
            }

            var payload = segment.Payload;
            var componentBytes = componentCount <= 256 ? 1 : 2;
            if (payload.Length < componentBytes + 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COC payload is too short.");
            }

            var componentIndex = componentBytes == 1 ? payload[0] : Jpeg2000Binary.ReadUInt16(payload, 0);
            if (componentIndex >= componentCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COC component index is outside the component range.");
            }

            var scoc = payload[componentBytes];
            var style = Jpeg2000CodingStyle.ParseStyle(
                payload,
                componentBytes + 1,
                scoc,
                Jpeg2000ProgressionOrder.LRCP,
                0,
                false);
            return new Jpeg2000CodingStyleComponent(componentIndex, style);
        }
    }
}
