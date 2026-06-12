using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegBaselineFixtureTests
{
    public static TheoryData<DicomTransferSyntax> JpegTransferSyntaxes => new()
    {
        DicomTransferSyntax.JPEGProcess1,
        DicomTransferSyntax.JPEGProcess2_4,
        DicomTransferSyntax.JPEGProcess14,
        DicomTransferSyntax.JPEGProcess14SV1,
    };

    [Fact]
    public void Monochrome_8_bit_fixture_contains_expected_pixel_shape()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8();

        var pixelData = DicomPixelData.Create(dataset);

        Assert.Equal((ushort)4, pixelData.Height);
        Assert.Equal((ushort)5, pixelData.Width);
        Assert.Equal((ushort)8, pixelData.BitsAllocated);
        Assert.Equal(1, pixelData.NumberOfFrames);
        Assert.Equal(20, pixelData.GetFrame(0).Size);
    }

    [Fact]
    public void Monochrome_16_bit_fixture_contains_expected_pixel_shape()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome16();

        var pixelData = DicomPixelData.Create(dataset);

        Assert.Equal((ushort)3, pixelData.Height);
        Assert.Equal((ushort)4, pixelData.Width);
        Assert.Equal((ushort)16, pixelData.BitsAllocated);
        Assert.Equal(1, pixelData.NumberOfFrames);
        Assert.Equal(24, pixelData.GetFrame(0).Size);
    }

    [Fact]
    public void Rgb_interleaved_fixture_sets_planar_configuration_zero()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved();

        var pixelData = DicomPixelData.Create(dataset);

        Assert.Equal((ushort)3, pixelData.SamplesPerPixel);
        Assert.Equal(PlanarConfiguration.Interleaved, pixelData.PlanarConfiguration);
        Assert.Equal(27, pixelData.GetFrame(0).Size);
    }

    [Fact]
    public void Rgb_planar_fixture_sets_planar_configuration_one()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbPlanar();

        var pixelData = DicomPixelData.Create(dataset);

        Assert.Equal((ushort)3, pixelData.SamplesPerPixel);
        Assert.Equal(PlanarConfiguration.Planar, pixelData.PlanarConfiguration);
        Assert.Equal(27, pixelData.GetFrame(0).Size);
    }

    [Fact]
    public void Multi_frame_fixture_preserves_all_frames()
    {
        var dataset = DicomPixelDataFixtures.CreateMultiFrameMonochrome8(frameCount: 4);

        var pixelData = DicomPixelData.Create(dataset);

        Assert.Equal(4, pixelData.NumberOfFrames);
        Assert.Equal(6, pixelData.GetFrame(0).Size);
        Assert.Equal(6, pixelData.GetFrame(3).Size);
    }

    [Fact]
    public void Required_compression_tag_assertion_accepts_compressed_pixel_dataset()
    {
        var pixelData = DicomPixelDataFixtures.CreateEmptyMonochrome8PixelData(DicomTransferSyntax.JPEGProcess1);

        PixelDataAssertions.AssertRequiredCompressionTags(pixelData.Dataset, DicomTransferSyntax.JPEGProcess1);
    }

    [Fact]
    public void Frame_count_assertion_accepts_preserved_frame_count()
    {
        var expected = DicomPixelData.Create(DicomPixelDataFixtures.CreateMultiFrameMonochrome8(frameCount: 3));
        var actual = DicomPixelData.Create(DicomPixelDataFixtures.CreateMultiFrameMonochrome8(frameCount: 3));

        PixelDataAssertions.AssertFrameCount(expected, actual);
    }

    [Fact]
    public void Exact_equality_assertion_accepts_identical_lossless_frames()
    {
        var expected = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 1, 2, 3, 4 }));
        var actual = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 1, 2, 3, 4 }));

        PixelDataAssertions.FramesMatchExactly(expected, actual);
    }

    [Fact]
    public void Tolerance_assertion_accepts_lossy_byte_differences_within_limit()
    {
        var expected = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 10, 20, 30, 40 }));
        var actual = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 11, 19, 32, 38 }));

        PixelDataAssertions.FramesMatchWithinTolerance(expected, actual, tolerance: 2);
    }

    [Theory]
    [MemberData(nameof(JpegTransferSyntaxes))]
    public void Raw_to_jpeg_acceptance_matrix_currently_fails_with_managed_stub_exception(DicomTransferSyntax syntax)
    {
        var codec = new PureTranscoderManager().GetCodec(syntax);
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8());
        var compressedPixelData = DicomPixelDataFixtures.CreateEmptyMonochrome8PixelData(syntax);

        var exception = PixelDataAssertions.AssertManagedCodecException<DicomCodecException>(
            () => codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters()));

        Assert.Contains(syntax.UID.Name, exception.Message);
        Assert.Contains("encode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(JpegTransferSyntaxes))]
    public void Compressed_to_raw_acceptance_matrix_currently_fails_with_managed_stub_exception(DicomTransferSyntax syntax)
    {
        var codec = new PureTranscoderManager().GetCodec(syntax);
        var compressedPixelData = DicomPixelDataFixtures.CreateEmptyMonochrome8PixelData(syntax);
        var rawPixelData = DicomPixelDataFixtures.CreateEmptyMonochrome8PixelData(DicomTransferSyntax.ExplicitVRLittleEndian);

        var exception = PixelDataAssertions.AssertManagedCodecException<DicomCodecException>(
            () => codec.Decode(compressedPixelData, rawPixelData, codec.GetDefaultParameters()));

        Assert.Contains(syntax.UID.Name, exception.Message);
        Assert.Contains("decode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
