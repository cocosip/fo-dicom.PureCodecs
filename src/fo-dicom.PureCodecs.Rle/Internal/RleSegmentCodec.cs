using System;
using System.Buffers;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Rle.Internal
{
    internal static class RleSegmentCodec
    {
        private const int MaximumRunLength = 128;

        public static byte[] Decode(byte[] encoded, int expectedLength)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (expectedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedLength));
            }

            var output = new byte[expectedLength];
            var inputIndex = 0;
            var outputIndex = 0;

            while (inputIndex < encoded.Length)
            {
                var control = unchecked((sbyte)encoded[inputIndex++]);
                if (control >= 0)
                {
                    var literalLength = control + 1;
                    if (inputIndex + literalLength > encoded.Length)
                    {
                        throw CreateException("RLE Lossless literal run exceeds segment input.");
                    }

                    if (outputIndex + literalLength > output.Length)
                    {
                        throw CreateException("RLE Lossless literal run exceeds segment output.");
                    }

                    Buffer.BlockCopy(encoded, inputIndex, output, outputIndex, literalLength);
                    inputIndex += literalLength;
                    outputIndex += literalLength;
                    continue;
                }

                if (control == -128)
                {
                    continue;
                }

                var repeatLength = 1 - control;
                if (inputIndex >= encoded.Length)
                {
                    throw CreateException("RLE Lossless repeat run is missing its repeated byte.");
                }

                if (outputIndex + repeatLength > output.Length)
                {
                    throw CreateException("RLE Lossless repeat run exceeds segment output.");
                }

                var repeated = encoded[inputIndex++];
                for (var index = 0; index < repeatLength; index++)
                {
                    output[outputIndex++] = repeated;
                }
            }

            if (outputIndex != expectedLength)
            {
                throw CreateException($"RLE Lossless decoded segment length {outputIndex} does not match expected length {expectedLength}.");
            }

            return output;
        }

        public static byte[] Encode(byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var encoded = ArrayPool<byte>.Shared.Rent(GetMaximumEncodedLength(source.Length));
            try
            {
                var encodedLength = Encode(source, encoded);
                var result = new byte[encodedLength];
                Buffer.BlockCopy(encoded, 0, result, 0, encodedLength);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(encoded);
            }
        }

        public static int Encode(byte[] source, byte[] destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length < GetMaximumEncodedLength(source.Length))
            {
                throw new ArgumentException("RLE Lossless destination buffer is shorter than the maximum encoded length.", nameof(destination));
            }

            var literalBuffer = new byte[132];
            var literalLength = 0;
            var previous = -1;
            var repeatLength = 0;
            var outputIndex = 0;

            for (var index = 0; index < source.Length; index++)
            {
                var value = source[index];
                if (value == previous)
                {
                    repeatLength++;
                    if (repeatLength > 2 && literalLength > 0)
                    {
                        FlushLiterals(destination, ref outputIndex, literalBuffer, ref literalLength);
                    }
                    else if (repeatLength > MaximumRunLength)
                    {
                        WriteRepeatRuns(destination, ref outputIndex, (byte)previous, MaximumRunLength);
                        repeatLength -= MaximumRunLength;
                    }

                    continue;
                }

                switch (repeatLength)
                {
                    case 0:
                        break;
                    case 1:
                        literalBuffer[literalLength++] = (byte)previous;
                        break;
                    case 2:
                        literalBuffer[literalLength++] = (byte)previous;
                        literalBuffer[literalLength++] = (byte)previous;
                        break;
                    default:
                        WriteRepeatRuns(destination, ref outputIndex, (byte)previous, repeatLength);
                        break;
                }

                FlushOversizedLiterals(destination, ref outputIndex, literalBuffer, ref literalLength);
                previous = value;
                repeatLength = 1;
            }

            if (repeatLength < 2)
            {
                while (repeatLength > 0)
                {
                    literalBuffer[literalLength++] = (byte)previous;
                    repeatLength--;
                }
            }

            FlushLiterals(destination, ref outputIndex, literalBuffer, ref literalLength);
            if (repeatLength >= 2)
            {
                WriteRepeatRuns(destination, ref outputIndex, (byte)previous, repeatLength);
            }

            return outputIndex;
        }

        private static int GetMaximumEncodedLength(int sourceLength)
        {
            var literalRunCount = sourceLength / MaximumRunLength;
            if (sourceLength % MaximumRunLength != 0)
            {
                literalRunCount++;
            }

            return checked(sourceLength + literalRunCount);
        }

        private static void FlushOversizedLiterals(byte[] destination, ref int outputIndex, byte[] literalBuffer, ref int literalLength)
        {
            while (literalLength > MaximumRunLength)
            {
                WriteLiteral(destination, ref outputIndex, literalBuffer, MaximumRunLength);
                Buffer.BlockCopy(literalBuffer, MaximumRunLength, literalBuffer, 0, literalLength - MaximumRunLength);
                literalLength -= MaximumRunLength;
            }
        }

        private static void WriteRepeatRuns(byte[] destination, ref int outputIndex, byte value, int count)
        {
            while (count > 0)
            {
                var runLength = Math.Min(MaximumRunLength, count);
                destination[outputIndex++] = unchecked((byte)(1 - runLength));
                destination[outputIndex++] = value;
                count -= runLength;
            }
        }

        private static void FlushLiterals(byte[] destination, ref int outputIndex, byte[] literalBuffer, ref int literalLength)
        {
            while (literalLength > 0)
            {
                var count = Math.Min(MaximumRunLength, literalLength);
                WriteLiteral(destination, ref outputIndex, literalBuffer, count);
                Buffer.BlockCopy(literalBuffer, count, literalBuffer, 0, literalLength - count);
                literalLength -= count;
            }
        }

        private static void WriteLiteral(byte[] destination, ref int outputIndex, byte[] literalBuffer, int count)
        {
            destination[outputIndex++] = (byte)(count - 1);
            Buffer.BlockCopy(literalBuffer, 0, destination, outputIndex, count);
            outputIndex += count;
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
