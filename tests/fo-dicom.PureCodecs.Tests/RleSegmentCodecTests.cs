using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Rle.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class RleSegmentCodecTests
{
    [Fact]
    public void Decode_expands_literal_run()
    {
        var decoded = RleSegmentCodec.Decode(new byte[] { 2, 10, 11, 12 }, expectedLength: 3);

        Assert.Equal(new byte[] { 10, 11, 12 }, decoded);
    }

    [Fact]
    public void Decode_expands_repeat_run()
    {
        var decoded = RleSegmentCodec.Decode(new byte[] { 254, 7 }, expectedLength: 3);

        Assert.Equal(new byte[] { 7, 7, 7 }, decoded);
    }

    [Fact]
    public void Decode_expands_mixed_literal_and_repeat_runs()
    {
        var decoded = RleSegmentCodec.Decode(new byte[] { 1, 1, 2, 253, 9, 0, 3 }, expectedLength: 7);

        Assert.Equal(new byte[] { 1, 2, 9, 9, 9, 9, 3 }, decoded);
    }

    [Fact]
    public void Decode_rejects_literal_run_that_exceeds_input()
    {
        var exception = Assert.Throws<DicomCodecException>(
            () => RleSegmentCodec.Decode(new byte[] { 2, 10, 11 }, expectedLength: 3));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("literal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_rejects_repeat_run_that_exceeds_output()
    {
        var exception = Assert.Throws<DicomCodecException>(
            () => RleSegmentCodec.Decode(new byte[] { 254, 7 }, expectedLength: 2));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("repeat", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encode_writes_literal_run()
    {
        var encoded = RleSegmentCodec.Encode(new byte[] { 10, 11, 12 });

        Assert.Equal(new byte[] { 2, 10, 11, 12 }, encoded);
    }

    [Fact]
    public void Encode_writes_repeat_run()
    {
        var encoded = RleSegmentCodec.Encode(new byte[] { 7, 7, 7 });

        Assert.Equal(new byte[] { 254, 7 }, encoded);
    }

    [Fact]
    public void Encode_writes_mixed_literal_and_repeat_runs()
    {
        var encoded = RleSegmentCodec.Encode(new byte[] { 1, 2, 9, 9, 9, 9, 3 });

        Assert.Equal(new byte[] { 1, 1, 2, 253, 9, 0, 3 }, encoded);
    }

    [Fact]
    public void Encode_writes_exact_packetization_into_preallocated_buffer()
    {
        var source = new byte[133];
        for (var index = 0; index < 128; index++)
        {
            source[index] = (byte)index;
        }

        source[128] = 200;
        source[129] = 200;
        source[130] = 200;
        source[131] = 5;
        source[132] = 6;

        var expected = new byte[134];
        expected[0] = 127;
        for (var index = 0; index < 128; index++)
        {
            expected[index + 1] = (byte)index;
        }

        expected[129] = 254;
        expected[130] = 200;
        expected[131] = 1;
        expected[132] = 5;
        expected[133] = 6;
        var destination = new byte[source.Length + ((source.Length + 127) / 128)];

        var written = RleSegmentCodec.Encode(source, destination);

        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, destination.Take(written));
    }
}
