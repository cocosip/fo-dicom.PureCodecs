using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class FullCompatibilityMatrixTests
{
    public static TheoryData<string, DicomTransferSyntax, IDicomCodec, int?> EfferentAcceptanceDecodeCases => new()
    {
        { "JPEG baseline YBR 4:2:2 acceptance sample", DicomTransferSyntax.JPEGProcess1, new DicomJpegProcess1Codec(), 38 },
        { "JPEG baseline YBR full acceptance sample", DicomTransferSyntax.JPEGProcess1, new DicomJpegProcess1Codec(), 20 },
        { "JPEG lossless RGB acceptance sample", DicomTransferSyntax.JPEGProcess14SV1, new DicomJpegLossless14SV1Codec(), null },
        { "RLE lossless acceptance sample", DicomTransferSyntax.RLELossless, new DicomRleLosslessCodec(), null },
        { "JPEG-LS lossless acceptance sample", DicomTransferSyntax.JPEGLSLossless, new DicomJpegLsLosslessCodec(), null },
        { "JPEG-LS near-lossless acceptance sample", DicomTransferSyntax.JPEGLSNearLossless, new DicomJpegLsNearLosslessCodec(), 2 },
        { "JPEG 2000 lossless acceptance sample", DicomTransferSyntax.JPEG2000Lossless, new DicomJpeg2000LosslessCodec(), null },
        { "JPEG 2000 lossy acceptance sample", DicomTransferSyntax.JPEG2000Lossy, new DicomJpeg2000LossyCodec(), 19 },
        { "JPEG 2000 lossy quality-50 acceptance sample", DicomTransferSyntax.JPEG2000Lossy, new DicomJpeg2000LossyCodec(), 15 },
    };

    public static TheoryData<string, DicomTransferSyntax, int?> EfferentUnitJpeg2000Cases => new()
    {
        { "Efferent unit 16-bit sample", DicomTransferSyntax.JPEG2000Lossless, null },
    };

    public static TheoryData<DicomTransferSyntax, IDicomCodec, byte[]> InvalidStreamCases => new()
    {
        { DicomTransferSyntax.RLELossless, new DicomRleLosslessCodec(), new byte[8] },
        { DicomTransferSyntax.JPEGProcess1, new DicomJpegProcess1Codec(), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEGProcess2_4, new DicomJpegProcess2_4Codec(), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEGProcess14, new DicomJpegLossless14Codec(), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEGProcess14SV1, new DicomJpegLossless14SV1Codec(), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEGLSLossless, new DicomJpegLsLosslessCodec(), new byte[] { 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEGLSNearLossless, new DicomJpegLsNearLosslessCodec(), new byte[] { 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEG2000Lossless, new DicomJpeg2000LosslessCodec(), new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 } },
        { DicomTransferSyntax.JPEG2000Lossy, new DicomJpeg2000LossyCodec(), new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 } },
        { DicomTransferSyntax.HTJ2KLossless, new DicomHtJpeg2000LosslessCodec(), new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 } },
        { DicomTransferSyntax.HTJ2KLosslessRPCL, new DicomHtJpeg2000LosslessRpclCodec(), new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 } },
        { DicomTransferSyntax.HTJ2K, new DicomHtJpeg2000LossyCodec(), new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 } },
    };

    [Theory]
    [MemberData(nameof(Phase1TransferSyntaxes.AllPairs), MemberType = typeof(Phase1TransferSyntaxes))]
    public void CanTranscode_matrix_allows_every_phase_1_syntax_pair(DicomTransferSyntax source, DicomTransferSyntax target)
    {
        ITranscoderManager manager = new PureTranscoderManager();

        Assert.True(manager.CanTranscode(source, target), $"{source.UID.Name} -> {target.UID.Name}");
    }

    [Theory]
    [MemberData(nameof(Phase1TransferSyntaxes.RoundTripCodecs), MemberType = typeof(Phase1TransferSyntaxes))]
    public void Raw_8_bit_matrix_round_trips_through_each_phase_1_codec(
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 16, columns: 16);

        AssertRoundTrip(dataset, syntax, codec, tolerance);
    }

    [Theory]
    [MemberData(nameof(Supported16BitRoundTripCodecs))]
    public void Raw_16_bit_matrix_round_trips_through_each_supported_phase_1_codec(
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome16(rows: 8, columns: 8);

        AssertRoundTrip(dataset, syntax, codec, tolerance);
    }

    [Theory]
    [MemberData(nameof(SupportedRgbRoundTripCodecs))]
    public void Rgb_matrix_round_trips_through_each_supported_phase_1_codec(
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16);

        AssertRoundTrip(dataset, syntax, codec, tolerance);
    }

    [Theory]
    [MemberData(nameof(Phase1TransferSyntaxes.RoundTripCodecs), MemberType = typeof(Phase1TransferSyntaxes))]
    public void Multi_frame_matrix_round_trips_through_each_phase_1_codec(
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var dataset = DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 16, columns: 16, frameCount: 3);

        AssertRoundTrip(dataset, syntax, codec, tolerance);
    }

    [Theory]
    [MemberData(nameof(EfferentUnitJpeg2000Cases))]
    public void Efferent_unit_compatibility_matrix_round_trips_jpeg2000_unit_samples(
        string fixtureName,
        DicomTransferSyntax syntax,
        int? tolerance)
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.UnitFixtures.Single(item => item.Name == fixtureName);
        var file = DicomFile.Open(fixture.Path);
        var codec = syntax == DicomTransferSyntax.JPEG2000Lossless
            ? new DicomJpeg2000LosslessCodec()
            : (IDicomCodec)new DicomJpeg2000LossyCodec();

        AssertRoundTrip(file.Dataset, syntax, codec, tolerance);
    }

    [Theory]
    [MemberData(nameof(EfferentAcceptanceDecodeCases))]
    public void Efferent_acceptance_transcode_matrix_decodes_available_compressed_samples(
        string fixtureName,
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.AcceptanceFixtures.Single(item => item.Name == fixtureName);
        Assert.Equal(syntax, fixture.ExpectedTransferSyntax);
        var file = DicomFile.Open(fixture.Path);
        var compressed = DicomPixelData.Create(file.Dataset);
        var decoded = CreateRawTargetForDecode(compressed);

        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(compressed.Width, decoded.Width);
        Assert.Equal(compressed.Height, decoded.Height);
        Assert.Equal(compressed.NumberOfFrames, decoded.NumberOfFrames);
        Assert.True(decoded.GetFrame(0).Size > 0);
        if (!tolerance.HasValue)
        {
            Assert.Equal(ExpectedRawFrameLength(decoded), decoded.GetFrame(0).Size);
        }
    }

    [Theory]
    [MemberData(nameof(EfferentAcceptanceDecodeCases))]
    public void Efferent_acceptance_inverse_transcode_matrix_decodes_reencoded_available_samples(
        string fixtureName,
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.AcceptanceFixtures.Single(item => item.Name == fixtureName);
        var file = DicomFile.Open(fixture.Path);
        var compressed = DicomPixelData.Create(file.Dataset);
        var raw = CreateRawTargetForDecode(compressed);
        codec.Decode(compressed, raw, codec.GetDefaultParameters());
        var reencoded = CreateTargetPixelData(raw, syntax);
        var decodedAgain = CreateTargetPixelData(raw, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(raw, reencoded, CreateEncodeParameters(codec, tolerance));
        codec.Decode(reencoded, decodedAgain, codec.GetDefaultParameters());

        if (tolerance.HasValue)
        {
            Assert.InRange(PixelDataAssertions.MaxSampleDifference(raw, decodedAgain), 0, tolerance.Value);
        }
        else
        {
            PixelDataAssertions.FramesMatchExactly(raw, decodedAgain);
        }
    }

    [Fact]
    public void Render_matrix_renders_available_compressed_fixtures()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();
        var catalog = ExternalFixtureCatalog.Resolve();
        var compressedFixtures = catalog.RenderFixtures
            .Where(fixture => fixture.ExpectedTransferSyntax != DicomTransferSyntax.ExplicitVRLittleEndian)
            .ToArray();

        Assert.NotEmpty(compressedFixtures);
        Assert.All(compressedFixtures, fixture =>
        {
            var rendered = new DicomImage(fixture.Path).RenderImage();
            Assert.NotNull(rendered);
        });
    }

    [Theory]
    [MemberData(nameof(InvalidStreamCases))]
    public void Invalid_stream_matrix_throws_managed_codec_exceptions(
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        byte[] frame)
    {
        var compressed = CreateCompressedPixelDataWithFrame(syntax, frame);
        var target = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 1, columns: 1), true);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, target, codec.GetDefaultParameters()));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    public static TheoryData<DicomTransferSyntax, IDicomCodec, int?> Supported16BitRoundTripCodecs
    {
        get
        {
            var data = new TheoryData<DicomTransferSyntax, IDicomCodec, int?>();
            foreach (var row in Phase1TransferSyntaxes.RoundTripCodecRows)
            {
                if (row.Syntax != DicomTransferSyntax.JPEGProcess1
                    && row.Syntax != DicomTransferSyntax.JPEGProcess2_4
                    && row.Syntax != DicomTransferSyntax.HTJ2K)
                {
                    data.Add(row.Syntax, row.Codec, row.Tolerance);
                }
            }

            return data;
        }
    }

    public static TheoryData<DicomTransferSyntax, IDicomCodec, int?> SupportedRgbRoundTripCodecs
    {
        get
        {
            var data = new TheoryData<DicomTransferSyntax, IDicomCodec, int?>();
            foreach (var row in Phase1TransferSyntaxes.RoundTripCodecRows)
            {
                if (row.Syntax != DicomTransferSyntax.JPEGProcess14
                    && row.Syntax != DicomTransferSyntax.JPEGProcess14SV1)
                {
                    data.Add(row.Syntax, row.Codec, row.Tolerance);
                }
            }

            return data;
        }
    }

    private static void AssertRoundTrip(
        DicomDataset dataset,
        DicomTransferSyntax syntax,
        IDicomCodec codec,
        int? tolerance)
    {
        var source = DicomPixelData.Create(dataset);
        var compressed = CreateTargetPixelData(source, syntax);
        var decoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var parameters = CreateEncodeParameters(codec, tolerance);

        codec.Encode(source, compressed, parameters);
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.AssertFrameCount(source, compressed);
        PixelDataAssertions.AssertRequiredCompressionTags(compressed.Dataset, syntax);
        if (tolerance.HasValue)
        {
            PixelDataAssertions.FramesMatchWithinTolerance(source, decoded, tolerance.Value);
        }
        else
        {
            PixelDataAssertions.FramesMatchExactly(source, decoded);
        }
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.SOPClassUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage) },
            { DicomTag.SOPInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, DicomUID.Generate()) },
            { DicomTag.StudyInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, DicomUID.Generate()) },
            { DicomTag.SeriesInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, DicomUID.Generate()) },
            { DicomTag.PhotometricInterpretation, source.PhotometricInterpretation.Value },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, source.BitsAllocated },
            { DicomTag.BitsStored, source.BitsStored },
            { DicomTag.HighBit, source.HighBit },
            { DicomTag.PixelRepresentation, (ushort)source.PixelRepresentation },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
        };

        if (source.NumberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, source.NumberOfFrames.ToString());
        }

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)source.PlanarConfiguration);
        }

        return DicomPixelData.Create(dataset, true);
    }

    private static DicomPixelData CreateRawTargetForDecode(DicomPixelData source)
    {
        var photometric = source.SamplesPerPixel == 3
            ? PhotometricInterpretation.Rgb.Value
            : source.PhotometricInterpretation.Value;
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage) },
            { DicomTag.SOPInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, DicomUID.Generate()) },
            { DicomTag.StudyInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, DicomUID.Generate()) },
            { DicomTag.SeriesInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, DicomUID.Generate()) },
            { DicomTag.PhotometricInterpretation, photometric },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, source.BitsAllocated },
            { DicomTag.BitsStored, source.BitsStored },
            { DicomTag.HighBit, source.HighBit },
            { DicomTag.PixelRepresentation, (ushort)source.PixelRepresentation },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
        };

        if (source.NumberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, source.NumberOfFrames.ToString());
        }

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        }

        return DicomPixelData.Create(dataset, true);
    }

    private static DicomPixelData CreateCompressedPixelDataWithFrame(DicomTransferSyntax syntax, byte[] frame)
    {
        var dataset = new DicomDataset(syntax)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(frame));
        return DicomPixelData.Create(dataset);
    }

    private static DicomCodecParams CreateEncodeParameters(IDicomCodec codec, int? tolerance)
    {
        var parameters = codec.GetDefaultParameters();
        if (parameters is DicomJpegLsParams jpegLsParameters && tolerance.HasValue)
        {
            jpegLsParameters.AllowedError = tolerance.Value;
        }

        return parameters;
    }

    private static int ExpectedRawFrameLength(DicomPixelData pixelData)
    {
        return pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * (pixelData.BitsAllocated / 8);
    }
}
