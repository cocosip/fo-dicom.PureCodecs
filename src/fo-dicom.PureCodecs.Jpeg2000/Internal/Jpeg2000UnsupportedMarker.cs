namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    [System.Obsolete("PPM and PPT packed packet headers are supported by the codestream decoder.")]
    internal static class Jpeg2000UnsupportedMarker
    {
        [System.Obsolete("PPM and PPT packed packet headers are supported by the codestream decoder.")]
        public static void RejectPackedPacketHeaders(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.PPM && segment.Code != Jpeg2000Marker.PPT)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 PPM or PPT marker segment expected.");
            }

            throw Jpeg2000Binary.CreateException(
                "This obsolete compatibility method always rejects packed packet headers; use the codestream decoder instead.");
        }
    }
}
