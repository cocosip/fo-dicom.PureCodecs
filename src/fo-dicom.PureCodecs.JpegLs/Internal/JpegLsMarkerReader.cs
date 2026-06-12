using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsMarkerReader
    {
        private readonly byte[] _data;
        private int _offset;

        public JpegLsMarkerReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool EndOfData
        {
            get { return _offset >= _data.Length; }
        }

        public JpegLsMarkerSegment ReadNextSkippingMetadata()
        {
            while (true)
            {
                var segment = ReadNext();
                if (!JpegLsMarker.IsMetadata(segment.Code))
                {
                    return segment;
                }
            }
        }

        public JpegLsMarkerSegment ReadNext()
        {
            SkipFillBytes();
            if (_offset >= _data.Length || _data[_offset] != 0xFF)
            {
                throw CreateException("JPEG-LS marker prefix 0xFF was not found.");
            }

            _offset++;
            while (_offset < _data.Length && _data[_offset] == 0xFF)
            {
                _offset++;
            }

            if (_offset >= _data.Length)
            {
                throw CreateException("JPEG-LS marker code is missing.");
            }

            var code = _data[_offset++];
            if (!JpegLsMarker.HasLength(code))
            {
                return new JpegLsMarkerSegment(code, new byte[0]);
            }

            if (_offset + 2 > _data.Length)
            {
                throw CreateException("JPEG-LS marker length is missing.");
            }

            var length = ReadUInt16BigEndian(_data, _offset);
            _offset += 2;
            if (length < 2)
            {
                throw CreateException($"JPEG-LS marker 0x{code:X2} has invalid length {length}.");
            }

            var payloadLength = length - 2;
            if (_offset + payloadLength > _data.Length)
            {
                throw CreateException($"JPEG-LS marker 0x{code:X2} length exceeds the input buffer.");
            }

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(_data, _offset, payload, 0, payloadLength);
            _offset += payloadLength;
            return new JpegLsMarkerSegment(code, payload);
        }

        public byte[] ReadEntropyDataUntilMarker(byte marker)
        {
            var start = _offset;
            while (_offset < _data.Length)
            {
                if (_data[_offset] != 0xFF)
                {
                    _offset++;
                    continue;
                }

                if (_offset + 1 >= _data.Length)
                {
                    throw CreateException("JPEG-LS marker code is missing.");
                }

                var markerOffset = _offset + 1;
                while (markerOffset < _data.Length && _data[markerOffset] == 0xFF)
                {
                    markerOffset++;
                }

                if (markerOffset >= _data.Length)
                {
                    throw CreateException("JPEG-LS marker code is missing.");
                }

                var code = _data[markerOffset];
                if (code < 0x80)
                {
                    _offset += 2;
                    continue;
                }

                var length = _offset - start;
                var payload = new byte[length];
                Buffer.BlockCopy(_data, start, payload, 0, length);
                _offset = markerOffset + 1;

                if (code != marker)
                {
                    throw CreateException($"JPEG-LS expected marker 0x{marker:X2} after scan data but found 0x{code:X2}.");
                }

                return payload;
            }

            throw CreateException($"JPEG-LS marker 0x{marker:X2} was not found after scan data.");
        }

        private void SkipFillBytes()
        {
            while (_offset < _data.Length && _data[_offset] != 0xFF)
            {
                _offset++;
            }
        }

        internal static int ReadUInt16BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 8) | bytes[offset + 1];
        }

        internal static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
