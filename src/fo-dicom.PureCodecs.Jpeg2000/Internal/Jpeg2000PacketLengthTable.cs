using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000PacketLengthTable
    {
        private Jpeg2000PacketLengthTable(int index, uint[] packetLengths)
        {
            Index = index;
            PacketLengths = packetLengths;
        }

        public int Index { get; }

        public IReadOnlyList<uint> PacketLengths { get; }

        public static Jpeg2000PacketLengthTable Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.PLT)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 PLT marker segment expected.");
            }

            if (segment.Payload.Length < 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 PLT payload is too short.");
            }

            var values = new List<uint>();
            var offset = 1;
            while (offset < segment.Payload.Length)
            {
                uint value = 0;
                var hasTerminator = false;
                while (offset < segment.Payload.Length)
                {
                    var current = segment.Payload[offset++];
                    value = (value << 7) | (uint)(current & 0x7F);
                    if ((current & 0x80) == 0)
                    {
                        hasTerminator = true;
                        break;
                    }
                }

                if (!hasTerminator)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 PLT packet length uses an unterminated variable-length integer.");
                }

                values.Add(value);
            }

            return new Jpeg2000PacketLengthTable(segment.Payload[0], values.ToArray());
        }
    }
}
