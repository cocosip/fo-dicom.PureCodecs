using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegMarkerWriter
    {
        private readonly List<byte> _bytes = new List<byte>();

        public void WriteStandalone(byte marker)
        {
            _bytes.Add(0xFF);
            _bytes.Add(marker);
        }

        public void WriteSegment(byte marker, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!JpegMarker.HasLength(marker))
            {
                throw new ArgumentException("Standalone JPEG markers cannot have a segment payload.", nameof(marker));
            }

            var length = payload.Length + 2;
            if (length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(payload), "JPEG marker payload is too large.");
            }

            _bytes.Add(0xFF);
            _bytes.Add(marker);
            _bytes.Add((byte)(length >> 8));
            _bytes.Add((byte)length);
            _bytes.AddRange(payload);
        }

        public void WriteRaw(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            _bytes.AddRange(bytes);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }
}
