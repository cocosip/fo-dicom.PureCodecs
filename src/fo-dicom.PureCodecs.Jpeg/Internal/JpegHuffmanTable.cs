using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegHuffmanTable
    {
        private readonly Dictionary<int, int> _decodeValues;
        private readonly Dictionary<int, HuffmanCode> _encodeCodes;

        private JpegHuffmanTable(Dictionary<int, int> decodeValues, Dictionary<int, HuffmanCode> encodeCodes)
        {
            _decodeValues = decodeValues;
            _encodeCodes = encodeCodes;
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

            return new JpegHuffmanTable(decodeValues, encodeCodes);
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
