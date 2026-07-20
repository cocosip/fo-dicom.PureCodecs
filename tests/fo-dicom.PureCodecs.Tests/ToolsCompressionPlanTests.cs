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
    public void CreateOutputPlan_skips_jpeg_baseline_for_16_bit_input()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
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
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var process24Item = Assert.Single(
            plan.Items,
            item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess2_4);
        Assert.True(process24Item.IsUnsupported);
        Assert.Equal("JPEG Extended Process 2/4 12-bit sequential DCT is not implemented by the current managed JPEG path.", process24Item.SkipReason);
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
    [InlineData("JPEG-LS Near-Lossless", @"Regression\Transcoded\1_jpegls_near_lossless.dcm", 2)]
    public void CompressAll_jpegls_outputs_from_local_real_fixture_match_reference_frame_size_and_round_trip(
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
        Assert.Equal(referencePixelData.GetFrame(0).Size, compressedPixelData.GetFrame(0).Size);
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
    public void CompressAll_jpeg2000_lossy_output_from_local_real_fixture_targets_reference_size_and_round_trips()
    {
        var inputPath = GetRegressionInputPath();
        var referencePath = ResolveFixturePath(@"Regression\Jpeg2000Baseline\fo_dicom_codecs_j2k_lossy.dcm");

        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-purecodecs-tool-regression-j2k-lossy-size");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var result = new DicomCompressionTool().CompressAll(inputPath, outputDirectory)
            .Single(item => item.Item.Format.TransferSyntax == DicomTransferSyntax.JPEG2000Lossy);

        Assert.Equal(CompressionResultStatus.Success, result.Status);
        var referencePixelData = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset);
        var compressedFile = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
        var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
        var decoded = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedFile.Dataset);
        var decodedPixelData = DicomPixelData.Create(decoded);
        var referenceFrameSize = referencePixelData.GetFrame(0).Size;
        var actualFrameSize = compressedPixelData.GetFrame(0).Size;
        var referenceFileSize = new FileInfo(referencePath).Length;
        var actualFileSize = new FileInfo(result.Item.OutputPath).Length;

        Assert.Equal(referencePixelData.Syntax, compressedPixelData.Syntax);
        Assert.InRange(Math.Abs(referenceFrameSize - actualFrameSize), 0, 1024);
        Assert.InRange(Math.Abs(referenceFileSize - actualFileSize), 0, 1024);
        PixelDataAssertions.FramesMatchWithinTolerance(sourcePixelData, decodedPixelData, tolerance: 16);
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
