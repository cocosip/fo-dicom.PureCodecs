using FellowOakDicom.PureCodecs.JpegLs.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsScanCodecTests
{
    [Fact]
    public void Regular_mode_scan_round_trip_reconstructs_8_bit_samples()
    {
        var samples = new[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 };
        var codec = new JpegLsScanCodec(width: 4, height: 3, componentCount: 1, bitsPerSample: 8, nearLossless: 0);

        var encoded = codec.Encode(samples);
        var decoded = codec.Decode(encoded);

        Assert.Equal(samples, decoded);
    }

    [Fact]
    public void Run_mode_scan_round_trip_reconstructs_flat_8_bit_samples()
    {
        var samples = new[] { 7, 7, 7, 7, 7, 7, 9, 9, 9, 9, 9, 9 };
        var codec = new JpegLsScanCodec(width: 6, height: 2, componentCount: 1, bitsPerSample: 8, nearLossless: 0);

        var encoded = codec.Encode(samples);
        var decoded = codec.Decode(encoded);

        Assert.Equal(samples, decoded);
    }

    [Fact]
    public void Sample_interleaved_run_mode_round_trip_reconstructs_repeated_interruptions()
    {
        var samples = new[]
        {
            10, 20, 30, 10, 20, 30, 10, 20, 30, 12, 22, 32,
            12, 22, 32, 12, 22, 32, 12, 22, 32, 14, 24, 34,
            14, 24, 34, 14, 24, 34, 14, 24, 34, 16, 26, 36,
        };
        var codec = new JpegLsScanCodec(
            width: 4,
            height: 3,
            componentCount: 3,
            bitsPerSample: 8,
            nearLossless: 0,
            interleaveMode: JpegLsInterleaveMode.Sample);

        var encoded = codec.Encode(samples);
        var decoded = codec.Decode(encoded);

        Assert.Equal(samples, decoded);
    }

    [Fact]
    public void Near_lossless_scan_round_trip_reconstructs_within_allowed_error()
    {
        var samples = new[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 };
        var codec = new JpegLsScanCodec(width: 4, height: 3, componentCount: 1, bitsPerSample: 8, nearLossless: 2);

        var encoded = codec.Encode(samples);
        var decoded = codec.Decode(encoded);

        for (var index = 0; index < samples.Length; index++)
        {
            Assert.True(
                JpegLsNearLossless.IsWithinTolerance(samples[index], decoded[index], allowedError: 2),
                $"Sample {index} differed by more than the allowed JPEG-LS NEAR value.");
        }
    }
}
