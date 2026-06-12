using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegEntropyBitReader
    {
        private readonly byte[] _data;
        private int _offset;
        private int _currentByte;
        private int _bitsRemaining;
        private int? _pendingMarker;

        public JpegEntropyBitReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool EndOfData
        {
            get { return _offset >= _data.Length && _bitsRemaining == 0 && !_pendingMarker.HasValue; }
        }

        public int ReadBit()
        {
            if (_bitsRemaining == 0)
            {
                LoadNextByte();
            }

            _bitsRemaining--;
            return (_currentByte >> _bitsRemaining) & 1;
        }

        public int ReadBits(int count)
        {
            if (count < 0 || count > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var value = 0;
            for (var index = 0; index < count; index++)
            {
                value = (value << 1) | ReadBit();
            }

            return value;
        }

        public byte ReadRestartMarker()
        {
            _bitsRemaining = 0;
            var marker = ReadMarker();
            if (marker < JpegMarker.RST0 || marker > JpegMarker.RST7)
            {
                throw JpegMarkerReader.CreateException($"JPEG expected restart marker but found 0x{marker:X2}.");
            }

            return (byte)marker;
        }

        private int ReadMarker()
        {
            if (_pendingMarker.HasValue)
            {
                var marker = _pendingMarker.Value;
                _pendingMarker = null;
                return marker;
            }

            if (_offset + 2 > _data.Length || _data[_offset] != 0xFF)
            {
                throw JpegMarkerReader.CreateException("JPEG restart marker prefix 0xFF was not found.");
            }

            _offset++;
            while (_offset < _data.Length && _data[_offset] == 0xFF)
            {
                _offset++;
            }

            if (_offset >= _data.Length)
            {
                throw JpegMarkerReader.CreateException("JPEG marker code is missing.");
            }

            return _data[_offset++];
        }

        private void LoadNextByte()
        {
            if (_pendingMarker.HasValue)
            {
                throw JpegMarkerReader.CreateException($"JPEG marker 0x{_pendingMarker.Value:X2} was found inside entropy data.");
            }

            if (_offset >= _data.Length)
            {
                throw JpegMarkerReader.CreateException("JPEG entropy data ended unexpectedly.");
            }

            var value = _data[_offset++];
            if (value == 0xFF)
            {
                if (_offset >= _data.Length)
                {
                    throw JpegMarkerReader.CreateException("JPEG marker code is missing.");
                }

                var marker = _data[_offset++];
                if (marker != 0x00)
                {
                    _pendingMarker = marker;
                    throw JpegMarkerReader.CreateException($"JPEG marker 0x{marker:X2} was found inside entropy data.");
                }
            }

            _currentByte = value;
            _bitsRemaining = 8;
        }
    }
}
