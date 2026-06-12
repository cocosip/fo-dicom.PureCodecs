using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using FellowOakDicom.PureCodecs;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class PureTranscoderManagerTests
{
    public static TheoryData<DicomTransferSyntax, Type> Phase1Codecs => new()
    {
        { DicomTransferSyntax.RLELossless, typeof(DicomRleLosslessCodec) },
        { DicomTransferSyntax.JPEGProcess1, typeof(DicomJpegProcess1Codec) },
        { DicomTransferSyntax.JPEGProcess2_4, typeof(DicomJpegProcess2_4Codec) },
        { DicomTransferSyntax.JPEGProcess14, typeof(DicomJpegLossless14Codec) },
        { DicomTransferSyntax.JPEGProcess14SV1, typeof(DicomJpegLossless14SV1Codec) },
        { DicomTransferSyntax.JPEGLSLossless, typeof(DicomJpegLsLosslessCodec) },
        { DicomTransferSyntax.JPEGLSNearLossless, typeof(DicomJpegLsNearLosslessCodec) },
        { DicomTransferSyntax.JPEG2000Lossless, typeof(DicomJpeg2000LosslessCodec) },
        { DicomTransferSyntax.JPEG2000Lossy, typeof(DicomJpeg2000LossyCodec) },
        { DicomTransferSyntax.HTJ2KLossless, typeof(DicomHtJpeg2000LosslessCodec) },
        { DicomTransferSyntax.HTJ2KLosslessRPCL, typeof(DicomHtJpeg2000LosslessRpclCodec) },
        { DicomTransferSyntax.HTJ2K, typeof(DicomHtJpeg2000LossyCodec) },
    };

    public static TheoryData<DicomTransferSyntax, Type> UnimplementedPhase1Codecs => new()
    {
        { DicomTransferSyntax.JPEGLSLossless, typeof(DicomJpegLsLosslessCodec) },
        { DicomTransferSyntax.JPEGLSNearLossless, typeof(DicomJpegLsNearLosslessCodec) },
        { DicomTransferSyntax.JPEG2000Lossless, typeof(DicomJpeg2000LosslessCodec) },
        { DicomTransferSyntax.JPEG2000Lossy, typeof(DicomJpeg2000LossyCodec) },
        { DicomTransferSyntax.HTJ2KLossless, typeof(DicomHtJpeg2000LosslessCodec) },
        { DicomTransferSyntax.HTJ2KLosslessRPCL, typeof(DicomHtJpeg2000LosslessRpclCodec) },
        { DicomTransferSyntax.HTJ2K, typeof(DicomHtJpeg2000LossyCodec) },
    };

    [Fact]
    public void Constructor_creates_transcoder_manager()
    {
        var manager = new PureTranscoderManager();

        Assert.IsAssignableFrom<TranscoderManager>(manager);
    }

    [Theory]
    [MemberData(nameof(Phase1Codecs))]
    public void HasCodec_returns_true_for_phase_1_transfer_syntaxes(DicomTransferSyntax syntax, Type _)
    {
        var manager = new PureTranscoderManager();

        Assert.True(manager.HasCodec(syntax));
    }

    [Theory]
    [MemberData(nameof(Phase1Codecs))]
    public void GetCodec_returns_expected_codec_for_phase_1_transfer_syntaxes(DicomTransferSyntax syntax, Type codecType)
    {
        var manager = new PureTranscoderManager();

        var codec = manager.GetCodec(syntax);

        Assert.IsType(codecType, codec);
        Assert.Same(syntax, codec.TransferSyntax);
        Assert.Equal(syntax.UID.Name, codec.Name);
        Assert.NotNull(codec.GetDefaultParameters());
    }

    [Theory]
    [MemberData(nameof(Phase1Codecs))]
    public void CanTranscode_from_raw_to_phase_1_transfer_syntaxes(DicomTransferSyntax syntax, Type _)
    {
        var manager = new PureTranscoderManager();

        Assert.True(manager.CanTranscode(DicomTransferSyntax.ExplicitVRLittleEndian, syntax));
    }

    [Theory]
    [MemberData(nameof(Phase1Codecs))]
    public void CanTranscode_from_phase_1_transfer_syntaxes_to_raw(DicomTransferSyntax syntax, Type _)
    {
        var manager = new PureTranscoderManager();

        Assert.True(manager.CanTranscode(syntax, DicomTransferSyntax.ExplicitVRLittleEndian));
    }

    [Theory]
    [MemberData(nameof(UnimplementedPhase1Codecs))]
    public void Stub_codecs_throw_dicom_codec_exception_for_encode_and_decode(DicomTransferSyntax syntax, Type _)
    {
        var codec = new PureTranscoderManager().GetCodec(syntax);

        var encode = Assert.Throws<DicomCodecException>(
            () => codec.Encode(oldPixelData: null!, newPixelData: null!, parameters: null!));
        var decode = Assert.Throws<DicomCodecException>(
            () => codec.Decode(oldPixelData: null!, newPixelData: null!, parameters: null!));

        Assert.Contains(syntax.UID.Name, encode.Message);
        Assert.Contains("encode", encode.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(syntax.UID.Name, decode.Message);
        Assert.Contains("decode", decode.Message, StringComparison.OrdinalIgnoreCase);
    }
}
