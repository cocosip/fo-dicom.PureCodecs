using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Tools;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class ToolsCompressionPlanTests
{
    [Fact]
    public void Parse_accepts_single_input_file()
    {
        var options = ToolOptions.Parse(new[] { @"D:\dicom\sample.dcm" });

        Assert.Equal(@"D:\dicom\sample.dcm", options.InputPath);
        Assert.Null(options.OutputDirectory);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_accepts_single_input_file_with_output_directory()
    {
        var options = ToolOptions.Parse(new[] { @"D:\dicom\sample.dcm", "-o", @"D:\exports" });

        Assert.Equal(@"D:\dicom\sample.dcm", options.InputPath);
        Assert.Equal(@"D:\exports", options.OutputDirectory);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_rejects_unknown_options()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ToolOptions.Parse(new[] { "--compress-all", @"D:\dicom\sample.dcm" }));

        Assert.Equal("Unknown option --compress-all.", exception.Message);
    }

    [Fact]
    public void AllTargetFormats_contains_every_phase_1_compression_syntax()
    {
        var syntaxes = CompressionTargetFormats.All.Select(format => format.TransferSyntax).ToArray();

        Assert.Equal(
            new[]
            {
                DicomTransferSyntax.RLELossless,
                DicomTransferSyntax.JPEGProcess1,
                DicomTransferSyntax.JPEGProcess2_4,
                DicomTransferSyntax.JPEGProcess14,
                DicomTransferSyntax.JPEGProcess14SV1,
                DicomTransferSyntax.JPEGLSLossless,
                DicomTransferSyntax.JPEGLSNearLossless,
                DicomTransferSyntax.JPEG2000Lossless,
                DicomTransferSyntax.JPEG2000Lossy,
                DicomTransferSyntax.HTJ2KLossless,
                DicomTransferSyntax.HTJ2KLosslessRPCL,
                DicomTransferSyntax.HTJ2K,
            },
            syntaxes);
    }

    [Fact]
    public void CreateOutputPlan_uses_default_output_directory_and_stable_suffixes()
    {
        var plan = CompressionPlan.Create(
            @"D:\dicom\sample.image.dcm",
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome8());

        Assert.Equal(@"D:\dicom\sample.image_compressed", plan.OutputDirectory);
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.JPEGLSLossless &&
            item.OutputPath == @"D:\dicom\sample.image_compressed\sample.image_jpegls_lossless.dcm");
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL &&
            item.OutputPath == @"D:\dicom\sample.image_compressed\sample.image_htj2k_lossless_rpcl.dcm");
    }

    [Fact]
    public void CreateOutputPlan_uses_explicit_output_directory()
    {
        var plan = CompressionPlan.Create(
            @"D:\dicom\sample.dcm",
            @"D:\exports",
            DicomPixelDataFixtures.CreateMonochrome8());

        Assert.Equal(@"D:\exports", plan.OutputDirectory);
        Assert.All(plan.Items, item => Assert.StartsWith(@"D:\exports\", item.OutputPath));
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_baseline_for_16_bit_input()
    {
        var plan = CompressionPlan.Create(
            @"D:\dicom\sample.dcm",
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var baselineItem = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess1);
        Assert.True(baselineItem.IsUnsupported);
        Assert.Equal("JPEG Baseline Process 1 supports only 8-bit input; this image has BitsStored=16.", baselineItem.SkipReason);
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_process_2_4_for_16_bit_input()
    {
        var plan = CompressionPlan.Create(
            @"D:\dicom\sample.dcm",
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var process24Item = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess2_4);
        Assert.True(process24Item.IsUnsupported);
        Assert.Equal("JPEG Extended Process 2/4 12-bit sequential DCT is not implemented by the current managed JPEG path.", process24Item.SkipReason);
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_sequential_dct_for_unsupported_8_bit_samples_per_pixel()
    {
        var plan = CompressionPlan.Create(
            @"D:\dicom\sample.dcm",
            outputDirectory: null,
            DicomPixelDataFixtures.CreateBaseDataset(
                rows: 2,
                columns: 2,
                samplesPerPixel: 4,
                photometricInterpretation: PhotometricInterpretation.Rgb,
                bitsAllocated: 8,
                bitsStored: 8,
                highBit: 7,
                planarConfiguration: PlanarConfiguration.Interleaved,
                numberOfFrames: 1,
                transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
                frame: new byte[16]));

        AssertSequentialJpegSkipped(plan, "JPEG sequential DCT supports only SamplesPerPixel 1 or 3.");
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_sequential_dct_for_unsupported_8_bit_photometric_interpretation()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved();
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, "HSV");

        var plan = CompressionPlan.Create(@"D:\dicom\sample.dcm", outputDirectory: null, dataset);

        AssertSequentialJpegSkipped(plan, "JPEG sequential DCT does not support photometric interpretation HSV.");
    }

    [Fact]
    public void CompressAll_outputs_from_local_real_fixture_decode_and_render()
    {
        const string inputPath = @"D:\1.dcm";
        if (!File.Exists(inputPath))
        {
            return;
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var results = new DicomCompressionTool().CompressAll(inputPath, outputDirectory);

        Assert.Contains(results, result => result.Status == CompressionResultStatus.Success);
        var failures = new List<string>();
        foreach (var result in results.Where(result => result.Status == CompressionResultStatus.Success))
        {
            try
            {
                var file = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
                var pixelData = DicomPixelData.Create(file.Dataset);
                var raw = new DicomTranscoder(pixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
                    .Transcode(file.Dataset);
                var rawPixelData = DicomPixelData.Create(raw);

                Assert.Equal(pixelData.NumberOfFrames, rawPixelData.NumberOfFrames);
                Assert.True(rawPixelData.GetFrame(0).Size > 0);
                using var rendered = new DicomImage(result.Item.OutputPath).RenderImage();
                Assert.NotNull(rendered);
            }
            catch (Exception exception)
            {
                failures.Add($"{result.Item.Format.Name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void CompressAll_rle_output_from_local_real_fixture_round_trips_exactly()
    {
        const string inputPath = @"D:\1.dcm";
        if (!File.Exists(inputPath))
        {
            return;
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-rle");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var result = new DicomCompressionTool().CompressAll(inputPath, outputDirectory)
            .Single(item => item.Item.Format.TransferSyntax == DicomTransferSyntax.RLELossless);

        Assert.Equal(CompressionResultStatus.Success, result.Status);
        var compressedFile = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(DicomTransferSyntax.RLELossless, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(DicomTransferSyntax.RLELossless, compressedPixelData.Syntax);
        Assert.Equal(sourcePixelData.NumberOfFrames, compressedPixelData.NumberOfFrames);
        Assert.Equal(sourcePixelData.Width, compressedPixelData.Width);
        Assert.Equal(sourcePixelData.Height, compressedPixelData.Height);
        Assert.Equal(sourcePixelData.BitsAllocated, compressedPixelData.BitsAllocated);
        Assert.Equal(sourcePixelData.BitsStored, compressedPixelData.BitsStored);
        Assert.Equal(sourcePixelData.HighBit, compressedPixelData.HighBit);
        Assert.Equal(sourcePixelData.PixelRepresentation, compressedPixelData.PixelRepresentation);
        Assert.Equal(2, ReadRleSegmentCount(compressedPixelData.GetFrame(0).Data));
        PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
    }

    [Theory]
    [InlineData("JPEG Lossless Process 14", @"D:\1_transcoded\1_jpeg_lossless.dcm")]
    [InlineData("JPEG Lossless Process 14 SV1", @"D:\1_transcoded\1_jpeg_lossless_sv1.dcm")]
    public void CompressAll_jpeg_lossless_outputs_from_local_real_fixture_match_reference_frame_size_and_round_trip(
        string formatName,
        string referencePath)
    {
        const string inputPath = @"D:\1.dcm";
        if (!File.Exists(inputPath) || !File.Exists(referencePath))
        {
            return;
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-jpeg");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var result = new DicomCompressionTool().CompressAll(inputPath, outputDirectory)
            .Single(item => item.Item.Format.Name == formatName);

        Assert.Equal(CompressionResultStatus.Success, result.Status);
        var referencePixelData = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset);
        var compressedFile = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(referencePixelData.Syntax, compressedPixelData.Syntax);
        Assert.Equal(sourcePixelData.NumberOfFrames, compressedPixelData.NumberOfFrames);
        Assert.Equal(referencePixelData.GetFrame(0).Size, compressedPixelData.GetFrame(0).Size);
        PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        using var rendered = new DicomImage(result.Item.OutputPath).RenderImage();
        Assert.NotNull(rendered);
    }

    [Theory]
    [InlineData("JPEG-LS Lossless", 0)]
    [InlineData("JPEG-LS Near-Lossless", 3)]
    public void CompressAll_jpegls_outputs_from_local_real_fixture_round_trip_with_expected_tolerance(
        string formatName,
        int tolerance)
    {
        const string inputPath = @"D:\1.dcm";
        if (!File.Exists(inputPath))
        {
            return;
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-jpegls");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var result = new DicomCompressionTool().CompressAll(inputPath, outputDirectory)
            .Single(item => item.Item.Format.Name == formatName);

        Assert.Equal(CompressionResultStatus.Success, result.Status);
        var compressedFile = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(sourcePixelData.NumberOfFrames, compressedPixelData.NumberOfFrames);
        if (tolerance == 0)
        {
            PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        }
        else
        {
            PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance);
        }

        using var rendered = new DicomImage(result.Item.OutputPath).RenderImage();
        Assert.NotNull(rendered);
    }

    private static void AssertSequentialJpegSkipped(CompressionPlan plan, string reason)
    {
        foreach (var syntax in new[] { DicomTransferSyntax.JPEGProcess1, DicomTransferSyntax.JPEGProcess2_4 })
        {
            var item = Assert.Single(plan.Items, item => item.Format.TransferSyntax == syntax);
            Assert.True(item.IsUnsupported);
            Assert.Equal(reason, item.SkipReason);
        }
    }

    private static int ReadRleSegmentCount(byte[] frame)
    {
        return frame[0] | (frame[1] << 8) | (frame[2] << 16) | (frame[3] << 24);
    }
}
