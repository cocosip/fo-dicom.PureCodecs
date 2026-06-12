using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsGolombCodeReader
    {
        private readonly byte[] _bytes;
        private int _position;
        private int _currentByte;
        private int _bitsRemaining;
        private bool _previousByteWasFF;

        public JpegLsGolombCodeReader(byte[] bytes)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public int Read(int parameter)
        {
            if (parameter < 0 || parameter > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), "JPEG-LS Golomb parameter is out of range.");
            }

            var quotient = 0;
            while (ReadBit() == 0)
            {
                quotient++;
            }

            var remainder = 0;
            for (var i = 0; i < parameter; i++)
            {
                remainder = (remainder << 1) | ReadBit();
            }

            return (quotient << parameter) | remainder;
        }

        public int DecodeMappedValue(int parameter, int limit, int quantizedBitsPerPixel)
        {
            var highBits = 0;
            while (ReadBit() == 0)
            {
                highBits++;
            }

            if (highBits >= limit - (quantizedBitsPerPixel + 1))
            {
                return ReadBits(quantizedBitsPerPixel) + 1;
            }

            return parameter == 0 ? highBits : (highBits << parameter) + ReadBits(parameter);
        }

        public int ReadBits(int bitCount)
        {
            if (bitCount < 0 || bitCount > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount), "JPEG-LS bit count is out of range.");
            }

            var value = 0;
            for (var i = 0; i < bitCount; i++)
            {
                value = (value << 1) | ReadBit();
            }

            return value;
        }

        public int ReadBit()
        {
            if (_bitsRemaining == 0)
            {
                FillByte();
            }

            _bitsRemaining--;
            return (_currentByte >> _bitsRemaining) & 1;
        }

        private void FillByte()
        {
            if (_position >= _bytes.Length)
            {
                throw new DicomCodecException("JPEG-LS Golomb code exceeds the input buffer.");
            }

            _currentByte = _bytes[_position++];
            _bitsRemaining = _previousByteWasFF ? 7 : 8;
            _previousByteWasFF = _currentByte == 0xFF;
        }
    }
}
