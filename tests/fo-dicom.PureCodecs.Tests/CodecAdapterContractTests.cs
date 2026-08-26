using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class CodecAdapterContractTests
{
    [Theory]
    [InlineData("jpeg")]
    [InlineData("jpeg-lossless")]
    [InlineData("jpeg-ls")]
    [InlineData("jpeg2000")]
    [InlineData("htj2k")]
    public void Multi_frame_codec_outputs_use_file_backed_buffers(string codecName)
    {
        var (syntax, codec) = CreateCodec(codecName);
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMultiFrameMonochrome8(
            rows: 8,
            columns: 8,
            frameCount: 2));
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(source.Dataset, syntax), true);

        codec.Encode(source, compressed, codec.GetDefaultParameters());

        Assert.All(Enumerable.Range(0, compressed.NumberOfFrames), frame =>
            Assert.False(compressed.GetFrame(frame).IsMemory));

        var decoded = DicomPixelData.Create(
            CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian),
            true);
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.All(Enumerable.Range(0, decoded.NumberOfFrames), frame =>
            Assert.False(decoded.GetFrame(frame).IsMemory));
    }

    [Fact]
    public void Dicom_transcoder_marks_sequential_jpeg_rgb_decode_as_interleaved_rgb()
    {
        RegisterPureTranscoder();
        var source = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16);
        var compressed = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.JPEGProcess1).Transcode(source);

        Assert.Equal(PhotometricInterpretation.YbrFull422, DicomPixelData.Create(compressed).PhotometricInterpretation);

        var decoded = new DicomTranscoder(
            DicomTransferSyntax.JPEGProcess1,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(compressed);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(PhotometricInterpretation.Rgb, decodedPixelData.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, decodedPixelData.PlanarConfiguration);
    }

    [Fact]
    public void Dicom_transcoder_marks_classic_jpeg2000_mct_decode_as_interleaved_rgb()
    {
        RegisterPureTranscoder();
        var source = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16);
        var compressed = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.JPEG2000Lossless).Transcode(source);

        Assert.Equal(PhotometricInterpretation.YbrRct, DicomPixelData.Create(compressed).PhotometricInterpretation);

        var decoded = new DicomTranscoder(
            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(compressed);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(PhotometricInterpretation.Rgb, decodedPixelData.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, decodedPixelData.PlanarConfiguration);
    }

    [Theory]
    [InlineData("YBR_FULL")]
    [InlineData("YBR_FULL_422")]
    [InlineData("YBR_PARTIAL_422")]
    public void Dicom_transcoder_marks_classic_jpeg2000_ybr_decode_as_interleaved_rgb(string photometricInterpretation)
    {
        RegisterPureTranscoder();
        var source = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16);
        var compressed = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.JPEG2000Lossless).Transcode(source);
        compressed.AddOrUpdate(DicomTag.PhotometricInterpretation, photometricInterpretation);

        var decoded = new DicomTranscoder(
            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(compressed);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(PhotometricInterpretation.Rgb, decodedPixelData.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, decodedPixelData.PlanarConfiguration);
    }

    [Fact]
    public void Lossless_jpeg_ybr_full_decode_uses_the_rgb_contract()
    {
        RegisterPureTranscoder();
        var source = CreateColorDataset(
            PhotometricInterpretation.YbrFull,
            columns: 2,
            new byte[] { 100, 128, 128, 76, 84, 255 });
        var compressed = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.JPEGProcess14SV1).Transcode(source);

        var decoded = new DicomTranscoder(
            DicomTransferSyntax.JPEGProcess14SV1,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(compressed);
        var decodedPixelData = DicomPixelData.Create(decoded);

        Assert.Equal(PhotometricInterpretation.Rgb, decodedPixelData.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, decodedPixelData.PlanarConfiguration);
        Assert.Equal(new byte[] { 100, 100, 100, 254, 0, 0 }, decodedPixelData.GetFrame(0).Data);
    }

    [Fact]
    public void Lossless_jpeg_ybr_full_422_encode_expands_samples_and_marks_rgb()
    {
        RegisterPureTranscoder();
        var source = CreateColorDataset(
            PhotometricInterpretation.YbrFull422,
            columns: 2,
            new byte[] { 100, 150, 128, 128 });

        var compressed = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.JPEGProcess14SV1).Transcode(source);
        var compressedPixelData = DicomPixelData.Create(compressed);

        Assert.Equal(PhotometricInterpretation.Rgb, compressedPixelData.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, compressedPixelData.PlanarConfiguration);

        var decoded = new DicomTranscoder(
            DicomTransferSyntax.JPEGProcess14SV1,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(compressed);

        Assert.Equal(
            new byte[] { 100, 100, 100, 150, 150, 150 },
            DicomPixelData.Create(decoded).GetFrame(0).Data);
    }

    private static (DicomTransferSyntax Syntax, IDicomCodec Codec) CreateCodec(string codecName) => codecName switch
    {
        "jpeg" => (DicomTransferSyntax.JPEGProcess1, new DicomJpegProcess1Codec()),
        "jpeg-lossless" => (DicomTransferSyntax.JPEGProcess14SV1, new DicomJpegLossless14SV1Codec()),
        "jpeg-ls" => (DicomTransferSyntax.JPEGLSLossless, new DicomJpegLsLosslessCodec()),
        "jpeg2000" => (DicomTransferSyntax.JPEG2000Lossless, new DicomJpeg2000LosslessCodec()),
        "htj2k" => (DicomTransferSyntax.HTJ2KLossless, new DicomHtJpeg2000LosslessCodec()),
        _ => throw new ArgumentOutOfRangeException(nameof(codecName))
    };

    private static DicomDataset CloneForTransferSyntax(DicomDataset source, DicomTransferSyntax syntax)
    {
        var clone = new DicomDataset(syntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }

    private static DicomDataset CreateColorDataset(
        PhotometricInterpretation photometricInterpretation,
        ushort columns,
        byte[] frame)
    {
        return DicomPixelDataFixtures.CreateBaseDataset(
            rows: 1,
            columns,
            samplesPerPixel: 3,
            photometricInterpretation,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: PlanarConfiguration.Interleaved,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            frame);
    }

    private static void RegisterPureTranscoder()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();
    }
}
