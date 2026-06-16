using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeHtJpeg2000LosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomHtJpeg2000LosslessCodec;
using PureHtJpeg2000LosslessCodec = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LosslessCodec;
using PureHtJpeg2000LosslessRpclCodec = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LosslessRpclCodec;
using PureHtJpeg2000LossyCodec = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LossyCodec;

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

    [Fact]
    public void Htj2k_lossless_large_monochrome_16_bit_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(CreateSmoothMonochrome16(rows: 459, columns: 888));
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
    public void Htj2k_lossless_rpcl_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLosslessRPCL);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LosslessRpclCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2KLosslessRPCL,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchExactly(source, decodedPixelData);
    }

    [Fact]
    public void Htj2k_lossy_output_decodes_with_fo_dicom_codecs_native_decoder()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2K);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LossyCodec();

        codec.Encode(source, compressedPixelData, new Jpeg2000.DicomHtJpeg2000Params { TargetRatio = 3.0 });

        Assert.False(ReadUsesReversibleTransform(compressedPixelData.GetFrame(0).Data));
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2K,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchWithinTolerance(source, decodedPixelData, tolerance: 8);
    }

    [Fact]
    public void Htj2k_lossy_matrix_rgb_output_decodes_with_native_decoder_tighter_than_pure_matrix_tolerance()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var compressedDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2K);
        var compressedPixelData = DicomPixelData.Create(compressedDataset, true);
        var codec = new PureHtJpeg2000LossyCodec();

        codec.Encode(source, compressedPixelData, codec.GetDefaultParameters());

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var decodedDataset = new DicomTranscoder(
                DicomTransferSyntax.HTJ2K,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decodedPixelData = DicomPixelData.Create(decodedDataset);

        PixelDataAssertions.FramesMatchWithinTolerance(source, decodedPixelData, tolerance: 8);
    }

    [Fact]
    public void Htj2k_lossy_large_monochrome_16_bit_output_is_smaller_than_lossless()
    {
        var source = DicomPixelData.Create(CreateSmoothMonochrome16(rows: 459, columns: 888));
        var losslessDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2KLossless);
        var losslessPixelData = DicomPixelData.Create(losslessDataset, true);
        var lossyDataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.HTJ2K);
        var lossyPixelData = DicomPixelData.Create(lossyDataset, true);

        new PureHtJpeg2000LosslessCodec().Encode(source, losslessPixelData, new Jpeg2000.DicomHtJpeg2000Params());
        new PureHtJpeg2000LossyCodec().Encode(source, lossyPixelData, new Jpeg2000.DicomHtJpeg2000Params { TargetRatio = 16 });

        Assert.True(
            lossyPixelData.GetFrame(0).Size < losslessPixelData.GetFrame(0).Size,
            $"HTJ2K lossy frame should be smaller than lossless. Lossy={lossyPixelData.GetFrame(0).Size}, lossless={losslessPixelData.GetFrame(0).Size}.");
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

    private static DicomDataset CreateSmoothMonochrome16(ushort rows, ushort columns)
    {
        var frame = new byte[rows * columns * 2];
        var offset = 0;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var value = (ushort)((x * 17 + y * 11 + ((x * y) >> 5)) & 0xFFFF);
                frame[offset++] = (byte)value;
                frame[offset++] = (byte)(value >> 8);
            }
        }

        return DicomPixelDataFixtures.CreateMonochrome16(rows, columns, frame);
    }

    private static bool ReadUsesReversibleTransform(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment).Transformation == 1;
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }
}
