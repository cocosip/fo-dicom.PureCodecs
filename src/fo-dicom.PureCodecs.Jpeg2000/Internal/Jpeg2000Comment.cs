using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000Comment
    {
        private Jpeg2000Comment(int registrationValue, byte[] payload)
        {
            RegistrationValue = registrationValue;
            Payload = payload;
        }

        public int RegistrationValue { get; }

        public byte[] Payload { get; }

        public static Jpeg2000Comment Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.COM)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COM marker segment expected.");
            }

            if (segment.Payload.Length < 2)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COM payload is too short.");
            }

            var payload = new byte[segment.Payload.Length - 2];
            Buffer.BlockCopy(segment.Payload, 2, payload, 0, payload.Length);
            return new Jpeg2000Comment(Jpeg2000Binary.ReadUInt16(segment.Payload, 0), payload);
        }
    }
}
