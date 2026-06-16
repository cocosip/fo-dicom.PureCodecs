using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeHtJpeg2000LosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomHtJpeg2000LosslessCodec;
using PureHtJpeg2000LosslessCodec = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LosslessCodec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000HtNativeCompatibilityTests
{
    [Fact]
    public void Fo_dicom_codecs_native_htj2k_lossless_output_decodes_with_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decodedPixelData = DicomPixelData.Create(decodedDataset, true);
        var codec = new NativeHtJpeg2000LosslessCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decodedPixelData);
    }

    [Fact]
    public void Htj2k_lossless_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LosslessCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2KLossless,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchExactly(source, decodedPixelData);
    }

    [Fact]
    public void Htj2k_lossless_monochrome_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LosslessCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2KLossless,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchExactly(source, decodedPixelData);
    }

    [Fact]
    public void Htj2k_lossless_small_monochrome_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LosslessCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2KLossless,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchExactly(source, decodedPixelData);
    }

    private static DicomDataset CloneForTransferSyntax(DicomDataset source, DicomTransferSyntax transferSyntax)
    {
        var clone = new DicomDataset(transferSyntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }
}
