using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegSequentialDctCodecTests
{
    [Fact]
    public void Baseline_sequential_round_trip_preserves_8_bit_samples_with_tolerance()
    {
        var samples = CreateGradient(width: 16, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var encoded = codec.Encode(samples, width: 16, height: 16, quality: 95);
        var decoded = codec.Decode(encoded, expectedWidth: 16, expectedHeight: 16);

        AssertWithinTolerance(samples, decoded, tolerance: 20);
    }

    [Fact]
    public void Extended_sequential_round_trip_preserves_8_bit_samples_with_tolerance()
    {
        var samples = CreateGradient(width: 8, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Extended);

        var encoded = codec.Encode(samples, width: 8, height: 16, quality: 95);
        var decoded = codec.Decode(encoded, expectedWidth: 8, expectedHeight: 16);

        AssertWithinTolerance(samples, decoded, tolerance: 20);
    }

    [Fact]
    public void Baseline_decoder_rejects_missing_quantization_table()
    {
        var bytes = new byte[]
        {
            0xFF, JpegMarker.SOI,
            0xFF, JpegMarker.SOF0, 0x00, 0x0B, 8, 0, 8, 0, 8, 1, 1, 0x11, 0,
            0xFF, JpegMarker.SOS, 0x00, 0x08, 1, 1, 0, 0, 63, 0,
            0xFF, JpegMarker.EOI,
        };
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Decode(bytes, expectedWidth: 8, expectedHeight: 8));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("quantization", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateGradient(int width, int height)
    {
        var samples = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[y * width + x] = (byte)((x * 13 + y * 9 + 17) % 256);
            }
        }

        return samples;
    }

    private static void AssertWithinTolerance(byte[] expected, byte[] actual, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var difference = System.Math.Abs(expected[index] - actual[index]);
            Assert.True(difference <= tolerance, $"Sample {index} differed by {difference}, tolerance {tolerance}.");
        }
    }
}
