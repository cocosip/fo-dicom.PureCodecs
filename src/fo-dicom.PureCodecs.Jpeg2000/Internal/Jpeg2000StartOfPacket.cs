namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000StartOfPacket
    {
        private Jpeg2000StartOfPacket(int sequenceNumber)
        {
            SequenceNumber = sequenceNumber;
        }

        public int SequenceNumber { get; }

        public static Jpeg2000StartOfPacket Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.SOP)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOP marker segment expected.");
            }

            if (segment.Payload.Length != 2)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOP payload length is invalid.");
            }

            return new Jpeg2000StartOfPacket(Jpeg2000Binary.ReadUInt16(segment.Payload, 0));
        }
    }
}
