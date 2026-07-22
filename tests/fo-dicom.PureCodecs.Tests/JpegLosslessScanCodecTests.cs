using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLosslessScanCodecTests
{
    [Fact]
    public void Lossless_scan_round_trip_preserves_8_bit_samples()
    {
        var samples = new[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 };

        AssertRoundTrip(samples, width: 4, height: 3, samplePrecision: 8, selectionValue: 1);
    }

    [Fact]
    public void Lossless_scan_round_trip_preserves_12_bit_samples()
    {
        var samples = new[] { 100, 110, 95, 130, 4095, 4080, 3000, 2800, 2048, 2000, 1900, 1800 };

        AssertRoundTrip(samples, width: 3, height: 4, samplePrecision: 12, selectionValue: 4);
    }

    [Fact]
    public void Lossless_scan_round_trip_preserves_16_bit_samples()
    {
        var samples = new[] { 1000, 1010, 65000, 65010, 32000, 32001, 1, 0, 40000, 41000, 42000, 43000 };

        AssertRoundTrip(samples, width: 6, height: 2, samplePrecision: 16, selectionValue: 7);
    }

    [Fact]
    public void Lossless_scan_decode_populates_supplied_workspace()
    {
        var samples = new[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 };
        var codec = JpegLosslessScanCodec.CreateDefault();
        var encoded = codec.Encode(samples, width: 4, height: 3, samplePrecision: 8, selectionValue: 1);
        var workspace = new int[samples.Length + 4];

        var decoded = codec.DecodeInterleaved(
            encoded,
            width: 4,
            height: 3,
            componentCount: 1,
            samplePrecision: 8,
            selectionValue: 1,
            workspace);

        Assert.Same(workspace, decoded);
        Assert.Equal(samples, decoded.Take(samples.Length));
    }

    [Fact]
    public void Lossless_scan_rejects_sample_outside_precision_range()
    {
        var codec = JpegLosslessScanCodec.CreateDefault();

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Encode(new[] { 4096 }, width: 1, height: 1, samplePrecision: 12, selectionValue: 1));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("sample", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRoundTrip(int[] samples, int width, int height, int samplePrecision, int selectionValue)
    {
        var codec = JpegLosslessScanCodec.CreateDefault();

        var encoded = codec.Encode(samples, width, height, samplePrecision, selectionValue);
        var decoded = codec.Decode(encoded, width, height, samplePrecision, selectionValue);

        Assert.Equal(samples, decoded);
    }
}
