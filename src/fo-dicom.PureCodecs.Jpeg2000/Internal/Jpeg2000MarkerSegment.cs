namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000MarkerSegment
    {
        public Jpeg2000MarkerSegment(byte code, byte[] payload)
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
