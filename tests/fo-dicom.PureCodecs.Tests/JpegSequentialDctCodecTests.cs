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
    public void Baseline_rgb_encoding_generates_image_specific_compact_huffman_tables()
    {
        var uniform = new byte[16 * 16 * 3];
        var detailed = CreateRgbGradient(width: 16, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var uniformJpeg = codec.Encode(uniform, width: 16, height: 16, componentCount: 3, quality: 90);
        var detailedJpeg = codec.Encode(detailed, width: 16, height: 16, componentCount: 3, quality: 90);

        var uniformDhtSize = GetDhtPayloadSize(uniformJpeg);
        var detailedDhtSize = GetDhtPayloadSize(detailedJpeg);
        Assert.True(uniformDhtSize < 544);
        Assert.True(detailedDhtSize < 544);
        Assert.NotEqual(uniformDhtSize, detailedDhtSize);
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

    private static byte[] CreateRgbGradient(int width, int height)
    {
        var samples = new byte[width * height * 3];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            samples[pixel * 3] = (byte)((pixel * 17 + 31) % 256);
            samples[pixel * 3 + 1] = (byte)((pixel * 29 + 71) % 256);
            samples[pixel * 3 + 2] = (byte)((pixel * 43 + 113) % 256);
        }

        return samples;
    }

    private static int GetDhtPayloadSize(byte[] jpeg)
    {
        for (var index = 0; index + 3 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xff && jpeg[index + 1] == 0xc4)
            {
                return (jpeg[index + 2] << 8) | jpeg[index + 3];
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain a DHT marker.");
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
