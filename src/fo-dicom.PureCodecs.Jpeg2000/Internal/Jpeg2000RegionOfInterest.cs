namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000RegionOfInterest
    {
        private Jpeg2000RegionOfInterest(int componentIndex, int style, int shift)
        {
            ComponentIndex = componentIndex;
            Style = style;
            Shift = shift;
        }

        public int ComponentIndex { get; }

        public int Style { get; }

        public int Shift { get; }

        public bool IsSupportedForDecoding
        {
            get { return false; }
        }

        public string UnsupportedBehavior
        {
            get { return "JPEG 2000 ROI marker is parsed but unsupported for decoding in the current managed pipeline."; }
        }

        public static Jpeg2000RegionOfInterest Parse(Jpeg2000MarkerSegment segment, int componentCount)
        {
            if (segment.Code != Jpeg2000Marker.RGN)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 RGN marker segment expected.");
            }

            var componentBytes = componentCount <= 256 ? 1 : 2;
            if (segment.Payload.Length != componentBytes + 2)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 RGN payload length is invalid.");
            }

            var componentIndex = componentBytes == 1 ? segment.Payload[0] : Jpeg2000Binary.ReadUInt16(segment.Payload, 0);
            if (componentIndex >= componentCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 RGN component index is outside the component range.");
            }

            return new Jpeg2000RegionOfInterest(
                componentIndex,
                segment.Payload[componentBytes],
                segment.Payload[componentBytes + 1]);
        }
    }
}
