using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000CodestreamReader
    {
        private static readonly byte[] Jp2Signature =
        {
            0x00, 0x00, 0x00, 0x0C,
            0x6A, 0x50, 0x20, 0x20,
            0x0D, 0x0A, 0x87, 0x0A
        };

        private readonly byte[] _data;
        private int _offset;

        public Jpeg2000CodestreamReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool EndOfData
        {
            get { return _offset >= _data.Length; }
        }

        public Jpeg2000MarkerSegment ReadNext()
        {
            if (_offset >= _data.Length || _data[_offset] != 0xFF)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 marker prefix 0xFF was not found.");
            }

            _offset++;
            if (_offset >= _data.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 marker code is missing.");
            }

            var code = _data[_offset++];
            if (!Jpeg2000Marker.HasLength(code))
            {
                return new Jpeg2000MarkerSegment(code, new byte[0]);
            }

            if (_offset + 2 > _data.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 marker length is missing.");
            }

            var length = Jpeg2000Binary.ReadUInt16(_data, _offset);
            _offset += 2;
            if (length < 2)
            {
                throw Jpeg2000Binary.CreateException($"JPEG 2000 marker 0x{code:X2} has invalid length {length}.");
            }

            var payloadLength = length - 2;
            if (_offset + payloadLength > _data.Length)
            {
                throw Jpeg2000Binary.CreateException($"JPEG 2000 marker 0x{code:X2} length exceeds the input buffer.");
            }

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(_data, _offset, payload, 0, payloadLength);
            _offset += payloadLength;
            return new Jpeg2000MarkerSegment(code, payload);
        }

        public byte[] ReadTileDataUntilEoc()
        {
            var end = _offset;
            while (end + 1 < _data.Length)
            {
                if (_data[end] == 0xFF && _data[end + 1] == Jpeg2000Marker.EOC)
                {
                    var payload = new byte[end - _offset];
                    Buffer.BlockCopy(_data, _offset, payload, 0, payload.Length);
                    _offset = end;
                    return payload;
                }

                end++;
            }

            throw Jpeg2000Binary.CreateException("JPEG 2000 tile data was not terminated by EOC.");
        }

        public byte[] ReadTileData(Jpeg2000StartOfTilePart sot)
        {
            if (sot == null)
            {
                throw new ArgumentNullException(nameof(sot));
            }

            if (sot.TilePartLength == 0)
            {
                return ReadTileDataUntilEoc();
            }

            const int sotMarkerAndSegmentLength = 12;
            const int sodMarkerLength = 2;
            var tileDataLength = checked((int)sot.TilePartLength - sotMarkerAndSegmentLength - sodMarkerLength);
            if (tileDataLength < 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT tile-part length is invalid.");
            }

            if (_offset + tileDataLength > _data.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT tile-part length exceeds the input buffer.");
            }

            var payload = new byte[tileDataLength];
            Buffer.BlockCopy(_data, _offset, payload, 0, payload.Length);
            _offset += tileDataLength;
            return payload;
        }

        public static bool IsRawCodestream(byte[] data)
        {
            return data != null
                && data.Length >= 2
                && data[0] == 0xFF
                && data[1] == Jpeg2000Marker.SOC;
        }

        public static bool IsJp2Wrapped(byte[] data)
        {
            if (data == null || data.Length < Jp2Signature.Length)
            {
                return false;
            }

            for (var i = 0; i < Jp2Signature.Length; i++)
            {
                if (data[i] != Jp2Signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static void EnsureRawCodestream(byte[] data)
        {
            if (IsRawCodestream(data))
            {
                return;
            }

            if (IsJp2Wrapped(data))
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 JP2 wrapper frames are unsupported by the current codestream reader.");
            }

            throw Jpeg2000Binary.CreateException("JPEG 2000 raw codestream SOC marker was not found.");
        }
    }
}
