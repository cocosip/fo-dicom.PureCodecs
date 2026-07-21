using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.PureCodecs.Tools;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class ToolsCompressionPlanTests
{
    [Fact]
    public void Parse_accepts_single_input_file()
    {
        var inputPath = Path.Combine("dicom", "sample.dcm");

        var options = ToolOptions.Parse(new[] { inputPath });

        Assert.Equal(inputPath, options.InputPath);
        Assert.Null(options.OutputDirectory);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_accepts_single_input_file_with_output_directory()
    {
        var inputPath = Path.Combine("dicom", "sample.dcm");
        var outputDirectory = Path.Combine("exports");

        var options = ToolOptions.Parse(new[] { inputPath, "-o", outputDirectory });

        Assert.Equal(inputPath, options.InputPath);
        Assert.Equal(outputDirectory, options.OutputDirectory);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void Parse_accepts_jpegls_lossless_format()
    {
        var options = ToolOptions.Parse(new[] { "D:\\60.dcm", "--format", "jpegls-lossless" });

        Assert.Equal("jpegls-lossless", options.Format);
    }

    [Fact]
    public void Parse_rejects_unknown_options()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ToolOptions.Parse(new[] { "--compress-all", Path.Combine("dicom", "sample.dcm") }));

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
        var inputPath = Path.Combine("dicom", "sample.image.dcm");
        var expectedOutputDirectory = Path.Combine("dicom", "sample.image_compressed");
        var plan = CompressionPlan.Create(
            inputPath,
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome8());

        Assert.Equal(expectedOutputDirectory, plan.OutputDirectory);
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.JPEGLSLossless &&
            item.OutputPath == Path.Combine(expectedOutputDirectory, "sample.image_jpegls_lossless.dcm"));
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL &&
            item.OutputPath == Path.Combine(expectedOutputDirectory, "sample.image_htj2k_lossless_rpcl.dcm"));
    }

    [Fact]
    public void CreateOutputPlan_uses_explicit_output_directory()
    {
        var inputPath = Path.Combine("dicom", "sample.dcm");
        var outputDirectory = Path.Combine("exports");
        var plan = CompressionPlan.Create(
            inputPath,
            outputDirectory,
            DicomPixelDataFixtures.CreateMonochrome8());

        Assert.Equal(outputDirectory, plan.OutputDirectory);
        Assert.All(plan.Items, item => Assert.StartsWith(outputDirectory + Path.DirectorySeparatorChar, item.OutputPath));
    }

    [Fact]
    public void CreateOutputPlan_leaves_high_bit_jpeg_baseline_validation_to_the_codec()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var baselineItem = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess1);
        Assert.False(baselineItem.IsUnsupported);
        Assert.Null(baselineItem.SkipReason);
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_process_2_4_for_16_bit_input()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var process24Item = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess2_4);
        Assert.True(process24Item.IsUnsupported);
        Assert.Equal("JPEG sequential DCT supports only BitsAllocated 8 and BitsStored 8; this image has BitsAllocated=16, BitsStored=16.", process24Item.SkipReason);
    }

    [Fact]
    public void CreateOutputPlan_allows_jpeg_process_2_4_for_12_bit_monochrome_input()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateBaseDataset(
                rows: 2,
                columns: 2,
                samplesPerPixel: 1,
                photometricInterpretation: PhotometricInterpretation.Monochrome2,
                bitsAllocated: 16,
                bitsStored: 12,
                highBit: 11,
                planarConfiguration: null,
                numberOfFrames: 1,
                transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
                frame: new byte[] { 0, 0, 255, 15, 0, 8, 0, 4 }));

        var process24Item = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess2_4);

        Assert.False(process24Item.IsUnsupported);
        Assert.Null(process24Item.SkipReason);
    }

    [Fact]
    public void CreateOutputPlan_enables_htj2k_after_standard_codestream_alignment()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome8());

        foreach (var syntax in new[]
        {
            DicomTransferSyntax.HTJ2KLossless,
            DicomTransferSyntax.HTJ2KLosslessRPCL,
            DicomTransferSyntax.HTJ2K
        })
        {
            var item = Assert.Single(plan.Items, item => item.Format.TransferSyntax == syntax);
            Assert.False(item.IsUnsupported);
            Assert.Null(item.SkipReason);
        }
    }

    [Fact]
    public void Compress_jpeg2000_lossy_uses_native_rate_16_layer_count()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-j2k-rate-16-input.dcm");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-j2k-rate-16");
        var source = new DicomFile(DicomPixelDataFixtures.CreateMonochrome8(rows: 128, columns: 128));
        source.Save(inputPath);

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var format = CompressionTargetFormats.All.Single(item => item.TransferSyntax == DicomTransferSyntax.JPEG2000Lossy);
        var result = new DicomCompressionTool().Compress(inputPath, outputDirectory, format);

        var output = Assert.Single(result);
        Assert.Equal(CompressionResultStatus.Success, output.Status);
        var pixelData = DicomPixelData.Create(DicomFile.Open(output.Item.OutputPath, FileReadOption.ReadAll).Dataset);
        Assert.Equal(8, ReadJpeg2000LayerCount(pixelData.GetFrame(0).Data));
    }

    [Fact]
    public void CreateOutputPlan_skips_jpeg_sequential_dct_for_unsupported_8_bit_samples_per_pixel()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
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

        var plan = CompressionPlan.Create(Path.Combine("dicom", "sample.dcm"), outputDirectory: null, dataset);

        AssertSequentialJpegSkipped(plan, "JPEG sequential DCT does not support photometric interpretation HSV.");
    }

    [Fact]
    public void CreateOutputPlan_allows_jpeg_sequential_dct_for_8_bit_palette_color()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreatePaletteColor8());

        foreach (var syntax in new[] { DicomTransferSyntax.JPEGProcess1, DicomTransferSyntax.JPEGProcess2_4 })
        {
            var item = Assert.Single(plan.Items, item => item.Format.TransferSyntax == syntax);
            Assert.False(item.IsUnsupported);
            Assert.Null(item.SkipReason);
        }
    }

    [Theory]
    [InlineData(@"Regression\Transcoded\1_jpeg_lossless.dcm", "1_jpeg_lossless.dcm")]
    [InlineData("Regression/Transcoded/1_jpegls_lossless.dcm", "1_jpegls_lossless.dcm")]
    public void Regression_reference_file_name_parsing_accepts_windows_and_unix_separators(
        string referencePath,
        string expectedFileName)
    {
        Assert.Equal(expectedFileName, GetPortableFileName(referencePath));
    }

    [Fact]
    public void CompressAll_outputs_from_local_real_fixture_decode_and_render()
    {
        var inputPath = GetRegressionInputPath();

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
        var inputPath = GetRegressionInputPath();

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
    [InlineData("JPEG Lossless Process 14", @"Regression\Transcoded\1_jpeg_lossless.dcm")]
    [InlineData("JPEG Lossless Process 14 SV1", @"Regression\Transcoded\1_jpeg_lossless_sv1.dcm")]
    public void CompressAll_jpeg_lossless_outputs_from_local_real_fixture_match_reference_frame_size_and_round_trip(
        string formatName,
        string referencePath)
    {
        var inputPath = GetRegressionInputPath();
        referencePath = ResolveFixturePath(referencePath);

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
        Assert.Equal(
            GetJpegCodestream(referencePixelData.GetFrame(0).Data),
            GetJpegCodestream(compressedPixelData.GetFrame(0).Data));
        PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        using var rendered = new DicomImage(result.Item.OutputPath).RenderImage();
        Assert.NotNull(rendered);
    }

    [Theory]
    [InlineData("JPEG-LS Lossless", @"Regression\Transcoded\1_jpegls_lossless.dcm", 0)]
    [InlineData("JPEG-LS Near-Lossless", @"Regression\Transcoded\1_jpegls_near_lossless.dcm", 3)]
    public void CompressAll_jpegls_outputs_from_local_real_fixture_emit_expected_near_value_and_round_trip(
        string formatName,
        string referencePath,
        int tolerance)
    {
        var inputPath = GetRegressionInputPath();
        referencePath = ResolveFixturePath(referencePath);

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-jpegls");
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
        Assert.Equal(tolerance, GetJpegLsNearLossless(compressedPixelData.GetFrame(0).Data));
        if (tolerance == 0)
        {
            Assert.Equal(referencePixelData.GetFrame(0).Size, compressedPixelData.GetFrame(0).Size);
            PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        }
        else
        {
            PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance);
        }

        using var rendered = new DicomImage(result.Item.OutputPath).RenderImage();
        Assert.NotNull(rendered);
    }

    private static int GetJpegLsNearLossless(byte[] codestream)
    {
        for (var index = 0; index + 3 < codestream.Length; index++)
        {
            if (codestream[index] != 0xff || codestream[index + 1] != 0xda)
            {
                continue;
            }

            var length = (codestream[index + 2] << 8) | codestream[index + 3];
            return codestream[index + length - 1];
        }

        throw new Xunit.Sdk.XunitException("JPEG-LS codestream does not contain SOS.");
    }

    [Theory]
    [InlineData("JPEG 2000 Lossless", @"Regression\Jpeg2000Baseline\fo_dicom_codecs_j2k_lossless.dcm", 0)]
    public void CompressAll_jpeg2000_classic_lossless_output_from_local_real_fixture_uses_standard_codestream_and_matches_reference_size(
        string formatName,
        string referencePath,
        int tolerance)
    {
        var inputPath = GetRegressionInputPath();
        referencePath = ResolveFixturePath(referencePath);

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-j2k");
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
        var frame = compressedPixelData.GetFrame(0).Data;
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(referencePixelData.Syntax, compressedPixelData.Syntax);
        var referenceFrame = referencePixelData.GetFrame(0).Data;
        var referenceFrameSize = referenceFrame.Length;
        var referenceFileSize = new FileInfo(referencePath).Length;
        var actualFileSize = new FileInfo(result.Item.OutputPath).Length;
        Assert.True(
            referenceFrameSize == frame.Length,
            $"Expected JPEG 2000 frame size {referenceFrameSize} but got {frame.Length}.");
        Assert.Equal(referenceFrame, frame);
        Assert.Equal(GetLogicalCodestreamLength(referenceFrame), GetLogicalCodestreamLength(frame));
        Assert.Equal(ReadSotTilePartLength(referenceFrame), ReadSotTilePartLength(frame));
        Assert.True(
            referenceFileSize == actualFileSize,
            $"Expected DICOM file size {referenceFileSize} but got {actualFileSize}.");
        Assert.DoesNotContain(new byte[] { (byte)'P', (byte)'C', (byte)'J', (byte)'2' }, frame);
        if (tolerance == 0)
        {
            PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        }
        else
        {
            PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance);
        }
    }

    [Fact]
    public void Compress_jpeg2000_classic_lossless_local_real_2_matches_native_codestream_byte_for_byte()
    {
        var inputPath = RegressionFixturePaths.LocalReal2;
        var referencePath = RegressionFixturePaths.Jpeg2000Baseline("fo_dicom_codecs_local2_j2k_lossless.dcm");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-j2k-local-real-2", Guid.NewGuid().ToString("N"));
        var format = CompressionTargetFormats.All.Single(item => item.TransferSyntax == DicomTransferSyntax.JPEG2000Lossless);

        try
        {
            var result = Assert.Single(new DicomCompressionTool().Compress(inputPath, outputDirectory, format));

            Assert.Equal(CompressionResultStatus.Success, result.Status);

            var referenceFrame = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
            var actualFrame = DicomPixelData.Create(DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;

            Assert.Equal(
                string.Join(",", ReadJpeg2000PacketEndOffsets(referenceFrame)),
                string.Join(",", ReadJpeg2000PacketEndOffsets(actualFrame)));
            Assert.Equal(referenceFrame, actualFrame);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CompressAll_jpeg2000_lossy_output_from_local_real_fixture_targets_reference_size_and_round_trips()
    {
        var inputPath = GetRegressionInputPath();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-j2k-lossy-size");
        var nativeOutputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-native-tool-regression-j2k-lossy-size");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        if (Directory.Exists(nativeOutputDirectory))
        {
            Directory.Delete(nativeOutputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var result = new DicomCompressionTool().CompressAll(inputPath, outputDirectory)
            .Single(item => item.Item.Format.TransferSyntax == DicomTransferSyntax.JPEG2000Lossy);
        var nativeFormat = FellowOakDicom.NativeCodecs.Tools.CompressionTargetFormats.All
            .Single(item => item.TransferSyntax == DicomTransferSyntax.JPEG2000Lossy);
        var nativeResult = new FellowOakDicom.NativeCodecs.Tools.DicomCompressionTool()
            .Compress(inputPath, nativeOutputDirectory, nativeFormat);

        Assert.Equal(CompressionResultStatus.Success, result.Status);
        var nativeOutput = Assert.Single(nativeResult);
        Assert.Equal(FellowOakDicom.NativeCodecs.Tools.CompressionResultStatus.Success, nativeOutput.Status);
        var referencePixelData = DicomPixelData.Create(DicomFile.Open(nativeOutput.Item.OutputPath, FileReadOption.ReadAll).Dataset);
        var compressedFile = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);
        var referenceFrameSize = referencePixelData.GetFrame(0).Size;
        var actualFrameSize = compressedPixelData.GetFrame(0).Size;
        var referenceFileSize = new FileInfo(nativeOutput.Item.OutputPath).Length;
        var actualFileSize = new FileInfo(result.Item.OutputPath).Length;

        Assert.Equal(referencePixelData.Syntax, compressedPixelData.Syntax);
        Assert.InRange(Math.Abs(referenceFrameSize - actualFrameSize), 0, 3072);
        Assert.InRange(Math.Abs(referenceFileSize - actualFileSize), 0, 3072);
        PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance: 16);
    }

    [Fact]
    public void Compress_htj2k_lossy_output_uses_native_default_qcd()
    {
        var inputPath = GetRegressionInputPath();
        var pureOutputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-htj2k-default-qcd", Guid.NewGuid().ToString("N"));
        var nativeOutputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-native-tool-regression-htj2k-default-qcd", Guid.NewGuid().ToString("N"));
        var pureFormat = CompressionTargetFormats.All.Single(item => item.TransferSyntax == DicomTransferSyntax.HTJ2K);
        var nativeFormat = FellowOakDicom.NativeCodecs.Tools.CompressionTargetFormats.All
            .Single(item => item.TransferSyntax == DicomTransferSyntax.HTJ2K);

        try
        {
            var nativeResult = Assert.Single(new FellowOakDicom.NativeCodecs.Tools.DicomCompressionTool()
                .Compress(inputPath, nativeOutputDirectory, nativeFormat));
            var pureResult = Assert.Single(new DicomCompressionTool()
                .Compress(inputPath, pureOutputDirectory, pureFormat));

            Assert.Equal(FellowOakDicom.NativeCodecs.Tools.CompressionResultStatus.Success, nativeResult.Status);
            Assert.Equal(CompressionResultStatus.Success, pureResult.Status);

            var nativeFrame = DicomPixelData.Create(DicomFile.Open(nativeResult.Item.OutputPath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
            var pureFrame = DicomPixelData.Create(DicomFile.Open(pureResult.Item.OutputPath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;

            Assert.Equal(ReadJpeg2000QcdPayload(nativeFrame), ReadJpeg2000QcdPayload(pureFrame));
            Assert.InRange(
                Math.Abs(GetLogicalCodestreamLength(nativeFrame) - GetLogicalCodestreamLength(pureFrame)),
                0,
                64);
        }
        finally
        {
            if (Directory.Exists(pureOutputDirectory))
            {
                Directory.Delete(pureOutputDirectory, recursive: true);
            }

            if (Directory.Exists(nativeOutputDirectory))
            {
                Directory.Delete(nativeOutputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Compress_htj2k_lossy_sparse_12_bit_image_decodes_with_native_codecs()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-htj2k-sparse-12-bit-" + Guid.NewGuid().ToString("N") + ".dcm");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-htj2k-sparse-12-bit-" + Guid.NewGuid().ToString("N"));
        var frame = CreateSparseTwelveBitFrame(rows: 288, columns: 288);
        var sourceDataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows: 288,
            columns: 288,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 16,
            bitsStored: 12,
            highBit: 11,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            frame: frame);

        try
        {
            new DicomFile(sourceDataset).Save(inputPath);
            var sourcePixelData = DicomPixelData.Create(sourceDataset);
            var format = CompressionTargetFormats.All.Single(item => item.TransferSyntax == DicomTransferSyntax.HTJ2K);
            var result = Assert.Single(new DicomCompressionTool().Compress(inputPath, outputDirectory, format));

            Assert.Equal(CompressionResultStatus.Success, result.Status);

            new DicomSetupBuilder()
                .RegisterServices(services => services
                    .AddFellowOakDicom()
                    .AddTranscoderManager<NativeTranscoderManager>())
                .SkipValidation()
                .Build();

            AssertNativeDecode(result.Item.OutputPath, sourcePixelData, tolerance: 512);
        }
        finally
        {
            if (File.Exists(inputPath))
            {
                File.Delete(inputPath);
            }

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Compress_htj2k_lossless_12_bit_image_uses_native_component_precision_and_qcd()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-htj2k-lossless-12-bit-" + Guid.NewGuid().ToString("N") + ".dcm");
        var pureOutputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-htj2k-lossless-12-bit-" + Guid.NewGuid().ToString("N"));
        var nativeOutputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-native-htj2k-lossless-12-bit-" + Guid.NewGuid().ToString("N"));
        var sourceDataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows: 288,
            columns: 288,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 16,
            bitsStored: 12,
            highBit: 11,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            frame: CreateSparseTwelveBitFrame(rows: 288, columns: 288));

        try
        {
            new DicomFile(sourceDataset).Save(inputPath);
            var pureFormat = CompressionTargetFormats.All.Single(item => item.TransferSyntax == DicomTransferSyntax.HTJ2KLossless);
            var nativeFormat = FellowOakDicom.NativeCodecs.Tools.CompressionTargetFormats.All
                .Single(item => item.TransferSyntax == DicomTransferSyntax.HTJ2KLossless);
            var nativeResult = Assert.Single(new FellowOakDicom.NativeCodecs.Tools.DicomCompressionTool()
                .Compress(inputPath, nativeOutputDirectory, nativeFormat));
            var pureResult = Assert.Single(new DicomCompressionTool()
                .Compress(inputPath, pureOutputDirectory, pureFormat));

            Assert.Equal(FellowOakDicom.NativeCodecs.Tools.CompressionResultStatus.Success, nativeResult.Status);
            Assert.Equal(CompressionResultStatus.Success, pureResult.Status);

            var nativeFrame = DicomPixelData.Create(DicomFile.Open(nativeResult.Item.OutputPath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
            var pureFrame = DicomPixelData.Create(DicomFile.Open(pureResult.Item.OutputPath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;

            Assert.Equal(ReadJpeg2000ComponentPrecision(nativeFrame), ReadJpeg2000ComponentPrecision(pureFrame));
            Assert.Equal(ReadJpeg2000QcdPayload(nativeFrame), ReadJpeg2000QcdPayload(pureFrame));
            Assert.Equal(ReadJpeg2000CommentPayload(nativeFrame), ReadJpeg2000CommentPayload(pureFrame));
        }
        finally
        {
            if (File.Exists(inputPath))
            {
                File.Delete(inputPath);
            }

            foreach (var outputDirectory in new[] { pureOutputDirectory, nativeOutputDirectory })
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void CompressAll_htj2k_outputs_from_local_real_fixture_decode_with_native_codecs_and_lossy_is_smaller()
    {
        var inputPath = GetRegressionInputPath();

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-htj2k-native");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var results = new DicomCompressionTool().CompressAll(inputPath, outputDirectory);
        var lossless = Assert.Single(results, item => item.Item.Format.TransferSyntax == DicomTransferSyntax.HTJ2KLossless);
        var losslessRpcl = Assert.Single(results, item => item.Item.Format.TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL);
        var lossy = Assert.Single(results, item => item.Item.Format.TransferSyntax == DicomTransferSyntax.HTJ2K);

        Assert.Equal(CompressionResultStatus.Success, lossless.Status);
        Assert.Equal(CompressionResultStatus.Success, losslessRpcl.Status);
        Assert.Equal(CompressionResultStatus.Success, lossy.Status);
        Assert.True(
            lossy.OutputSize < lossless.OutputSize,
            $"HTJ2K lossy file should be smaller than lossless. Lossy={lossy.OutputSize}, lossless={lossless.OutputSize}.");

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        AssertNativeDecode(lossless.Item.OutputPath, sourcePixelData, tolerance: 0);
        AssertNativeDecode(losslessRpcl.Item.OutputPath, sourcePixelData, tolerance: 0);
        AssertNativeDecode(lossy.Item.OutputPath, sourcePixelData, tolerance: 512);
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

    private static byte[] GetJpegCodestream(byte[] frame)
    {
        for (var index = 0; index + 1 < frame.Length; index++)
        {
            if (frame[index] != 0xff || frame[index + 1] != 0xd9)
            {
                continue;
            }

            var codestream = new byte[index + 2];
            Buffer.BlockCopy(frame, 0, codestream, 0, codestream.Length);
            return codestream;
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain an EOI marker.");
    }

    private static int ReadJpeg2000LayerCount(byte[] codestream)
    {
        for (var index = 0; index + 7 < codestream.Length; index++)
        {
            if (codestream[index] == 0xff && codestream[index + 1] == 0x52)
            {
                return (codestream[index + 6] << 8) | codestream[index + 7];
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG 2000 COD marker was not found.");
    }

    private static byte[] ReadJpeg2000QcdPayload(byte[] codestream)
    {
        for (var index = 0; index + 3 < codestream.Length; index++)
        {
            if (codestream[index] != 0xff || codestream[index + 1] != 0x5c)
            {
                continue;
            }

            var length = (codestream[index + 2] << 8) | codestream[index + 3];
            if (length < 2 || index + 2 + length > codestream.Length)
            {
                break;
            }

            var payload = new byte[length - 2];
            Buffer.BlockCopy(codestream, index + 4, payload, 0, payload.Length);
            return payload;
        }

        throw new Xunit.Sdk.XunitException("JPEG 2000 QCD marker was not found.");
    }

    private static byte ReadJpeg2000ComponentPrecision(byte[] codestream)
    {
        for (var index = 0; index + 43 < codestream.Length; index++)
        {
            if (codestream[index] != 0xff || codestream[index + 1] != 0x51)
            {
                continue;
            }

            var length = (codestream[index + 2] << 8) | codestream[index + 3];
            if (length < 41 || index + 43 >= codestream.Length)
            {
                break;
            }

            return codestream[index + 40];
        }

        throw new Xunit.Sdk.XunitException("JPEG 2000 SIZ marker was not found.");
    }

    private static byte[] ReadJpeg2000CommentPayload(byte[] codestream)
    {
        for (var index = 0; index + 3 < codestream.Length; index++)
        {
            if (codestream[index] != 0xff || codestream[index + 1] != 0x64)
            {
                continue;
            }

            var length = (codestream[index + 2] << 8) | codestream[index + 3];
            if (length < 2 || index + 2 + length > codestream.Length)
            {
                break;
            }

            var payload = new byte[length - 2];
            Buffer.BlockCopy(codestream, index + 4, payload, 0, payload.Length);
            return payload;
        }

        throw new Xunit.Sdk.XunitException("JPEG 2000 COM marker was not found.");
    }

    private static void AssertNativeDecode(string path, DicomPixelData sourcePixelData, int tolerance)
    {
        var compressedFile = DicomFile.Open(path, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);

        if (tolerance == 0)
        {
            PixelDataAssertions.FramesMatchExactly(sourcePixelData, decodedPixelData);
        }
        else
        {
            PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance);
        }
    }

    private static byte[] CreateSparseTwelveBitFrame(int rows, int columns)
    {
        var frame = new byte[rows * columns * 2];
        for (var y = 64; y < rows - 64; y++)
        {
            for (var x = 64; x < columns - 64; x++)
            {
                var value = (ushort)((((x * 37) ^ (y * 97)) + ((x - 144) * (x - 144)) + ((y - 144) * (y - 144))) & 0x0fff);
                var offset = ((y * columns) + x) * 2;
                frame[offset] = (byte)value;
                frame[offset + 1] = (byte)(value >> 8);
            }
        }

        return frame;
    }

    private static IReadOnlyList<int> ReadJpeg2000PacketEndOffsets(byte[] frame)
    {
        var assembly = typeof(FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000LosslessCodec).Assembly;
        var parserType = assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000CodestreamParser", throwOnError: true)!;
        var parsed = parserType.GetMethod("ParseSingleTilePart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { frame, "JPEG 2000", "JPEG 2000 classic" })!;
        var parsedType = parsed.GetType();
        var size = parsedType.GetProperty("Size")!.GetValue(parsed)!;
        var codingStyle = parsedType.GetProperty("CodingStyle")!.GetValue(parsed)!;
        var tileData = Assert.IsType<byte[]>(parsedType.GetProperty("TileData")!.GetValue(parsed));
        var codingStyleType = codingStyle.GetType();
        var frameDecoderType = assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameDecoder", throwOnError: true)!;
        var components = Assert.IsAssignableFrom<Array>(frameDecoderType.GetMethod(
            "CreateComponents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new[] { size, codingStyle }));
        var packetDecoderType = assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder", throwOnError: true)!;
        var decoder = Activator.CreateInstance(
            packetDecoderType,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                tileData,
                components.Length,
                codingStyleType.GetProperty("LayerCount")!.GetValue(codingStyle)!,
                (int)codingStyleType.GetProperty("DecompositionLevels")!.GetValue(codingStyle)! + 1,
                codingStyleType.GetProperty("ProgressionOrder")!.GetValue(codingStyle)!,
                components,
                codingStyleType.GetProperty("CodeBlockWidth")!.GetValue(codingStyle)!,
                codingStyleType.GetProperty("CodeBlockHeight")!.GetValue(codingStyle)!,
                codingStyleType.GetProperty("CodeBlockStyle")!.GetValue(codingStyle)!,
                codingStyle
            },
            culture: null)!;
        packetDecoderType.GetMethod("BuildCodeBlockMaps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(decoder, null);

        var decodePacket = packetDecoderType.GetMethod("DecodePacket", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var offset = packetDecoderType.GetField("_offset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var packets = (System.Collections.IEnumerable)packetDecoderType.GetMethod(
            "EnumeratePackets",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(decoder, null)!;
        var offsets = new List<int>();
        foreach (var packet in packets)
        {
            var packetType = packet.GetType();
            decodePacket.Invoke(decoder, new[]
            {
                packetType.GetProperty("LayerIndex")!.GetValue(packet),
                packetType.GetProperty("ResolutionLevel")!.GetValue(packet),
                packetType.GetProperty("ComponentIndex")!.GetValue(packet),
                packetType.GetProperty("PrecinctIndex")!.GetValue(packet)
            });
            offsets.Add((int)offset.GetValue(decoder)!);
        }

        return offsets;
    }

    private static int ReadRleSegmentCount(byte[] frame)
    {
        return frame[0] | (frame[1] << 8) | (frame[2] << 16) | (frame[3] << 24);
    }

    private static int GetLogicalCodestreamLength(byte[] frame)
    {
        for (var index = frame.Length - 2; index >= 0; index--)
        {
            if (frame[index] == 0xFF && frame[index + 1] == 0xD9)
            {
                return index + 2;
            }
        }

        throw new InvalidOperationException("JPEG 2000 EOC marker was not found.");
    }

    private static uint ReadSotTilePartLength(byte[] frame)
    {
        for (var index = 0; index + 9 < frame.Length; index++)
        {
            if (frame[index] == 0xFF && frame[index + 1] == 0x90)
            {
                return ((uint)frame[index + 6] << 24)
                    | ((uint)frame[index + 7] << 16)
                    | ((uint)frame[index + 8] << 8)
                    | frame[index + 9];
            }
        }

        throw new InvalidOperationException("JPEG 2000 SOT marker was not found.");
    }

    private static string GetRegressionInputPath()
    {
        return RegressionFixturePaths.LocalReal1;
    }

    private static string ResolveFixturePath(string path)
    {
        if (path.IndexOf("Jpeg2000Baseline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return RegressionFixturePaths.Jpeg2000Baseline(GetPortableFileName(path));
        }

        return RegressionFixturePaths.Transcoded(GetPortableFileName(path));
    }

    private static string GetPortableFileName(string path)
    {
        var normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFileName(normalized);
    }
}
