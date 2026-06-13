using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000BioReader
    {
        private readonly byte[] _data;
        private int _position;
        private ushort _buffer;
        private int _bitCount;

        public Jpeg2000BioReader(byte[] data, int offset)
        {
            _data = data ?? new byte[0];
            _position = offset;
        }

        public int BytesRead
        {
            get { return _position; }
        }

        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                ByteIn();
            }

            _bitCount--;
            return (_buffer >> _bitCount) & 1;
        }

        public int ReadBits(int count)
        {
            if (count <= 0 || count > 32)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 BIO bit count is invalid.");
            }

            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = (value << 1) | ReadBit();
            }

            return value;
        }

        public void AlignToByte()
        {
            if ((_buffer & 0xFF) == 0xFF)
            {
                ByteIn();
            }

            _bitCount = 0;
        }

        private void ByteIn()
        {
            if (_position >= _data.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 packet header ended unexpectedly.");
            }

            _buffer = (ushort)((_buffer << 8) & 0xFFFF);
            _bitCount = _buffer == 0xFF00 ? 7 : 8;
            _buffer |= _data[_position++];
        }
    }

    internal sealed class Jpeg2000RawBitReader
    {
        private readonly byte[] _data;
        private int _position;
        private uint _buffer;
        private int _bitCount;

        public Jpeg2000RawBitReader(byte[] data)
        {
            _data = data ?? new byte[0];
        }

        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                if (_buffer == 0xFF)
                {
                    var next = ReadByte();
                    if (next > 0x8F)
                    {
                        _buffer = 0xFF;
                        _bitCount = 8;
                    }
                    else
                    {
                        _buffer = next;
                        _bitCount = 7;
                    }
                }
                else
                {
                    _buffer = ReadByte();
                    _bitCount = 8;
                }
            }

            _bitCount--;
            return (int)((_buffer >> _bitCount) & 1);
        }

        private byte ReadByte()
        {
            if (_position >= _data.Length)
            {
                return 0xFF;
            }

            return _data[_position++];
        }
    }
}
