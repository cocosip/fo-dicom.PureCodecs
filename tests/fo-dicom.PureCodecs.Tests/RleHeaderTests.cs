using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Rle.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class RleHeaderTests
{
    [Fact]
    public void Parse_reads_valid_64_byte_header()
    {
        var bytes = CreateHeader(2, 64, 70);

        var header = RleHeader.Parse(bytes);

        Assert.Equal(2, header.SegmentCount);
        Assert.Equal(new[] { 64, 70 }, header.SegmentOffsets);
    }

    [Fact]
    public void Parse_rejects_frame_shorter_than_64_bytes()
    {
        var exception = Assert.Throws<DicomCodecException>(() => RleHeader.Parse(new byte[63]));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("64", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void Parse_rejects_unsupported_segment_count(int segmentCount)
    {
        var bytes = CreateHeader(segmentCount, 64);

        var exception = Assert.Throws<DicomCodecException>(() => RleHeader.Parse(bytes));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("segment count", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_rejects_non_increasing_segment_offsets()
    {
        var bytes = CreateHeader(2, 64, 64);

        var exception = Assert.Throws<DicomCodecException>(() => RleHeader.Parse(bytes));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("offset", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToBytes_writes_segment_count_and_offsets_in_little_endian_order()
    {
        var header = new RleHeader(new[] { 64, 0x01020304 });

        var bytes = header.ToBytes();

        Assert.Equal(64, bytes.Length);
        Assert.Equal(new byte[] { 2, 0, 0, 0 }, bytes[..4]);
        Assert.Equal(new byte[] { 64, 0, 0, 0 }, bytes[4..8]);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, bytes[8..12]);
    }

    private static byte[] CreateHeader(int segmentCount, params int[] offsets)
    {
        var frameLength = 64;
        foreach (var offset in offsets)
        {
            frameLength = Math.Max(frameLength, offset + 1);
        }

        var bytes = new byte[frameLength];
        WriteInt32LittleEndian(bytes, 0, segmentCount);

        for (var index = 0; index < offsets.Length; index++)
        {
            WriteInt32LittleEndian(bytes, 4 + index * 4, offsets[index]);
        }

        return bytes;
    }

    private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
