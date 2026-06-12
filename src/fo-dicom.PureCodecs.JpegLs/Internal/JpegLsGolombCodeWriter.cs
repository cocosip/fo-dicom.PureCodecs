using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsGolombCodeWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private byte _currentByte;
        private int _bitCount;

        public void Write(int value, int parameter)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "JPEG-LS Golomb value cannot be negative.");
            }

            if (parameter < 0 || parameter > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), "JPEG-LS Golomb parameter is out of range.");
            }

            var quotient = value >> parameter;
            for (var i = 0; i < quotient; i++)
            {
                WriteBit(0);
            }

            WriteBit(1);
            for (var bit = parameter - 1; bit >= 0; bit--)
            {
                WriteBit((value >> bit) & 1);
            }
        }

        public void EncodeMappedValue(int parameter, int mappedError, int limit, int quantizedBitsPerPixel)
        {
            var highBits = mappedError >> parameter;
            if (highBits < limit - (quantizedBitsPerPixel + 1))
            {
                WriteUnary(highBits);
                if (parameter > 0)
                {
                    WriteBits(mappedError & ((1 << parameter) - 1), parameter);
                }

                return;
            }

            WriteUnary(limit - quantizedBitsPerPixel - 1);
            WriteBits((mappedError - 1) & ((1 << quantizedBitsPerPixel) - 1), quantizedBitsPerPixel);
        }

        public void WriteBit(int bit)
        {
            WriteBits(bit & 1, 1);
        }

        public void WriteBits(int value, int bitCount)
        {
            if (bitCount < 0 || bitCount > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "JPEG-LS bit count is out of range.");
            }

            for (var bit = bitCount - 1; bit >= 0; bit--)
            {
                WriteRawBit((value >> bit) & 1);
            }
        }

        private void WriteUnary(int zeros)
        {
            for (var i = 0; i < zeros; i++)
            {
                WriteRawBit(0);
            }

            WriteRawBit(1);
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _bytes.Add(_currentByte);
                _currentByte = 0;
                _bitCount = 0;
            }

            return _bytes.ToArray();
        }

        private void WriteRawBit(int bit)
        {
            _currentByte = (byte)(_currentByte | ((bit & 1) << (7 - _bitCount)));
            _bitCount++;

            if (_bitCount == 8)
            {
                _bytes.Add(_currentByte);
                if (_currentByte == 0xFF)
                {
                    _currentByte = 0;
                    _bitCount = 1;
                    return;
                }

                _currentByte = 0;
                _bitCount = 0;
                return;
            }
        }
    }
}
