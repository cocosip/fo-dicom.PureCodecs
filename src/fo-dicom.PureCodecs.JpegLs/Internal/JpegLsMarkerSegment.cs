using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsMarkerSegment
    {
        public JpegLsMarkerSegment(byte code, byte[] payload)
        {
            Code = code;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public byte Code { get; }

        public byte[] Payload { get; }

        public bool HasPayload
        {
            get { return Payload.Length > 0; }
        }
    }
}
