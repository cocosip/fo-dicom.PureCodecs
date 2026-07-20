using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegHuffmanTable
    {
        private readonly Dictionary<int, int> _decodeValues;
        private readonly Dictionary<int, HuffmanCode> _encodeCodes;
        private readonly byte[] _codeLengthCounts;
        private readonly byte[] _values;

        private JpegHuffmanTable(
            Dictionary<int, int> decodeValues,
            Dictionary<int, HuffmanCode> encodeCodes,
            byte[] codeLengthCounts,
            byte[] values)
        {
            _decodeValues = decodeValues;
            _encodeCodes = encodeCodes;
            _codeLengthCounts = codeLengthCounts;
            _values = values;
        }

        public static JpegHuffmanTable Build(byte[] codeLengthCounts, byte[] values)
        {
            if (codeLengthCounts == null)
            {
                throw new ArgumentNullException(nameof(codeLengthCounts));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var decodeValues = new Dictionary<int, int>();
            var encodeCodes = new Dictionary<int, HuffmanCode>();
            var valueIndex = 0;
            var code = 0;

            for (var lengthIndex = 0; lengthIndex < codeLengthCounts.Length; lengthIndex++)
            {
                var bitLength = lengthIndex + 1;
                var count = codeLengthCounts[lengthIndex];
                for (var index = 0; index < count; index++)
                {
                    if (valueIndex >= values.Length)
                    {
                        throw JpegMarkerReader.CreateException("JPEG Huffman table has fewer values than code lengths require.");
                    }

                    var value = values[valueIndex++];
                    decodeValues[Key(bitLength, code)] = value;
                    encodeCodes[value] = new HuffmanCode(code, bitLength);
                    code++;
                }

                code <<= 1;
            }

            if (valueIndex != values.Length)
            {
                throw JpegMarkerReader.CreateException("JPEG Huffman table has unused values.");
            }

            var countsCopy = new byte[codeLengthCounts.Length];
            Buffer.BlockCopy(codeLengthCounts, 0, countsCopy, 0, countsCopy.Length);
            var valuesCopy = new byte[values.Length];
            Buffer.BlockCopy(values, 0, valuesCopy, 0, valuesCopy.Length);
            return new JpegHuffmanTable(decodeValues, encodeCodes, countsCopy, valuesCopy);
        }

        public static JpegHuffmanTable CreateOptimal(int[] frequencies)
        {
            if (frequencies == null)
            {
                throw new ArgumentNullException(nameof(frequencies));
            }

            if (frequencies.Length != 256)
            {
                throw new ArgumentException("JPEG Huffman frequencies must contain 256 symbols.", nameof(frequencies));
            }

            var frequency = new long[257];
            var codeSizes = new int[257];
            var others = new int[257];
            for (var index = 0; index < others.Length; index++)
            {
                others[index] = -1;
            }

            for (var index = 0; index < frequencies.Length; index++)
            {
                frequency[index] = frequencies[index];
            }

            // The pseudo-symbol prevents an all-ones code from being emitted.
            frequency[256] = 1;
            while (true)
            {
                var first = FindLeastFrequentSymbol(frequency);
                var second = FindLeastFrequentSymbol(frequency, first);
                if (second < 0)
                {
                    break;
                }

                frequency[first] += frequency[second];
                frequency[second] = 0;
                var firstBranchEnd = IncrementCodeSize(codeSizes, others, first);
                others[firstBranchEnd] = second;
                IncrementCodeSize(codeSizes, others, second);
            }

            var lengthCounts = new int[257];
            for (var symbol = 0; symbol < 257; symbol++)
            {
                if (codeSizes[symbol] > 0)
                {
                    lengthCounts[codeSizes[symbol]]++;
                }
            }

            for (var length = lengthCounts.Length - 1; length > 16; length--)
            {
                while (lengthCounts[length] > 0)
                {
                    var shorterLength = length - 2;
                    while (shorterLength > 0 && lengthCounts[shorterLength] == 0)
                    {
                        shorterLength--;
                    }

                    if (shorterLength == 0)
                    {
                        throw new InvalidOperationException("Unable to limit JPEG Huffman code lengths.");
                    }

                    lengthCounts[length] -= 2;
                    lengthCounts[length - 1]++;
                    lengthCounts[shorterLength + 1] += 2;
                    lengthCounts[shorterLength]--;
                }
            }

            for (var length = 16; length > 0; length--)
            {
                if (lengthCounts[length] > 0)
                {
                    lengthCounts[length]--;
                    break;
                }
            }

            var counts = new byte[16];
            var valueCount = 0;
            for (var length = 1; length <= counts.Length; length++)
            {
                counts[length - 1] = checked((byte)lengthCounts[length]);
                valueCount += lengthCounts[length];
            }

            var values = new byte[valueCount];
            var valueIndex = 0;
            for (var codeSize = 1; codeSize < codeSizes.Length; codeSize++)
            {
                for (var symbol = 0; symbol < frequencies.Length; symbol++)
                {
                    if (codeSizes[symbol] == codeSize)
                    {
                        values[valueIndex++] = (byte)symbol;
                    }
                }
            }

            return Build(counts, values);
        }

        public byte[] CreateDhtPayload(int tableClass, int tableId)
        {
            if (tableClass < 0 || tableClass > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tableClass));
            }

            if (tableId < 0 || tableId > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(tableId));
            }

            var payload = new byte[1 + _codeLengthCounts.Length + _values.Length];
            payload[0] = (byte)((tableClass << 4) | tableId);
            Buffer.BlockCopy(_codeLengthCounts, 0, payload, 1, _codeLengthCounts.Length);
            Buffer.BlockCopy(_values, 0, payload, 1 + _codeLengthCounts.Length, _values.Length);
            return payload;
        }

        public int Decode(JpegEntropyBitReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var code = 0;
            for (var bitLength = 1; bitLength <= 16; bitLength++)
            {
                code = (code << 1) | reader.ReadBit();
                if (_decodeValues.TryGetValue(Key(bitLength, code), out var value))
                {
                    return value;
                }
            }

            throw JpegMarkerReader.CreateException("JPEG Huffman code was not found in the decode table.");
        }

        public void Encode(JpegEntropyBitWriter writer, int value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (!_encodeCodes.TryGetValue(value, out var code))
            {
                throw JpegMarkerReader.CreateException($"JPEG Huffman value {value} was not found in the encode table.");
            }

            writer.WriteBits(code.Code, code.BitLength);
        }

        private static int Key(int bitLength, int code)
        {
            return (bitLength << 16) | code;
        }

        private static int FindLeastFrequentSymbol(long[] frequencies, int excluded = -1)
        {
            var result = -1;
            for (var symbol = 0; symbol < frequencies.Length; symbol++)
            {
                if (symbol == excluded || frequencies[symbol] == 0)
                {
                    continue;
                }

                if (result < 0 || frequencies[symbol] <= frequencies[result])
                {
                    result = symbol;
                }
            }

            return result;
        }

        private static int IncrementCodeSize(int[] codeSizes, int[] others, int symbol)
        {
            codeSizes[symbol]++;
            while (others[symbol] >= 0)
            {
                symbol = others[symbol];
                codeSizes[symbol]++;
            }

            return symbol;
        }

        private readonly struct HuffmanCode
        {
            public HuffmanCode(int code, int bitLength)
            {
                Code = code;
                BitLength = bitLength;
            }

            public int Code { get; }

            public int BitLength { get; }
        }
    }
}
