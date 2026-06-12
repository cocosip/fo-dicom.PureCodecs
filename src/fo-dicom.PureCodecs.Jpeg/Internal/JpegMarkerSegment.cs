namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegMarkerSegment
    {
        public JpegMarkerSegment(byte code, byte[] payload)
        {
            Code = code;
            Payload = payload ?? new byte[0];
        }

        public byte Code { get; }

        public byte[] Payload { get; }

        public bool HasPayload
        {
            get { return Payload.Length > 0; }
        }
    }
}
