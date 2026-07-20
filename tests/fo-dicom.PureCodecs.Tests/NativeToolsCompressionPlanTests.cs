using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.NativeCodecs.Tools;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class NativeToolsCompressionPlanTests
{
    [Fact]
    public void TrimJpegLsFramePadding_discards_bytes_after_eoi_and_preserves_dicom_even_length()
    {
        var trimMethod = typeof(DicomCompressionTool).GetMethod(
            "TrimJpegLsFramePadding",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(trimMethod);

        var oddLength = Assert.IsType<byte[]>(trimMethod!.Invoke(null, new object[]
        {
            new byte[] { 0xff, 0xd8, 0x01, 0xff, 0xd9, 0xa5, 0x7e }
        }));
        var evenLength = Assert.IsType<byte[]>(trimMethod.Invoke(null, new object[]
        {
            new byte[] { 0xff, 0xd8, 0x01, 0x02, 0xff, 0xd9, 0xa5 }
        }));

        Assert.Equal(new byte[] { 0xff, 0xd8, 0x01, 0xff, 0xd9, 0x00 }, oddLength);
        Assert.Equal(new byte[] { 0xff, 0xd8, 0x01, 0x02, 0xff, 0xd9 }, evenLength);
    }

    [Fact]
    public void Parse_accepts_single_input_file_with_format_and_output_directory()
    {
        var inputPath = Path.Combine("dicom", "sample.dcm");
        var outputDirectory = Path.Combine("exports");

        var options = ToolOptions.Parse(new[] { inputPath, "--format", "j2k_lossy", "-o", outputDirectory });

        Assert.Equal(inputPath, options.InputPath);
        Assert.Equal("j2k_lossy", options.Format);
        Assert.Equal(outputDirectory, options.OutputDirectory);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void AllTargetFormats_matches_the_phase_1_native_compression_contract()
    {
        var formats = CompressionTargetFormats.All;

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
            formats.Select(format => format.TransferSyntax));
        Assert.Equal(
            new[]
            {
                "rle",
                "jpeg_baseline",
                "jpeg_process2_4",
                "jpeg_lossless_14",
                "jpeg_lossless_sv1",
                "jpegls_lossless",
                "jpegls_near_lossless",
                "j2k_lossless",
                "j2k_lossy",
                "htj2k_lossless",
                "htj2k_lossless_rpcl",
                "htj2k_lossy",
            },
            formats.Select(format => format.Suffix));
    }

    [Fact]
    public void CreateOutputPlan_uses_the_shared_default_directory_and_file_names()
    {
        var inputPath = Path.Combine("dicom", "sample.image.dcm");
        var plan = CompressionPlan.Create(inputPath, outputDirectory: null, DicomPixelDataFixtures.CreateMonochrome8());

        var expectedOutputDirectory = Path.Combine("dicom", "sample.image_compressed");
        Assert.Equal(expectedOutputDirectory, plan.OutputDirectory);
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.JPEGLSLossless &&
            item.OutputPath == Path.Combine(expectedOutputDirectory, "sample.image_jpegls_lossless.dcm"));
        Assert.Contains(plan.Items, item =>
            item.Format.TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL &&
            item.OutputPath == Path.Combine(expectedOutputDirectory, "sample.image_htj2k_lossless_rpcl.dcm"));
    }

    [Fact]
    public void CreateOutputPlan_leaves_16_bit_jpeg_baseline_validation_to_the_native_codec()
    {
        var plan = CompressionPlan.Create(
            Path.Combine("dicom", "sample.dcm"),
            outputDirectory: null,
            DicomPixelDataFixtures.CreateMonochrome16());

        var item = Assert.Single(plan.Items, item => item.Format.TransferSyntax == DicomTransferSyntax.JPEGProcess1);
        Assert.False(item.IsUnsupported);
        Assert.Null(item.SkipReason);
    }

    [Fact]
    public void Compress_encodes_rle_with_the_native_transcoder_manager()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;
        var outputDirectory = Path.Combine(Path.GetTempPath(), "fo-dicom-native-codecs-tool-rle", Guid.NewGuid().ToString("N"));
        var sourcePixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var target = Assert.Single(
            CompressionTargetFormats.All,
            format => format.TransferSyntax == DicomTransferSyntax.RLELossless);

        try
        {
            var result = Assert.Single(new DicomCompressionTool().Compress(inputPath, outputDirectory, target));

            Assert.Equal(CompressionResultStatus.Success, result.Status);
            Assert.NotNull(result.OutputSize);
            Assert.True(File.Exists(result.Item.OutputPath));

            var compressed = DicomFile.Open(result.Item.OutputPath, FileReadOption.ReadAll);
            var compressedPixelData = DicomPixelData.Create(compressed.Dataset);
            var decoded = new DicomTranscoder(DicomTransferSyntax.RLELossless, DicomTransferSyntax.ExplicitVRLittleEndian)
                .Transcode(compressed.Dataset);
            Assert.Equal(DicomTransferSyntax.RLELossless, compressedPixelData.Syntax);
            PixelDataAssertions.FramesMatchExactly(sourcePixelData, DicomPixelData.Create(decoded));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
