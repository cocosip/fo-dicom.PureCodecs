using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using CoreHtJpeg2000Params = FellowOakDicom.Imaging.Codec.DicomHtJpeg2000Params;
using CoreJpeg2000Params = FellowOakDicom.Imaging.Codec.DicomJpeg2000Params;
using NativeJpeg2000LosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000LosslessCodec;
using PureHtJpeg2000Params = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000Params;
using PureJpeg2000Params = FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000Params;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000DicomIntegrationTests
{
    [Fact]
    public void Jpeg2000_default_parameters_match_fo_dicom_codecs_contract()
    {
        var codec = new DicomJpeg2000LossyCodec();

        var parameters = Assert.IsType<PureJpeg2000Params>(codec.GetDefaultParameters());

        Assert.True(parameters.Irreversible);
        Assert.Equal(20, parameters.Rate);
        Assert.Equal(new[] { 1280, 640, 320, 160, 80, 40, 20, 10, 5 }, parameters.RateLevels);
        Assert.Equal(Jpeg2000ProgressionOrder.LRCP, parameters.ProgressionOrder);
        Assert.False(parameters.IsVerbose);
        Assert.True(parameters.AllowMCT);
        Assert.True(parameters.UpdatePhotometricInterpretation);
        Assert.False(parameters.EncodeSignedPixelValuesAsUnsigned);
    }

    [Fact]
    public void Htj2k_lossless_default_parameters_match_openjph_wrapper()
    {
        var codec = new DicomHtJpeg2000LosslessCodec();

        var parameters = Assert.IsType<PureHtJpeg2000Params>(codec.GetDefaultParameters());

        Assert.Equal(ProgressionOrder.RPCL, parameters.ProgressionOrder);
    }

    [Fact]
    public void Jpeg2000_accepts_fo_dicom_core_parameters()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();
        var parameters = new CoreJpeg2000Params
        {
            Irreversible = false,
            Rate = 12,
            RateLevels = new[] { 32, 16, 8 },
            AllowMCT = false,
            UpdatePhotometricInterpretation = false,
            EncodeSignedPixelValuesAsUnsigned = true
        };

        codec.Encode(source, compressed, parameters);

        Assert.Equal(Jpeg2000ProgressionOrder.LRCP, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_pure_parameters_reject_unsupported_progression_order()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();
        var parameters = new PureJpeg2000Params
        {
            Irreversible = false,
            ProgressionOrder = Jpeg2000ProgressionOrder.RPCL
        };

        var exception = Assert.Throws<DicomCodecException>(() => codec.Encode(source, compressed, parameters));

        Assert.Contains("LRCP", exception.Message);
    }

    [Fact]
    public void Jpeg2000_irreversible_parameter_controls_cod_transform()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params { Irreversible = false });

        Assert.False(ReadUsesIrreversibleTransform(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_lossless_null_parameters_use_reversible_default()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, parameters: null!);

        Assert.False(ReadUsesIrreversibleTransform(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_rate_controls_lossy_quantization_tolerance()
    {
        var sourceFrame = Enumerable.Range(0, 64 * 64).Select(index => (byte)((index * 13) % 251)).ToArray();
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64, frame: sourceFrame);
        var source = DicomPixelData.Create(dataset);
        var lowerCompressionDecoded = EncodeDecodeJpeg2000Lossy(source, new PureJpeg2000Params { Irreversible = true, Rate = 5 });
        var higherCompressionDecoded = EncodeDecodeJpeg2000Lossy(source, new PureJpeg2000Params { Irreversible = true, Rate = 20 });

        Assert.True(MaxByteDifference(source.GetFrame(0).Data, higherCompressionDecoded.GetFrame(0).Data) >
            MaxByteDifference(source.GetFrame(0).Data, lowerCompressionDecoded.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_rate_levels_control_cod_layer_count()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = true,
            Rate = 20,
            RateLevels = new[] { 1280, 640, 20, 10 }
        });

        Assert.Equal(3, ReadLayerCount(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_target_ratio_num_layers_control_lossy_cod_layer_count()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy),
            true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = true,
            TargetRatio = 3,
            NumLayers = 2
        });

        Assert.Equal(2, ReadLayerCount(compressed.GetFrame(0).Data));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    public void Jpeg2000_rejects_invalid_nonzero_target_ratio(double targetRatio)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy),
            true);
        var codec = new DicomJpeg2000LossyCodec();

        var exception = Assert.Throws<DicomCodecException>(() => codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = true,
            TargetRatio = targetRatio,
            NumLayers = 1
        }));

        Assert.Contains("TargetRatio", exception.Message);
    }

    [Fact]
    public void Jpeg2000_rejects_target_ratio_layer_count_that_exceeds_cod_limit()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy),
            true);
        var codec = new DicomJpeg2000LossyCodec();

        var exception = Assert.Throws<DicomCodecException>(() => codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = true,
            TargetRatio = 2,
            NumLayers = ushort.MaxValue + 1
        }));

        Assert.Contains("65535", exception.Message);
    }

    [Fact]
    public void Jpeg2000_target_ratio_lossless_layers_include_final_lossless_layer()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless),
            true);
        var nativeDecoded = DicomPixelData.Create(
            CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian),
            true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = false,
            TargetRatio = 3,
            NumLayers = 2,
            IncludeFinalLosslessLayer = true
        });

        Assert.Equal(3, ReadLayerCount(compressed.GetFrame(0).Data));
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, nativeDecoded);
    }

    [Fact]
    public void Jpeg2000_default_lossy_rate_levels_write_fo_dicom_codecs_layer_count()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());

        Assert.Equal(7, ReadLayerCount(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_default_lossless_rate_levels_include_final_lossless_layer()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());

        Assert.Equal(8, ReadLayerCount(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_standard_encoder_round_trips_rgb_interleaved_and_planar_losslessly()
    {
        AssertRgbLosslessRoundTrip(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 2, columns: 2));
        AssertRgbLosslessRoundTrip(DicomPixelDataFixtures.CreateRgbPlanar(rows: 2, columns: 2));
    }

    [Fact]
    public void Jpeg2000_lossless_ybr_full_output_decodes_as_rgb_with_fo_dicom_codecs()
    {
        const ushort rows = 64;
        const ushort columns = 64;
        var (ybrFrame, expectedRgb) = CreateNeutralYbrFullFrame(rows, columns);
        var source = CreateYbrPixelData(
            PhotometricInterpretation.YbrFull,
            rows,
            columns,
            ybrFrame);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.JPEG2000Lossless),
            true);
        var decoded = CreateRgbInterleavedTarget(source);
        var pureCodec = new DicomJpeg2000LosslessCodec();

        pureCodec.Encode(source, compressed, CreateSingleLayerLosslessParameters());
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, decoded, nativeCodec.GetDefaultParameters());

        Assert.True(ReadUsesMultipleComponentTransform(compressed.GetFrame(0).Data));
        Assert.Equal(PhotometricInterpretation.YbrRct, compressed.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, compressed.PlanarConfiguration);
        Assert.Equal(expectedRgb, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Jpeg2000_lossless_ybr_full_mct_updates_stale_photometric_even_when_disabled()
    {
        const ushort rows = 64;
        const ushort columns = 64;
        var (ybrFrame, expectedRgb) = CreateNeutralYbrFullFrame(rows, columns);
        var source = CreateYbrPixelData(PhotometricInterpretation.YbrFull, rows, columns, ybrFrame);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.JPEG2000Lossless),
            true);
        var decoded = CreateRgbInterleavedTarget(source);
        var parameters = CreateSingleLayerLosslessParameters();
        parameters.UpdatePhotometricInterpretation = false;
        var pureCodec = new DicomJpeg2000LosslessCodec();

        pureCodec.Encode(source, compressed, parameters);
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, decoded, nativeCodec.GetDefaultParameters());

        Assert.True(ReadUsesMultipleComponentTransform(compressed.GetFrame(0).Data));
        Assert.Equal(PhotometricInterpretation.YbrRct, compressed.PhotometricInterpretation);
        Assert.Equal(expectedRgb, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Jpeg2000_lossless_ybr_full_422_output_decodes_as_rgb_with_fo_dicom_codecs()
    {
        const ushort rows = 64;
        const ushort columns = 64;
        var (ybrFrame, expectedRgb) = CreateNeutralYbrFull422Frame(rows, columns);
        var source = CreateYbrPixelData(
            PhotometricInterpretation.YbrFull422,
            rows,
            columns,
            ybrFrame);
        var compressed = DicomPixelData.Create(
            CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.JPEG2000Lossless),
            true);
        var decoded = CreateRgbInterleavedTarget(source);
        var pureCodec = new DicomJpeg2000LosslessCodec();

        pureCodec.Encode(source, compressed, CreateSingleLayerLosslessParameters());
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, decoded, nativeCodec.GetDefaultParameters());

        Assert.True(ReadUsesMultipleComponentTransform(compressed.GetFrame(0).Data));
        Assert.Equal(PhotometricInterpretation.YbrRct, compressed.PhotometricInterpretation);
        Assert.Equal(expectedRgb, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Jpeg2000_allow_mct_rgb_encode_writes_mct_and_updates_photometric()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 2, columns: 2);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = true,
            AllowMCT = true,
            UpdatePhotometricInterpretation = true
        });

        Assert.True(ReadUsesMultipleComponentTransform(compressed.GetFrame(0).Data));
        Assert.Equal(PhotometricInterpretation.YbrIct, compressed.PhotometricInterpretation);
    }

    [Fact]
    public void Jpeg2000_allow_mct_false_rgb_encode_keeps_rgb_without_mct()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 2, columns: 2);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params { Irreversible = false, AllowMCT = false });

        Assert.False(ReadUsesMultipleComponentTransform(compressed.GetFrame(0).Data));
        Assert.Equal(PhotometricInterpretation.Rgb, compressed.PhotometricInterpretation);
    }

    [Fact]
    public void Jpeg2000_encode_signed_pixel_values_as_unsigned_clears_siz_signed_flag()
    {
        var dataset = CreateSignedMonochrome16();
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, new PureJpeg2000Params
        {
            Irreversible = false,
            EncodeSignedPixelValuesAsUnsigned = true
        });

        Assert.False(ReadSizeSegment(compressed.GetFrame(0).Data).Components[0].IsSigned);
    }

    [Fact]
    public void Htj2k_lossless_ignores_core_progression_parameters_like_fo_dicom_codecs()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();
        var parameters = new CoreHtJpeg2000Params
        {
            ProgressionOrder = ProgressionOrder.CPRL
        };

        codec.Encode(source, compressed, parameters);

        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Htj2k_lossless_core_default_parameters_use_openjph_default_progression()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, new CoreHtJpeg2000Params());

        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Jpeg2000_multiframe_data_round_trips_and_preserves_frame_count()
    {
        var dataset = DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 2, columns: 3, frameCount: 3);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.AssertFrameCount(source, compressed);
        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Jpeg2000_lossless_preserves_frame_count_and_required_compression_tags()
    {
        AssertPreservesCompressionTags(
            DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 2, columns: 3, frameCount: 2),
            DicomTransferSyntax.JPEG2000Lossless,
            new DicomJpeg2000LosslessCodec());
    }

    [Fact]
    public void Jpeg2000_lossless_accepts_palette_color_index_data()
    {
        var dataset = DicomPixelDataFixtures.CreatePaletteColor8(rows: 2, columns: 3);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal("PALETTE COLOR", compressed.PhotometricInterpretation.Value);
        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Jpeg2000_lossy_accepts_palette_color_index_data()
    {
        var dataset = DicomPixelDataFixtures.CreatePaletteColor8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal("PALETTE COLOR", compressed.PhotometricInterpretation.Value);
        PixelDataAssertions.AssertFrameCount(source, compressed);
        Assert.Equal(source.GetFrame(0).Data.Length, decoded.GetFrame(0).Data.Length);
    }

    [Fact]
    public void Jpeg2000_lossy_preserves_frame_count_and_required_compression_tags()
    {
        AssertPreservesCompressionTags(
            DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 2, columns: 3, frameCount: 2),
            DicomTransferSyntax.JPEG2000Lossy,
            new DicomJpeg2000LossyCodec());
    }

    [Fact]
    public void Htj2k_preserves_frame_count_and_required_compression_tags()
    {
        AssertPreservesCompressionTags(
            DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 2, columns: 3, frameCount: 2),
            DicomTransferSyntax.HTJ2KLossless,
            new DicomHtJpeg2000LosslessCodec());
    }

    [Fact]
    public void Jpeg2000_invalid_codestream_throws_managed_exception()
    {
        var compressedPixelData = CreateCompressedPixelDataWithFrame(
            DicomTransferSyntax.JPEG2000Lossless,
            new byte[] { 0xFF, Jpeg2000Marker.SOC, 0xFF, Jpeg2000Marker.EOC });
        var target = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), true);
        var codec = new DicomJpeg2000LosslessCodec();

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressedPixelData, target, codec.GetDefaultParameters()));

        Assert.Contains("JPEG 2000", exception.Message);
    }

    [Fact]
    public void Htj2k_invalid_codestream_throws_managed_exception()
    {
        var compressedPixelData = CreateCompressedPixelDataWithFrame(
            DicomTransferSyntax.HTJ2KLossless,
            new byte[] { 0xFF, Jpeg2000Marker.SOC, 0xFF, Jpeg2000Marker.EOC });
        var target = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressedPixelData, target, codec.GetDefaultParameters()));

        Assert.Contains("HTJ2K", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jpeg2000_parser_resolves_packed_packet_headers(bool usePpm)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var packed = new byte[] { 0xAA, 0xBB, 0xCC };
        var frame = usePpm
            ? InsertSegmentBeforeMarker(
                encoded.GetFrame(0).Data,
                Jpeg2000Marker.SOT,
                Jpeg2000Marker.PPM,
                new byte[] { 0x00, 0x00, 0x00, 0x00, 0x03, 0xAA, 0xBB, 0xCC })
            : InsertTileHeaderSegmentAndAdjustPsot(
                encoded.GetFrame(0).Data,
                Jpeg2000Marker.PPT,
                new byte[] { 0x00, 0xAA, 0xBB, 0xCC });
        var parserType = typeof(Jpeg2000Marker).Assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000CodestreamParser",
            throwOnError: true)!;
        var parsed = parserType.GetMethod(
            "ParseSingleTilePart",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { frame, "JPEG 2000", "JPEG 2000" })!;
        var property = parsed.GetType().GetProperty("PackedPacketHeaders");

        Assert.NotNull(property);
        Assert.Equal(packed, Assert.IsType<byte[]>(property!.GetValue(parsed)));
    }

    [Theory]
    [InlineData(Jpeg2000Marker.PPM)]
    [InlineData(Jpeg2000Marker.PPT)]
    public void Jpeg2000_decode_rejects_packed_packet_header_marker_in_wrong_header(byte marker)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var frame = marker == Jpeg2000Marker.PPM
            ? InsertTileHeaderSegmentAndAdjustPsot(encoded.GetFrame(0).Data, marker, new byte[] { 0x00, 0x01 })
            : InsertSegmentBeforeMarker(encoded.GetFrame(0).Data, Jpeg2000Marker.SOT, marker, new byte[] { 0x00, 0x01 });
        var compressed = CreateCompressedPixelDataWithFrame(source, codec.TransferSyntax, frame);
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.Contains(marker == Jpeg2000Marker.PPM ? "main header" : "tile-part header", exception.Message);
    }

    [Fact]
    public void Jpeg2000_decode_rejects_ppm_and_ppt_in_the_same_codestream()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var withPpm = InsertSegmentBeforeMarker(
            encoded.GetFrame(0).Data,
            Jpeg2000Marker.SOT,
            Jpeg2000Marker.PPM,
            new byte[] { 0x00, 0x00 });
        var frame = InsertTileHeaderSegmentAndAdjustPsot(
            withPpm,
            Jpeg2000Marker.PPT,
            new byte[] { 0x00, 0x01 });
        var compressed = CreateCompressedPixelDataWithFrame(source, codec.TransferSyntax, frame);
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.Contains("both PPM and PPT", exception.Message);
    }

    [Fact]
    public void Jpeg2000_parser_resolves_main_header_rgn_shift_per_component()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var frame = InsertSegmentBeforeMarker(
            encoded.GetFrame(0).Data,
            Jpeg2000Marker.SOT,
            Jpeg2000Marker.RGN,
            new byte[] { 0x00, 0x00, 0x08 });
        var parserType = typeof(Jpeg2000Marker).Assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000CodestreamParser",
            throwOnError: true)!;
        var parsed = parserType.GetMethod(
            "ParseSingleTilePart",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { frame, "JPEG 2000", "JPEG 2000" })!;
        var shiftsProperty = parsed.GetType().GetProperty("RegionOfInterestShifts");

        Assert.NotNull(shiftsProperty);
        var shifts = Assert.IsAssignableFrom<IReadOnlyList<int>>(shiftsProperty!.GetValue(parsed));
        Assert.Equal(new[] { 8 }, shifts);
    }

    [Fact]
    public void Jpeg2000_parser_prefers_tile_header_rgn_over_main_header_rgn()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var withMainRgn = InsertSegmentBeforeMarker(
            encoded.GetFrame(0).Data,
            Jpeg2000Marker.SOT,
            Jpeg2000Marker.RGN,
            new byte[] { 0x00, 0x00, 0x08 });
        var frame = InsertTileHeaderSegmentAndAdjustPsot(
            withMainRgn,
            Jpeg2000Marker.RGN,
            new byte[] { 0x00, 0x00, 0x05 });
        var parserType = typeof(Jpeg2000Marker).Assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000CodestreamParser",
            throwOnError: true)!;
        var parsed = parserType.GetMethod(
            "ParseSingleTilePart",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { frame, "JPEG 2000", "JPEG 2000" })!;
        var shifts = Assert.IsAssignableFrom<IReadOnlyList<int>>(
            parsed.GetType().GetProperty("RegionOfInterestShifts")!.GetValue(parsed));

        Assert.Equal(new[] { 5 }, shifts);
    }

    [Theory]
    [InlineData(Jpeg2000Marker.RGN)]
    [InlineData(Jpeg2000Marker.PPM)]
    [InlineData(Jpeg2000Marker.PPT)]
    public void Jpeg2000_pure_and_native_decode_semantic_marker_codestream(byte marker)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1);
        var source = DicomPixelData.Create(dataset);
        var codestream = CreateSinglePixelSemanticMarkerCodestream(marker);
        var compressed = CreateCompressedPixelDataWithFrame(source, DicomTransferSyntax.JPEG2000Lossless, codestream);
        var pureTarget = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var nativeTarget = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var pureCodec = new DicomJpeg2000LosslessCodec();
        pureCodec.Decode(compressed, pureTarget, pureCodec.GetDefaultParameters());
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, nativeTarget, nativeCodec.GetDefaultParameters());

        Assert.Equal(new byte[] { 131 }, pureTarget.GetFrame(0).Data);
        PixelDataAssertions.FramesMatchExactly(pureTarget, nativeTarget);
    }

    [Fact]
    public void Jpeg2000_decode_applies_main_header_poc_covering_existing_lrcp_packets()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var frame = encoded.GetFrame(0).Data;
        var coding = ReadCodingStyle(frame);
        var poc = new byte[]
        {
            0,
            0,
            (byte)(coding.LayerCount >> 8),
            (byte)coding.LayerCount,
            (byte)(coding.DecompositionLevels + 1),
            1,
            (byte)Jpeg2000ProgressionOrder.LRCP,
        };
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            InsertSegmentBeforeMarker(frame, Jpeg2000Marker.SOT, Jpeg2000Marker.POC, poc));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var nativeTarget = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        codec.Decode(compressed, target, codec.GetDefaultParameters());
        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, nativeTarget, nativeCodec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, target);
        PixelDataAssertions.FramesMatchExactly(source, nativeTarget);
    }

    [Fact]
    public void Jpeg2000_decode_rejects_codestream_without_eoc()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            RemoveAtMarker(encoded.GetFrame(0).Data, Jpeg2000Marker.EOC));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.Contains("EOC", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Htj2k_decode_rejects_codestream_without_eoc()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomHtJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            RemoveAtMarker(encoded.GetFrame(0).Data, Jpeg2000Marker.EOC));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.Contains("EOC", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Htj2k_decode_excludes_tile_header_segments_from_psot_payload()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomHtJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            InsertTileHeaderCommentAndAdjustPsot(encoded.GetFrame(0).Data));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        codec.Decode(compressed, target, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, target);
    }

    [Fact]
    public void Jpeg2000_decode_rejects_siz_signedness_conflicting_with_dicom_metadata()
    {
        AssertRejectsSizeMetadataMismatch(
            new DicomJpeg2000LosslessCodec(),
            DicomTransferSyntax.JPEG2000Lossless,
            replacementSsiz: 0x87,
            expectedMessage: "signedness");
    }

    [Fact]
    public void Htj2k_decode_rejects_siz_precision_conflicting_with_dicom_metadata()
    {
        AssertRejectsSizeMetadataMismatch(
            new DicomHtJpeg2000LosslessCodec(),
            DicomTransferSyntax.HTJ2KLossless,
            replacementSsiz: 0x06,
            expectedMessage: "precision");
    }

    [Fact]
    public void Jpeg2000_decode_applies_main_header_coc_and_qcc_overrides()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, CreateSingleLayerLosslessParameters());
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            RewriteDefaultsWithComponentOverrides(encoded.GetFrame(0).Data, source.SamplesPerPixel));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        codec.Decode(compressed, target, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, target);
    }

    [Fact]
    public void Jpeg2000_decode_applies_tile_header_coc_and_qcc_overrides()
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var codec = new DicomJpeg2000LosslessCodec();
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, codec.TransferSyntax), true);
        codec.Encode(source, encoded, CreateSingleLayerLosslessParameters());
        var compressed = CreateCompressedPixelDataWithFrame(
            source,
            codec.TransferSyntax,
            InsertTileComponentOverrides(encoded.GetFrame(0).Data, source.SamplesPerPixel));
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        codec.Decode(compressed, target, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, target);
    }

    [Fact]
    public void Jpeg2000_rejects_unsupported_photometric_interpretation()
    {
        var pixelData = CreateUnsupportedPhotometricPixelData();
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(pixelData.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        var codec = new DicomJpeg2000LosslessCodec();

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Encode(pixelData, compressed, codec.GetDefaultParameters()));

        Assert.Contains("photometric", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jpeg2000_standard_decode_rejects_unsupported_component_subsampling()
    {
        var codestream = CreateCodestreamWithSubsampling(horizontalSeparation: 2, verticalSeparation: 1);
        var compressedPixelData = CreateCompressedPixelDataWithFrame(DicomTransferSyntax.JPEG2000Lossless, codestream);
        var target = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), true);
        var codec = new DicomJpeg2000LosslessCodec();

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressedPixelData, target, codec.GetDefaultParameters()));

        Assert.Contains("subsampling", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jpeg2000_standard_decode_accepts_non_lrcp_progression_order()
    {
        var codestream = CreateCodestreamWithProgressionOrder(Jpeg2000ProgressionOrder.RPCL);
        var compressedPixelData = CreateCompressedPixelDataWithFrame(DicomTransferSyntax.JPEG2000Lossless, codestream);
        var target = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Decode(compressedPixelData, target, codec.GetDefaultParameters());

        Assert.Equal(new byte[] { 128 }, target.GetFrame(0).Data);
    }

    private static Jpeg2000ProgressionOrder ReadProgressionOrder(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment).ProgressionOrder;
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }

    private static bool ReadUsesIrreversibleTransform(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment).Transformation == 0;
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }

    private static int ReadLayerCount(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment).LayerCount;
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }

    private static Jpeg2000CodingStyleDefault ReadCodingStyle(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment);
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }

    private static bool ReadUsesMultipleComponentTransform(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment).UsesMultipleComponentTransform;
            }
        }

        throw new Xunit.Sdk.XunitException("COD marker not found.");
    }

    private static Jpeg2000SizeSegment ReadSizeSegment(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.SIZ)
            {
                return Jpeg2000SizeSegment.Parse(segment);
            }
        }

        throw new Xunit.Sdk.XunitException("SIZ marker not found.");
    }

    private static DicomPixelData EncodeDecodeJpeg2000Lossy(DicomPixelData source, PureJpeg2000Params parameters)
    {
        var codec = new DicomJpeg2000LossyCodec();
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.JPEG2000Lossy), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        codec.Encode(source, compressed, parameters);
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());
        return decoded;
    }

    private static void AssertPreservesCompressionTags(DicomDataset dataset, DicomTransferSyntax syntax, IDicomCodec codec)
    {
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, syntax);
        var compressed = DicomPixelData.Create(compressedDataset, true);

        codec.Encode(source, compressed, codec.GetDefaultParameters());

        PixelDataAssertions.AssertFrameCount(source, compressed);
        PixelDataAssertions.AssertRequiredCompressionTags(compressedDataset, syntax);
    }

    private static void AssertRgbLosslessRoundTrip(DicomDataset dataset)
    {
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    private static int MaxByteDifference(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        var max = 0;
        for (var index = 0; index < expected.Length; index++)
        {
            max = System.Math.Max(max, System.Math.Abs(expected[index] - actual[index]));
        }

        return max;
    }

    private static DicomPixelData CreateUnsupportedPhotometricPixelData()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, "HSV" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)3 },
            { DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(new byte[] { 1, 2, 3 }));
        return pixelData;
    }

    private static DicomPixelData CreateRgbInterleavedTarget(DicomPixelData source)
    {
        var dataset = CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
        dataset.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        return DicomPixelData.Create(dataset, true);
    }

    private static PureJpeg2000Params CreateSingleLayerLosslessParameters()
    {
        return new PureJpeg2000Params
        {
            Irreversible = false,
            Rate = 0,
            RateLevels = Array.Empty<int>(),
            AllowMCT = true,
            UpdatePhotometricInterpretation = true
        };
    }

    private static DicomPixelData CreateYbrPixelData(
        PhotometricInterpretation photometricInterpretation,
        ushort rows,
        ushort columns,
        byte[] frame)
    {
        var dataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
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
        return DicomPixelData.Create(dataset);
    }

    private static (byte[] Ybr, byte[] Rgb) CreateNeutralYbrFullFrame(ushort rows, ushort columns)
    {
        var pixelCount = rows * columns;
        var ybr = new byte[pixelCount * 3];
        var rgb = new byte[pixelCount * 3];
        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            var value = (byte)((pixel * 17 + 11) % 251);
            ybr[pixel * 3] = value;
            ybr[pixel * 3 + 1] = 128;
            ybr[pixel * 3 + 2] = 128;
            rgb[pixel * 3] = value;
            rgb[pixel * 3 + 1] = value;
            rgb[pixel * 3 + 2] = value;
        }

        return (ybr, rgb);
    }

    private static (byte[] Ybr, byte[] Rgb) CreateNeutralYbrFull422Frame(ushort rows, ushort columns)
    {
        var pixelCount = rows * columns;
        var ybr = new byte[pixelCount * 2];
        var rgb = new byte[pixelCount * 3];
        for (var pixel = 0; pixel < pixelCount; pixel += 2)
        {
            var first = (byte)((pixel * 17 + 11) % 251);
            var second = (byte)(((pixel + 1) * 17 + 11) % 251);
            var packed = pixel * 2;
            ybr[packed] = first;
            ybr[packed + 1] = second;
            ybr[packed + 2] = 128;
            ybr[packed + 3] = 128;
            rgb[pixel * 3] = first;
            rgb[pixel * 3 + 1] = first;
            rgb[pixel * 3 + 2] = first;
            rgb[(pixel + 1) * 3] = second;
            rgb[(pixel + 1) * 3 + 1] = second;
            rgb[(pixel + 1) * 3 + 2] = second;
        }

        return (ybr, rgb);
    }

    private static DicomDataset CreateSignedMonochrome16()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)PixelRepresentation.Signed },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(
            new byte[] { 0xFF, 0xFF, 0x01, 0x00 }));
        return dataset;
    }

    private static DicomPixelData CreateCompressedPixelDataWithFrame(DicomTransferSyntax syntax, byte[] frame)
    {
        var dataset = CloneForTransferSyntax(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), syntax);
        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(frame));
        return pixelData;
    }

    private static DicomPixelData CreateCompressedPixelDataWithFrame(
        DicomPixelData source,
        DicomTransferSyntax syntax,
        byte[] frame)
    {
        var dataset = CloneForTransferSyntax(source.Dataset, syntax);
        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(frame));
        return pixelData;
    }

    private static byte[] InsertSegmentBeforeMarker(byte[] codestream, byte beforeMarker, byte marker, byte[] payload)
    {
        var offset = FindMarkerOffset(codestream, beforeMarker);
        var segmentLength = payload.Length + 4;
        var result = new byte[codestream.Length + segmentLength];
        Buffer.BlockCopy(codestream, 0, result, 0, offset);
        result[offset] = 0xFF;
        result[offset + 1] = marker;
        result[offset + 2] = (byte)((payload.Length + 2) >> 8);
        result[offset + 3] = (byte)(payload.Length + 2);
        Buffer.BlockCopy(payload, 0, result, offset + 4, payload.Length);
        Buffer.BlockCopy(codestream, offset, result, offset + segmentLength, codestream.Length - offset);
        return result;
    }

    private static byte[] RemoveAtMarker(byte[] codestream, byte marker)
    {
        var offset = FindMarkerOffset(codestream, marker);
        var result = new byte[offset];
        Buffer.BlockCopy(codestream, 0, result, 0, result.Length);
        return result;
    }

    private static byte[] InsertTileHeaderCommentAndAdjustPsot(byte[] codestream)
    {
        var sotOffset = FindMarkerOffset(codestream, Jpeg2000Marker.SOT);
        var sodOffset = FindMarkerOffset(codestream, Jpeg2000Marker.SOD, sotOffset + 2);
        var comment = new byte[] { 0xFF, Jpeg2000Marker.COM, 0x00, 0x04, 0x00, 0x01 };
        var result = new byte[codestream.Length + comment.Length];
        Buffer.BlockCopy(codestream, 0, result, 0, sodOffset);
        Buffer.BlockCopy(comment, 0, result, sodOffset, comment.Length);
        Buffer.BlockCopy(codestream, sodOffset, result, sodOffset + comment.Length, codestream.Length - sodOffset);

        var psotOffset = sotOffset + 6;
        var psot = ((uint)result[psotOffset] << 24)
            | ((uint)result[psotOffset + 1] << 16)
            | ((uint)result[psotOffset + 2] << 8)
            | result[psotOffset + 3];
        psot += (uint)comment.Length;
        result[psotOffset] = (byte)(psot >> 24);
        result[psotOffset + 1] = (byte)(psot >> 16);
        result[psotOffset + 2] = (byte)(psot >> 8);
        result[psotOffset + 3] = (byte)psot;
        return result;
    }

    private static byte[] InsertTileHeaderSegmentAndAdjustPsot(byte[] codestream, byte marker, byte[] payload)
    {
        var sotOffset = FindMarkerOffset(codestream, Jpeg2000Marker.SOT);
        var sodOffset = FindMarkerOffset(codestream, Jpeg2000Marker.SOD, sotOffset + 2);
        var segment = new byte[payload.Length + 4];
        segment[0] = 0xFF;
        segment[1] = marker;
        segment[2] = (byte)((payload.Length + 2) >> 8);
        segment[3] = (byte)(payload.Length + 2);
        Buffer.BlockCopy(payload, 0, segment, 4, payload.Length);
        var result = new byte[codestream.Length + segment.Length];
        Buffer.BlockCopy(codestream, 0, result, 0, sodOffset);
        Buffer.BlockCopy(segment, 0, result, sodOffset, segment.Length);
        Buffer.BlockCopy(codestream, sodOffset, result, sodOffset + segment.Length, codestream.Length - sodOffset);

        var psotOffset = sotOffset + 6;
        var psot = ((uint)result[psotOffset] << 24)
            | ((uint)result[psotOffset + 1] << 16)
            | ((uint)result[psotOffset + 2] << 8)
            | result[psotOffset + 3];
        psot += (uint)segment.Length;
        result[psotOffset] = (byte)(psot >> 24);
        result[psotOffset + 1] = (byte)(psot >> 16);
        result[psotOffset + 2] = (byte)(psot >> 8);
        result[psotOffset + 3] = (byte)psot;
        return result;
    }

    private static byte[] CreateSinglePixelSemanticMarkerCodestream(byte marker)
    {
        const int tier1FractionalBits = 6;
        const int bitPlaneCount = 2;
        const int passCount = (bitPlaneCount * 3) - 2;
        var assembly = typeof(Jpeg2000Marker).Assembly;
        var tier1Type = assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder",
            throwOnError: true)!;
        var tier1 = Activator.CreateInstance(tier1Type, 1, 1, 0, (byte)0)!;
        var tier1Data = (byte[])tier1Type.GetMethod("Encode", new[] { typeof(int[]), typeof(int) })!
            .Invoke(tier1, new object[] { new[] { 3 << tier1FractionalBits }, passCount })!;
        var blockType = assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock",
            throwOnError: true)!;
        var block = Activator.CreateInstance(blockType, 0, 0, 1, 1, 7, passCount, tier1Data)!;
        var blocks = Array.CreateInstance(blockType, 1);
        blocks.SetValue(block, 0);
        var packetEncoderType = assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder",
            throwOnError: true)!;
        var packet = (byte[])packetEncoderType.GetMethod("EncodeSingleLayerPacket")!
            .Invoke(null, new object[] { blocks })!;
        var packetHeaderLength = packet.Length - tier1Data.Length;
        var packetHeader = packet.Take(packetHeaderLength).ToArray();
        var packetBody = packet.Skip(packetHeaderLength).ToArray();
        Assert.Equal(tier1Data, packetBody);

        var payloadBuilderType = assembly.GetType(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000MarkerPayloadBuilder",
            throwOnError: true)!;
        var size = (byte[])payloadBuilderType.GetMethod(
            "CreateSize",
            new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int) })!
            .Invoke(null, new object[] { 1, 1, 8, false, 1 })!;
        var writer = new Jpeg2000CodestreamWriter();
        writer.WriteStandalone(Jpeg2000Marker.SOC);
        writer.WriteSegment(Jpeg2000Marker.SIZ, size);
        writer.WriteSegment(
            Jpeg2000Marker.COD,
            new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x04, 0x04, 0x00, 0x01 });
        writer.WriteSegment(Jpeg2000Marker.QCD, new byte[] { 0x40, 0x40 });
        if (marker == Jpeg2000Marker.RGN)
        {
            writer.WriteSegment(Jpeg2000Marker.RGN, new byte[] { 0x00, 0x00, 0x08 });
        }
        else if (marker == Jpeg2000Marker.PPM)
        {
            var ppm = new byte[packetHeader.Length + 5];
            ppm[0] = 0;
            WriteUInt32(ppm, 1, (uint)packetHeader.Length);
            Buffer.BlockCopy(packetHeader, 0, ppm, 5, packetHeader.Length);
            writer.WriteSegment(Jpeg2000Marker.PPM, ppm);
        }

        byte[]? ppt = null;
        if (marker == Jpeg2000Marker.PPT)
        {
            ppt = new byte[packetHeader.Length + 1];
            Buffer.BlockCopy(packetHeader, 0, ppt, 1, packetHeader.Length);
        }

        var tileData = marker == Jpeg2000Marker.RGN ? packet : packetBody;
        var tileHeaderLength = ppt == null ? 0 : ppt.Length + 4;
        var psot = checked((uint)(tileData.Length + tileHeaderLength + 14));
        var sot = new byte[8];
        WriteUInt32(sot, 2, psot);
        sot[7] = 1;
        writer.WriteSegment(Jpeg2000Marker.SOT, sot);
        if (ppt != null)
        {
            writer.WriteSegment(Jpeg2000Marker.PPT, ppt);
        }

        writer.WriteStandalone(Jpeg2000Marker.SOD);
        writer.WriteRaw(tileData);
        writer.WriteStandalone(Jpeg2000Marker.EOC);
        return writer.ToArray();
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static void AssertRejectsSizeMetadataMismatch(
        IDicomCodec codec,
        DicomTransferSyntax syntax,
        byte replacementSsiz,
        string expectedMessage)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var encoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, syntax), true);
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var codestream = encoded.GetFrame(0).Data.ToArray();
        var sizOffset = FindMarkerOffset(codestream, Jpeg2000Marker.SIZ);
        codestream[sizOffset + 40] = replacementSsiz;
        var compressed = CreateCompressedPixelDataWithFrame(source, syntax, codestream);
        var target = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] RewriteDefaultsWithComponentOverrides(byte[] codestream, int componentCount)
    {
        var original = codestream.ToArray();
        var mutated = codestream.ToArray();
        var codOffset = FindMarkerOffset(original, Jpeg2000Marker.COD);
        var qcdOffset = FindMarkerOffset(original, Jpeg2000Marker.QCD);
        var sotOffset = FindMarkerOffset(original, Jpeg2000Marker.SOT);
        var codPayloadLength = ReadMarkerPayloadLength(original, codOffset);
        var qcdPayloadLength = ReadMarkerPayloadLength(original, qcdOffset);

        mutated[codOffset + 9]--;
        for (var index = 1; index < qcdPayloadLength; index++)
        {
            mutated[qcdOffset + 4 + index] ^= 0x08;
        }

        var overrides = new List<byte>();
        for (var component = 0; component < componentCount; component++)
        {
            var cocPayload = new byte[2 + codPayloadLength - 5];
            cocPayload[0] = (byte)component;
            cocPayload[1] = original[codOffset + 4];
            Buffer.BlockCopy(original, codOffset + 9, cocPayload, 2, codPayloadLength - 5);
            AppendMarkerSegment(overrides, Jpeg2000Marker.COC, cocPayload);

            var qccPayload = new byte[1 + qcdPayloadLength];
            qccPayload[0] = (byte)component;
            Buffer.BlockCopy(original, qcdOffset + 4, qccPayload, 1, qcdPayloadLength);
            AppendMarkerSegment(overrides, Jpeg2000Marker.QCC, qccPayload);
        }

        var result = new byte[mutated.Length + overrides.Count];
        Buffer.BlockCopy(mutated, 0, result, 0, sotOffset);
        overrides.CopyTo(result, sotOffset);
        Buffer.BlockCopy(mutated, sotOffset, result, sotOffset + overrides.Count, mutated.Length - sotOffset);
        return result;
    }

    private static byte[] InsertTileComponentOverrides(byte[] codestream, int componentCount)
    {
        var original = codestream.ToArray();
        var codOffset = FindMarkerOffset(original, Jpeg2000Marker.COD);
        var qcdOffset = FindMarkerOffset(original, Jpeg2000Marker.QCD);
        var sotOffset = FindMarkerOffset(original, Jpeg2000Marker.SOT);
        var sodOffset = FindMarkerOffset(original, Jpeg2000Marker.SOD, sotOffset + 2);
        var codPayloadLength = ReadMarkerPayloadLength(original, codOffset);
        var qcdPayloadLength = ReadMarkerPayloadLength(original, qcdOffset);
        var codPayload = new byte[codPayloadLength];
        var qcdPayload = new byte[qcdPayloadLength];
        Buffer.BlockCopy(original, codOffset + 4, codPayload, 0, codPayload.Length);
        Buffer.BlockCopy(original, qcdOffset + 4, qcdPayload, 0, qcdPayload.Length);

        var tileHeader = new List<byte>();
        var tileCodPayload = codPayload.ToArray();
        tileCodPayload[5]--;
        AppendMarkerSegment(tileHeader, Jpeg2000Marker.COD, tileCodPayload);
        var tileQcdPayload = qcdPayload.ToArray();
        for (var index = 1; index < tileQcdPayload.Length; index++)
        {
            tileQcdPayload[index] ^= 0x08;
        }

        AppendMarkerSegment(tileHeader, Jpeg2000Marker.QCD, tileQcdPayload);
        for (var component = 0; component < componentCount; component++)
        {
            var cocPayload = new byte[2 + codPayloadLength - 5];
            cocPayload[0] = (byte)component;
            cocPayload[1] = codPayload[0];
            Buffer.BlockCopy(codPayload, 5, cocPayload, 2, codPayloadLength - 5);
            AppendMarkerSegment(tileHeader, Jpeg2000Marker.COC, cocPayload);

            var qccPayload = new byte[1 + qcdPayloadLength];
            qccPayload[0] = (byte)component;
            Buffer.BlockCopy(qcdPayload, 0, qccPayload, 1, qcdPayloadLength);
            AppendMarkerSegment(tileHeader, Jpeg2000Marker.QCC, qccPayload);
        }

        var result = new byte[original.Length + tileHeader.Count];
        Buffer.BlockCopy(original, 0, result, 0, sodOffset);
        tileHeader.CopyTo(result, sodOffset);
        Buffer.BlockCopy(original, sodOffset, result, sodOffset + tileHeader.Count, original.Length - sodOffset);
        AddToPsot(result, sotOffset, tileHeader.Count);
        return result;
    }

    private static int ReadMarkerPayloadLength(byte[] codestream, int markerOffset)
    {
        return ((codestream[markerOffset + 2] << 8) | codestream[markerOffset + 3]) - 2;
    }

    private static void AppendMarkerSegment(List<byte> destination, byte marker, byte[] payload)
    {
        destination.Add(0xFF);
        destination.Add(marker);
        destination.Add((byte)((payload.Length + 2) >> 8));
        destination.Add((byte)(payload.Length + 2));
        destination.AddRange(payload);
    }

    private static void AddToPsot(byte[] codestream, int sotOffset, int increment)
    {
        var psotOffset = sotOffset + 6;
        var psot = ((uint)codestream[psotOffset] << 24)
            | ((uint)codestream[psotOffset + 1] << 16)
            | ((uint)codestream[psotOffset + 2] << 8)
            | codestream[psotOffset + 3];
        psot += (uint)increment;
        codestream[psotOffset] = (byte)(psot >> 24);
        codestream[psotOffset + 1] = (byte)(psot >> 16);
        codestream[psotOffset + 2] = (byte)(psot >> 8);
        codestream[psotOffset + 3] = (byte)psot;
    }

    private static int FindMarkerOffset(byte[] codestream, byte marker, int start = 0)
    {
        for (var offset = start; offset + 1 < codestream.Length; offset++)
        {
            if (codestream[offset] == 0xFF && codestream[offset + 1] == marker)
            {
                return offset;
            }
        }

        throw new Xunit.Sdk.XunitException($"Marker 0xFF{marker:X2} not found.");
    }

    private static byte[] CreateCodestreamWithSubsampling(int horizontalSeparation, int verticalSeparation)
    {
        var writer = new Jpeg2000CodestreamWriter();
        writer.WriteStandalone(Jpeg2000Marker.SOC);
        writer.WriteSegment(Jpeg2000Marker.SIZ, CreateSizePayload(horizontalSeparation, verticalSeparation));
        writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(Jpeg2000ProgressionOrder.LRCP));
        writer.WriteSegment(Jpeg2000Marker.QCD, new byte[] { 0x00, 0x08 });
        writer.WriteSegment(Jpeg2000Marker.SOT, new byte[] { 0, 0, 0, 0, 0, 15, 0, 1 });
        writer.WriteStandalone(Jpeg2000Marker.SOD);
        writer.WriteRaw(new byte[] { 0x00 });
        writer.WriteStandalone(Jpeg2000Marker.EOC);
        return writer.ToArray();
    }

    private static byte[] CreateCodestreamWithProgressionOrder(Jpeg2000ProgressionOrder progressionOrder)
    {
        var writer = new Jpeg2000CodestreamWriter();
        writer.WriteStandalone(Jpeg2000Marker.SOC);
        writer.WriteSegment(Jpeg2000Marker.SIZ, CreateSizePayload(1, 1));
        writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(progressionOrder));
        writer.WriteSegment(Jpeg2000Marker.QCD, new byte[] { 0x00, 0x08 });
        writer.WriteSegment(Jpeg2000Marker.SOT, new byte[] { 0, 0, 0, 0, 0, 15, 0, 1 });
        writer.WriteStandalone(Jpeg2000Marker.SOD);
        writer.WriteRaw(new byte[] { 0x00 });
        writer.WriteStandalone(Jpeg2000Marker.EOC);
        return writer.ToArray();
    }

    private static byte[] CreateSizePayload(int horizontalSeparation, int verticalSeparation)
    {
        return new byte[]
        {
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x01,
            0x07, (byte)horizontalSeparation, (byte)verticalSeparation
        };
    }

    private static byte[] CreateCodingStylePayload(Jpeg2000ProgressionOrder progressionOrder)
    {
        return new byte[] { 0x00, (byte)progressionOrder, 0x00, 0x01, 0x00, 0x00, 0x04, 0x04, 0x00, 0x00 };
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
