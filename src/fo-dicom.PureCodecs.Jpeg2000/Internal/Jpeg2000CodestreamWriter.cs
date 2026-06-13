using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000CodestreamWriter
    {
        private readonly List<byte> _bytes = new List<byte>();

        public void WriteStandalone(byte marker)
        {
            if (Jpeg2000Marker.HasLength(marker))
            {
                throw new ArgumentException("JPEG 2000 marker requires a segment payload.", nameof(marker));
            }

            _bytes.Add(0xFF);
            _bytes.Add(marker);
        }

        public void WriteSegment(byte marker, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!Jpeg2000Marker.HasLength(marker))
            {
                throw new ArgumentException("Standalone JPEG 2000 markers cannot have a segment payload.", nameof(marker));
            }

            var length = payload.Length + 2;
            if (length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(payload), "JPEG 2000 marker payload is too large.");
            }

            _bytes.Add(0xFF);
            _bytes.Add(marker);
            _bytes.Add((byte)(length >> 8));
            _bytes.Add((byte)length);
            _bytes.AddRange(payload);
        }

        public void WriteRaw(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _bytes.AddRange(payload);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }
}
