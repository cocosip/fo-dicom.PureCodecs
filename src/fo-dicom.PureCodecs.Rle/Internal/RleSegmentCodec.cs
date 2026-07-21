using System;
using System.Collections.Generic;
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

            var encoded = new List<byte>(source.Length);
            var literalBuffer = new byte[132];
            var literalLength = 0;
            var previous = -1;
            var repeatLength = 0;

            for (var index = 0; index < source.Length; index++)
            {
                var value = source[index];
                if (value == previous)
                {
                    repeatLength++;
                    if (repeatLength > 2 && literalLength > 0)
                    {
                        FlushLiterals(encoded, literalBuffer, ref literalLength);
                    }
                    else if (repeatLength > MaximumRunLength)
                    {
                        WriteRepeatRuns(encoded, (byte)previous, MaximumRunLength);
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
                        WriteRepeatRuns(encoded, (byte)previous, repeatLength);
                        break;
                }

                FlushOversizedLiterals(encoded, literalBuffer, ref literalLength);
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

            FlushLiterals(encoded, literalBuffer, ref literalLength);
            if (repeatLength >= 2)
            {
                WriteRepeatRuns(encoded, (byte)previous, repeatLength);
            }

            return encoded.ToArray();
        }

        private static void FlushOversizedLiterals(List<byte> encoded, byte[] literalBuffer, ref int literalLength)
        {
            while (literalLength > MaximumRunLength)
            {
                WriteLiteral(encoded, literalBuffer, MaximumRunLength);
                Buffer.BlockCopy(literalBuffer, MaximumRunLength, literalBuffer, 0, literalLength - MaximumRunLength);
                literalLength -= MaximumRunLength;
            }
        }

        private static void WriteRepeatRuns(List<byte> encoded, byte value, int count)
        {
            while (count > 0)
            {
                var runLength = Math.Min(MaximumRunLength, count);
                encoded.Add(unchecked((byte)(1 - runLength)));
                encoded.Add(value);
                count -= runLength;
            }
        }

        private static void FlushLiterals(List<byte> encoded, byte[] literalBuffer, ref int literalLength)
        {
            while (literalLength > 0)
            {
                var count = Math.Min(MaximumRunLength, literalLength);
                WriteLiteral(encoded, literalBuffer, count);
                Buffer.BlockCopy(literalBuffer, count, literalBuffer, 0, literalLength - count);
                literalLength -= count;
            }
        }

        private static void WriteLiteral(List<byte> encoded, byte[] literalBuffer, int count)
        {
            encoded.Add((byte)(count - 1));
            for (var index = 0; index < count; index++)
            {
                encoded.Add(literalBuffer[index]);
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
