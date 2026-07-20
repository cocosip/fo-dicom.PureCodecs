using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegEntropyTests
{
    [Fact]
    public void Bit_reader_reads_bits_most_significant_bit_first()
    {
        var reader = new JpegEntropyBitReader(new byte[] { 0b1010_0101, 0b1100_0000 });

        Assert.Equal(0b101, reader.ReadBits(3));
        Assert.Equal(0b00101, reader.ReadBits(5));
        Assert.Equal(0b11, reader.ReadBits(2));
    }

    [Fact]
    public void Bit_reader_unstuffs_ff_zero_sequence()
    {
        var reader = new JpegEntropyBitReader(new byte[] { 0xFF, 0x00, 0x80 });

        Assert.Equal(0xFF, reader.ReadBits(8));
        Assert.Equal(1, reader.ReadBit());
    }

    [Fact]
    public void Bit_reader_stops_before_restart_marker()
    {
        var reader = new JpegEntropyBitReader(new byte[] { 0b1010_0000, 0xFF, JpegMarker.RST0 });

        Assert.Equal(0b1010, reader.ReadBits(4));
        Assert.Equal(JpegMarker.RST0, reader.ReadRestartMarker());
        Assert.True(reader.EndOfData);
    }

    [Fact]
    public void Bit_reader_rejects_unexpected_marker_inside_entropy_data()
    {
        var reader = new JpegEntropyBitReader(new byte[] { 0xFF, JpegMarker.EOI });

        var exception = Assert.Throws<DicomCodecException>(() => reader.ReadBit());

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("marker", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bit_writer_writes_bits_most_significant_bit_first()
    {
        var writer = new JpegEntropyBitWriter();

        writer.WriteBits(0b101, 3);
        writer.WriteBits(0b00101, 5);
        writer.WriteBits(0b11, 2);

        Assert.Equal(new byte[] { 0b1010_0101, 0xff, 0x00 }, writer.ToArray());
    }

    [Fact]
    public void Bit_writer_stuffs_zero_after_ff_byte()
    {
        var writer = new JpegEntropyBitWriter();

        writer.WriteBits(0xFF, 8);

        Assert.Equal(new byte[] { 0xFF, 0x00 }, writer.ToArray());
    }

    [Fact]
    public void Huffman_decode_table_decodes_canonical_codes()
    {
        var table = JpegHuffmanTable.Build(new byte[] { 0, 2, 1 }, new byte[] { 10, 11, 12 });
        var reader = new JpegEntropyBitReader(new byte[] { 0b0001_1000 });

        Assert.Equal(10, table.Decode(reader));
        Assert.Equal(11, table.Decode(reader));
        Assert.Equal(12, table.Decode(reader));
    }

    [Fact]
    public void Huffman_encode_table_writes_canonical_codes()
    {
        var table = JpegHuffmanTable.Build(new byte[] { 0, 2, 1 }, new byte[] { 10, 11, 12 });
        var writer = new JpegEntropyBitWriter();

        table.Encode(writer, 10);
        table.Encode(writer, 11);
        table.Encode(writer, 12);

        Assert.Equal(new byte[] { 0b0001_1001 }, writer.ToArray());
    }
}
