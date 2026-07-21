using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegDctPrimitiveTests
{
    [Fact]
    public void Block_stores_8_by_8_coefficients()
    {
        var block = new JpegBlock8x8();

        block[7, 6] = 42;

        Assert.Equal(64, JpegBlock8x8.CoefficientCount);
        Assert.Equal(42, block[7, 6]);
        Assert.Equal(42, block[62]);
    }

    [Fact]
    public void Block_rejects_coordinates_outside_8_by_8_range()
    {
        var block = new JpegBlock8x8();

        var exception = Assert.Throws<DicomCodecException>(() => block[8, 0] = 1);

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("block", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quantization_table_quantizes_and_dequantizes_coefficients()
    {
        var divisors = new int[64];
        for (var index = 0; index < divisors.Length; index++)
        {
            divisors[index] = index + 1;
        }

        var table = new JpegQuantizationTable(divisors);
        var block = new JpegBlock8x8();
        block[0] = 16;
        block[7] = -24;

        var quantized = table.Quantize(block);
        var dequantized = table.Dequantize(quantized);

        Assert.Equal(16, quantized[0]);
        Assert.Equal(-3, quantized[7]);
        Assert.Equal(16, dequantized[0]);
        Assert.Equal(-24, dequantized[7]);
    }

    [Fact]
    public void Quantization_table_rejects_zero_divisor()
    {
        var divisors = new int[64];
        divisors[0] = 1;

        var exception = Assert.Throws<DicomCodecException>(() => new JpegQuantizationTable(divisors));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("quantization", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zigzag_round_trip_preserves_coefficients()
    {
        var block = new JpegBlock8x8();
        for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
        {
            block[index] = index;
        }

        var zigzag = JpegZigZag.ToZigZag(block);
        var restored = JpegZigZag.FromZigZag(zigzag);

        Assert.Equal(0, zigzag[0]);
        Assert.Equal(1, zigzag[1]);
        Assert.Equal(8, zigzag[2]);
        Assert.Equal(63, zigzag[63]);
        Assert.Equal(block.ToArray(), restored.ToArray());
    }

    [Fact]
    public void Forward_dct_constant_block_has_only_dc_coefficient()
    {
        var samples = new JpegBlock8x8();
        for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
        {
            samples[index] = 10;
        }

        var coefficients = JpegDct.Forward(samples);

        Assert.Equal(80, coefficients[0], tolerance: 0.0001);
        for (var index = 1; index < JpegBlock8x8.CoefficientCount; index++)
        {
            Assert.Equal(0, coefficients[index], tolerance: 0.0001);
        }
    }

    [Fact]
    public void Inverse_dct_reconstructs_known_block_with_tolerance()
    {
        var samples = new JpegBlock8x8();
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                samples[y, x] = y * 8 + x - 32;
            }
        }

        var coefficients = JpegDct.Forward(samples);
        var reconstructed = JpegDct.Inverse(coefficients);

        for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
        {
            Assert.Equal(samples[index], reconstructed[index], tolerance: 0.0001);
        }
    }

    [Fact]
    public void Inverse_dct_preserves_reference_dc_and_ac_samples()
    {
        var coefficients = new JpegBlock8x8();
        coefficients[0, 0] = 80;
        coefficients[0, 1] = 40;

        var reconstructed = JpegDct.Inverse(coefficients);
        var expectedRow = new[]
        {
            16.935199226610738,
            15.879378012096794,
            13.928474791935511,
            11.379496896414716,
            8.6205031035852855,
            6.0715252080644913,
            4.1206219879032053,
            3.0648007733892628,
        };

        for (var y = 0; y < JpegBlock8x8.Size; y++)
        {
            for (var x = 0; x < JpegBlock8x8.Size; x++)
            {
                Assert.Equal(expectedRow[x], reconstructed[y, x], tolerance: 0.0000000001);
            }
        }
    }
}
