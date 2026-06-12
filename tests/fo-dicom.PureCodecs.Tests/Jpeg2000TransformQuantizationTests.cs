using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000TransformQuantizationTests
{
    [Theory]
    [InlineData(0, 8, false, -128)]
    [InlineData(128, 8, false, 0)]
    [InlineData(255, 8, false, 127)]
    [InlineData(-7, 12, true, -7)]
    public void Dc_level_shift_maps_unsigned_samples_around_zero_and_preserves_signed_samples(
        int sample,
        int precision,
        bool signed,
        int expected)
    {
        var shifted = Jpeg2000LevelShift.Forward(sample, precision, signed);
        var restored = Jpeg2000LevelShift.Inverse(shifted, precision, signed);

        Assert.Equal(expected, shifted);
        Assert.Equal(sample, restored);
    }

    [Theory]
    [InlineData(8, false, 0x07)]
    [InlineData(16, true, 0x8F)]
    public void Ssiz_precision_and_sign_map_to_dicom_pixel_metadata(int bitsStored, bool signed, int expectedSsiz)
    {
        var ssiz = Jpeg2000SamplePrecision.ToSsiz(bitsStored, signed);
        var precision = Jpeg2000SamplePrecision.FromSsiz((byte)ssiz);

        Assert.Equal(expectedSsiz, ssiz);
        Assert.Equal(bitsStored, precision.BitsStored);
        Assert.Equal(signed, precision.IsSigned);
        Assert.Equal(signed ? 1 : 0, precision.PixelRepresentation);
    }

    [Theory]
    [InlineData(8, 8, 0)]
    [InlineData(16, 12, 1)]
    public void Dicom_pixel_metadata_validation_accepts_supported_depths(int bitsAllocated, int bitsStored, int pixelRepresentation)
    {
        var precision = Jpeg2000SamplePrecision.ValidateDicomPixelMetadata(
            bitsAllocated,
            bitsStored,
            pixelRepresentation);

        Assert.Equal(bitsStored, precision.BitsStored);
        Assert.Equal(pixelRepresentation == 1, precision.IsSigned);
    }

    [Theory]
    [InlineData(12, 16, 0)]
    [InlineData(16, 17, 0)]
    [InlineData(16, 12, 2)]
    public void Dicom_pixel_metadata_validation_rejects_unsupported_depths(
        int bitsAllocated,
        int bitsStored,
        int pixelRepresentation)
    {
        var exception = Assert.Throws<DicomCodecException>(() =>
            Jpeg2000SamplePrecision.ValidateDicomPixelMetadata(
                bitsAllocated,
                bitsStored,
                pixelRepresentation));

        Assert.Contains("JPEG 2000", exception.Message);
        Assert.Contains("pixel", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowMct_controls_reversible_color_transform_for_rgb_samples()
    {
        var rgb = new Jpeg2000IntRgb(120, 80, 20);

        var disabled = Jpeg2000ComponentTransform.ForwardReversible(rgb, allowMultipleComponentTransform: false);
        var transformed = Jpeg2000ComponentTransform.ForwardReversible(rgb, allowMultipleComponentTransform: true);
        var restored = Jpeg2000ComponentTransform.InverseReversible(transformed, allowMultipleComponentTransform: true);

        Assert.Equal(rgb, disabled);
        Assert.Equal(new Jpeg2000IntRgb(75, -60, 40), transformed);
        Assert.Equal(rgb, restored);
    }

    [Fact]
    public void Irreversible_color_transform_round_trips_rgb_samples_with_tolerance_when_enabled()
    {
        var rgb = new Jpeg2000FloatRgb(120, 80, 20);

        var disabled = Jpeg2000ComponentTransform.ForwardIrreversible(rgb, allowMultipleComponentTransform: false);
        var transformed = Jpeg2000ComponentTransform.ForwardIrreversible(rgb, allowMultipleComponentTransform: true);
        var restored = Jpeg2000ComponentTransform.InverseIrreversible(transformed, allowMultipleComponentTransform: true);

        Assert.Equal(rgb.R, disabled.R, tolerance: 0.0001);
        Assert.NotEqual(rgb.R, transformed.R);
        Assert.Equal(rgb.R, restored.R, tolerance: 0.0001);
        Assert.Equal(rgb.G, restored.G, tolerance: 0.0001);
        Assert.Equal(rgb.B, restored.B, tolerance: 0.0001);
    }

    [Fact]
    public void Reversible_wavelet_transform_round_trips_integer_samples_exactly()
    {
        var samples = CreateIntPlane(8, 6);

        var coefficients = Jpeg2000ReversibleWaveletTransform.Forward2D(samples, width: 8, height: 6, levels: 2);
        var restored = Jpeg2000ReversibleWaveletTransform.Inverse2D(coefficients, width: 8, height: 6, levels: 2);

        Assert.Equal(samples, restored);
    }

    [Fact]
    public void Irreversible_wavelet_transform_round_trips_float_samples_with_tolerance()
    {
        var samples = CreateFloatPlane(8, 6);

        var coefficients = Jpeg2000IrreversibleWaveletTransform.Forward2D(samples, width: 8, height: 6, levels: 2);
        var restored = Jpeg2000IrreversibleWaveletTransform.Inverse2D(coefficients, width: 8, height: 6, levels: 2);

        for (var index = 0; index < samples.Length; index++)
        {
            Assert.Equal(samples[index], restored[index], tolerance: 0.0001);
        }
    }

    [Theory]
    [InlineData(8, Jpeg2000SubbandKind.LL, 2, 10)]
    [InlineData(8, Jpeg2000SubbandKind.HL, 2, 11)]
    [InlineData(8, Jpeg2000SubbandKind.HH, 2, 12)]
    public void Guard_bits_contribute_to_effective_wavelet_coefficient_depth(
        int precision,
        Jpeg2000SubbandKind subband,
        int guardBits,
        int expectedDepth)
    {
        Assert.Equal(expectedDepth, Jpeg2000BitPlaneMath.EffectiveBitDepth(precision, subband, guardBits));
    }

    [Theory]
    [InlineData(new[] { 0, 0, 0 }, 8, Jpeg2000SubbandKind.LL, 2, 10)]
    [InlineData(new[] { 0, 1, -1 }, 8, Jpeg2000SubbandKind.LL, 2, 9)]
    [InlineData(new[] { 0, 32, -7 }, 8, Jpeg2000SubbandKind.HL, 2, 5)]
    public void Zero_bit_plane_calculation_tracks_coefficient_magnitude(
        int[] coefficients,
        int precision,
        Jpeg2000SubbandKind subband,
        int guardBits,
        int expectedZeroBitPlanes)
    {
        Assert.Equal(expectedZeroBitPlanes, Jpeg2000BitPlaneMath.ZeroBitPlanes(coefficients, precision, subband, guardBits));
    }

    [Fact]
    public void No_quantization_path_preserves_lossless_five_three_coefficients()
    {
        var qcd = Jpeg2000QuantizationDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCD,
            new byte[] { 0x00, 0x08, 0x09, 0x0A }));

        var model = Jpeg2000QuantizationModel.From(qcd, precision: 8, decompositionLevels: 1);

        Assert.Equal(Jpeg2000QuantizationStyle.None, model.Style);
        Assert.Equal(1.0, model.GetStepSize(2), tolerance: 0.0);
        Assert.Equal(-37, model.InverseQuantize(-37, subbandIndex: 2), tolerance: 0.0);
    }

    [Fact]
    public void Scalar_derived_quantization_expands_one_base_step_to_all_subbands()
    {
        var qcd = Jpeg2000QuantizationDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCD,
            new byte[] { 0x21, 0x58, 0x00 }));

        var model = Jpeg2000QuantizationModel.From(qcd, precision: 8, decompositionLevels: 2);

        Assert.Equal(Jpeg2000QuantizationStyle.ScalarDerived, model.Style);
        Assert.Equal(7, model.SubbandCount);
        Assert.Equal(model.GetStepSize(0) / 2.0, model.GetStepSize(3), tolerance: 0.0001);
        Assert.Equal(model.GetStepSize(0) / 4.0, model.GetStepSize(6), tolerance: 0.0001);
    }

    [Fact]
    public void Scalar_expounded_quantization_uses_explicit_lossy_subband_steps()
    {
        var qcd = Jpeg2000QuantizationDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCD,
            new byte[] { 0x42, 0x50, 0x00, 0x58, 0x00, 0x60, 0x00, 0x68, 0x00 }));

        var model = Jpeg2000QuantizationModel.From(qcd, precision: 8, decompositionLevels: 1);

        Assert.Equal(Jpeg2000QuantizationStyle.ScalarExpounded, model.Style);
        Assert.Equal(4, model.SubbandCount);
        Assert.True(model.GetStepSize(0) > model.GetStepSize(1));
        Assert.Equal(5 * model.GetStepSize(3), model.InverseQuantize(5, subbandIndex: 3), tolerance: 0.0001);
    }

    private static int[] CreateIntPlane(int width, int height)
    {
        var samples = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[y * width + x] = (x * 11) - (y * 7) + ((x * y) % 5);
            }
        }

        return samples;
    }

    private static double[] CreateFloatPlane(int width, int height)
    {
        var samples = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[y * width + x] = (x * 0.75) - (y * 1.25) + System.Math.Sin(x + y);
            }
        }

        return samples;
    }
}
