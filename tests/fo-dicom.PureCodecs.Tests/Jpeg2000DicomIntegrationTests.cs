using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using CoreHtJpeg2000Params = FellowOakDicom.Imaging.Codec.DicomHtJpeg2000Params;
using CoreJpeg2000Params = FellowOakDicom.Imaging.Codec.DicomJpeg2000Params;
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
