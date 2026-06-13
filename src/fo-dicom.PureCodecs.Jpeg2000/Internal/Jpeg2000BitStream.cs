using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000BitWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private int _current;
        private int _bitCount;

        public void WriteBit(bool bit)
        {
            _current = (_current << 1) | (bit ? 1 : 0);
            _bitCount++;
            if (_bitCount == 8)
            {
                FlushByte();
            }
        }

        public void WriteBits(uint value, int bitCount)
        {
            for (var bit = bitCount - 1; bit >= 0; bit--)
            {
                WriteBit(((value >> bit) & 1) != 0);
            }
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _current <<= 8 - _bitCount;
                FlushByte();
            }

            return _bytes.ToArray();
        }

        private void FlushByte()
        {
            var value = (byte)_current;
            _bytes.Add(value);
            if (value == 0xFF)
            {
                _bytes.Add(0x00);
            }

            _current = 0;
            _bitCount = 0;
        }
    }

    internal sealed class Jpeg2000BitReader
    {
        private readonly byte[] _bytes;
        private int _offset;
        private int _current;
        private int _remainingBits;

        public Jpeg2000BitReader(byte[] bytes)
        {
            _bytes = bytes ?? new byte[0];
        }

        public bool ReadBit()
        {
            if (_remainingBits == 0)
            {
                FillByte();
            }

            _remainingBits--;
            return ((_current >> _remainingBits) & 1) != 0;
        }

        public uint ReadBits(int bitCount)
        {
            uint value = 0;
            for (var i = 0; i < bitCount; i++)
            {
                value = (value << 1) | (ReadBit() ? 1u : 0u);
            }

            return value;
        }

        private void FillByte()
        {
            if (_offset >= _bytes.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 bitstream ended unexpectedly.");
            }

            _current = _bytes[_offset++];
            if (_current == 0xFF && _offset < _bytes.Length && _bytes[_offset] == 0x00)
            {
                _offset++;
            }

            _remainingBits = 8;
        }
    }
}
