namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000UnsupportedMarker
    {
        public static void RejectPackedPacketHeaders(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.PPM && segment.Code != Jpeg2000Marker.PPT)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 PPM or PPT marker segment expected.");
            }

            throw Jpeg2000Binary.CreateException($"JPEG 2000 marker 0x{segment.Code:X2} packed packet headers are unsupported by the current managed pipeline.");
        }
    }
}
