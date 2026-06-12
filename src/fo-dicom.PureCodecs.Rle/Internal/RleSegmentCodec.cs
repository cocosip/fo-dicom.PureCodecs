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
            var index = 0;
            while (index < source.Length)
            {
                var repeatLength = CountRepeat(source, index);
                if (repeatLength >= 3)
                {
                    WriteRepeatRuns(encoded, source[index], repeatLength);
                    index += repeatLength;
                    continue;
                }

                var literalStart = index;
                index += repeatLength;
                while (index < source.Length)
                {
                    repeatLength = CountRepeat(source, index);
                    if (repeatLength >= 3)
                    {
                        break;
                    }

                    index += repeatLength;
                    if (index - literalStart >= MaximumRunLength)
                    {
                        break;
                    }
                }

                WriteLiteralRuns(encoded, source, literalStart, index - literalStart);
            }

            return encoded.ToArray();
        }

        private static int CountRepeat(byte[] source, int start)
        {
            var value = source[start];
            var count = 1;
            while (start + count < source.Length && source[start + count] == value && count < MaximumRunLength)
            {
                count++;
            }

            return count;
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

        private static void WriteLiteralRuns(List<byte> encoded, byte[] source, int start, int count)
        {
            while (count > 0)
            {
                var runLength = Math.Min(MaximumRunLength, count);
                encoded.Add((byte)(runLength - 1));
                for (var index = 0; index < runLength; index++)
                {
                    encoded.Add(source[start + index]);
                }

                start += runLength;
                count -= runLength;
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
