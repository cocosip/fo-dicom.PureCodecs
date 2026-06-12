using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegEntropyBitWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private int _currentByte;
        private int _bitsWritten;

        public void WriteBits(int value, int count)
        {
            if (count < 0 || count > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            for (var bit = count - 1; bit >= 0; bit--)
            {
                WriteBit((value >> bit) & 1);
            }
        }

        public void WriteBit(int bit)
        {
            if (bit != 0 && bit != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bit));
            }

            _currentByte = (_currentByte << 1) | bit;
            _bitsWritten++;
            if (_bitsWritten == 8)
            {
                WriteByte(_currentByte);
                _currentByte = 0;
                _bitsWritten = 0;
            }
        }

        public byte[] ToArray()
        {
            var copy = new List<byte>(_bytes);
            if (_bitsWritten > 0)
            {
                var padded = _currentByte << (8 - _bitsWritten);
                copy.Add((byte)padded);
                if (padded == 0xFF)
                {
                    copy.Add(0x00);
                }
            }

            return copy.ToArray();
        }

        private void WriteByte(int value)
        {
            _bytes.Add((byte)value);
            if (value == 0xFF)
            {
                _bytes.Add(0x00);
            }
        }
    }
}
