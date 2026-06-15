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
