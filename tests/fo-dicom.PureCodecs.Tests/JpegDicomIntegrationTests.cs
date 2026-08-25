using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegCodecParams = FellowOakDicom.Imaging.NativeCodec.DicomJpegParams;
using NativeJpegProcess1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess1Codec;
using NativeJpegProcess4Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess4Codec;
using NativeJpegSampleFactor = FellowOakDicom.Imaging.NativeCodec.DicomJpegSampleFactor;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegDicomIntegrationTests
{
    [Fact]
    public void Default_jpeg_parameters_match_fo_dicom_color_conversion_default()
    {
        var parameters = Assert.IsType<JpegCodecParams>(new DicomJpegProcess1Codec().GetDefaultParameters());

        Assert.Equal(90, parameters.Quality);
        Assert.True(parameters.ConvertColorspaceToRGB);
        Assert.Equal(0, parameters.SmoothingFactor);
        Assert.Equal(DicomJpegSampleFactor.SF444, parameters.SampleFactor);
        Assert.Equal(1, parameters.Predictor);
        Assert.Equal(0, parameters.PointTransform);
    }

    [Fact]
    public void Process1_maps_core_422_sample_factor_to_jpeg_sampling_factors()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var codec = new DicomJpegProcess1Codec();
        var parameters = new DicomJpegParams
        {
            Quality = 90,
            SampleFactor = DicomJpegSampleFactor.SF422
        };

        codec.Encode(source, compressed, parameters);

        Assert.Equal(new byte[] { 0x21, 0x11, 0x11 }, GetSofSamplingFactors(ToArray(compressed.GetFrame(0))));
    }

    [Fact]
    public void Process1_smoothing_changes_codestream_and_zero_preserves_default()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var defaultCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var zeroCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var smoothedCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var codec = new DicomJpegProcess1Codec();

        codec.Encode(source, defaultCompressed, codec.GetDefaultParameters());
        codec.Encode(source, zeroCompressed, new DicomJpegParams { SmoothingFactor = 0 });
        codec.Encode(source, smoothedCompressed, new DicomJpegParams { SmoothingFactor = 50 });

        Assert.Equal(ToArray(defaultCompressed.GetFrame(0)), ToArray(zeroCompressed.GetFrame(0)));
        Assert.NotEqual(ToArray(zeroCompressed.GetFrame(0)), ToArray(smoothedCompressed.GetFrame(0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Pure_process2_4_rejects_unsafe_smoothing_before_emitting_a_frame(int smoothingFactor)
    {
        var source = CreateMonochromeEdgePattern(rows: 16, columns: 16);
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var codec = new DicomJpegProcess2_4Codec();

        var exception = Assert.Throws<DicomCodecException>(() => codec.Encode(
            source,
            compressed,
            new DicomJpegParams { SmoothingFactor = smoothingFactor }));

        Assert.Contains("smoothing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, compressed.NumberOfFrames);
    }

    [Fact]
    public void Process2_4_smoothing_changes_output_and_cross_decodes_in_both_directions()
    {
        var source = CreateMonochromeEdgePattern(rows: 16, columns: 16);
        var pureCodec = new DicomJpegProcess2_4Codec();
        var nativeCodec = new NativeJpegProcess4Codec();
        var pureZero = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var pureSmoothed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        pureCodec.Encode(source, pureZero, new DicomJpegParams { SmoothingFactor = 0 });
        pureCodec.Encode(source, pureSmoothed, new DicomJpegParams { SmoothingFactor = 50 });
        Assert.NotEqual(ToArray(pureZero.GetFrame(0)), ToArray(pureSmoothed.GetFrame(0)));

        var pureDecodedPureOutput = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecodedPureOutput = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        pureCodec.Decode(pureSmoothed, pureDecodedPureOutput, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(pureSmoothed, nativeDecodedPureOutput, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchWithinTolerance(pureDecodedPureOutput, nativeDecodedPureOutput, tolerance: 2);

        const string nativeSmoothedBase64 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/wAALCAAQABABAREA/8QAFgABAQEAAAAAAAAAAAAAAAAAAgAB/8QAIRAAAAUCBwAAAAAAAAAAAAAAAQIDBhQRQQASExYhMWH/2gAIAQEAAD8AxNMGmVMhEwZANbgiZRnbKkWDufMr7o5rUwk1BaZkzkUFkC1uSKFCdsqRcO58yvujmtTEomLTMoQ6YsgWtwdMwztlSLD3PmV90c1qYKigNMqhzqAyAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var nativeSmoothedFrame = Convert.FromBase64String(nativeSmoothedBase64);
        var nativeSmoothed = CreateCompressedPixelData(source, DicomTransferSyntax.JPEGProcess2_4, nativeSmoothedFrame);
        var pureDecodedNativeOutput = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecodedNativeOutput = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        pureCodec.Decode(nativeSmoothed, pureDecodedNativeOutput, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(nativeSmoothed, nativeDecodedNativeOutput, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchWithinTolerance(nativeDecodedNativeOutput, pureDecodedNativeOutput, tolerance: 2);
    }

    [Fact]
    public void Ybr_full_to_rgb_conversion_maps_samples()
    {
        var rgb = JpegColorConverter.YbrFullToRgb(new byte[] { 100, 128, 128, 76, 84, 255 });

        Assert.Equal(new byte[] { 100, 100, 100, 254, 0, 0 }, rgb);
    }

    [Fact]
    public void Rgb_to_ybr_full_uses_native_fixed_point_rounding()
    {
        var ybr = JpegColorConverter.RgbToYbrFull(new byte[] { 0, 0, 7 });

        Assert.Equal(new byte[] { 1, 131, 127 }, ybr);
    }

    [Fact]
    public void Ybr_full_422_to_rgb_conversion_expands_shared_chroma()
    {
        var rgb = JpegColorConverter.YbrFull422ToRgb(new byte[] { 100, 150, 128, 128 });

        Assert.Equal(new byte[] { 100, 100, 100, 150, 150, 150 }, rgb);
    }

    [Fact]
    public void Planar_rgb_to_interleaved_conversion_reorders_samples()
    {
        var planar = new byte[] { 1, 2, 3, 10, 20, 30, 100, 110, 120 };

        var interleaved = JpegColorConverter.PlanarRgbToInterleaved(planar, pixelCount: 3);

        Assert.Equal(new byte[] { 1, 10, 100, 2, 20, 110, 3, 30, 120 }, interleaved);
    }

    [Fact]
    public void Process1_decode_converts_ybr_full_to_rgb_when_requested()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = CreateYbrFullPixelData();
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);
        var decodedPixelData = CreateRgbTargetPixelData(rawPixelData);

        codec.Encode(rawPixelData, compressedPixelData, new JpegCodecParams { ConvertColorspaceToRGB = false });
        codec.Decode(compressedPixelData, decodedPixelData, new JpegCodecParams { ConvertColorspaceToRGB = true });

        var decoded = ToArray(decodedPixelData.GetFrame(0));
        Assert.Equal(6, decoded.Length);
        Assert.True(decoded[0] > decoded[1] + 40);
        Assert.True(decoded[0] > decoded[2] + 40);
    }

    [Fact]
    public void Process1_encode_accepts_rgb_planar_by_normalizing_to_interleaved()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbPlanar(rows: 1, columns: 3));
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, new JpegCodecParams { ConvertColorspaceToRGB = false });

        Assert.Equal(rawPixelData.GetFrame(0).Size, decodedPixelData.GetFrame(0).Size);
    }

    [Fact]
    public void Process1_encode_converts_rgb_to_ybr_full_422_with_native_jpeg_sampling()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());

        Assert.Equal(PhotometricInterpretation.YbrFull422, compressedPixelData.PhotometricInterpretation);
        Assert.Equal(
            new byte[] { 0x11, 0x11, 0x11 },
            GetSofSamplingFactors(ToArray(compressedPixelData.GetFrame(0))));
    }

    [Theory]
    [InlineData(DicomJpegSampleFactor.SF444, 0x11)]
    [InlineData(DicomJpegSampleFactor.SF422, 0x21)]
    public void Process1_encodes_raw_ybr_full_422_for_native_decode(
        DicomJpegSampleFactor sampleFactor,
        byte expectedLumaSampling)
    {
        var (source, expectedRgb) = CreateRawYbrFull422AndExpectedRgb(rows: 8, columns: 8);
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var nativeDecoded = CreateRgbTargetPixelData(expectedRgb);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();
        var nativeParameters = Assert.IsType<NativeJpegCodecParams>(nativeCodec.GetDefaultParameters());
        nativeParameters.ConvertColorSpaceToRGB = true;

        pureCodec.Encode(
            source,
            compressed,
            new JpegCodecParams { Quality = 90, SampleFactor = sampleFactor });
        nativeCodec.Decode(compressed, nativeDecoded, nativeParameters);

        Assert.Equal(expectedLumaSampling, GetSofSamplingFactors(ToArray(compressed.GetFrame(0)))[0]);
        Assert.InRange(PixelDataAssertions.MaxSampleDifference(expectedRgb, nativeDecoded), 0, 64);
    }

    [Fact]
    public void Process1_rgb_encode_then_decode_is_not_less_accurate_than_native_default()
    {
        var codec = new DicomJpegProcess1Codec();
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var decoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecodedPureOutput = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeCodec = new NativeJpegProcess1Codec();
        var nativeParameters = Assert.IsType<NativeJpegCodecParams>(nativeCodec.GetDefaultParameters());
        nativeParameters.ConvertColorSpaceToRGB = true;

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());
        nativeCodec.Encode(source, nativeCompressed, nativeParameters);
        nativeCodec.Decode(nativeCompressed, nativeDecoded, nativeParameters);
        nativeCodec.Decode(compressed, nativeDecodedPureOutput, nativeParameters);

        var nativeDifference = PixelDataAssertions.MaxSampleDifference(source, nativeDecoded);
        var pureDifference = PixelDataAssertions.MaxSampleDifference(source, decoded);
        Assert.InRange(nativeDifference, 0, 48);
        Assert.True(
            pureDifference <= nativeDifference,
            $"Pure JPEG max sample difference {pureDifference} exceeds native difference {nativeDifference}.");
        Assert.Equal(source.GetFrame(0).Size, nativeDecodedPureOutput.GetFrame(0).Size);
    }

    [Fact]
    public void Process1_normalizes_8_bit_samples_from_16_bit_dicom_containers()
    {
        var source = CreateMonochrome16Container8Stored(rows: 8, columns: 8, sample: 137);
        var pureCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();

        pureCodec.Encode(source, pureCompressed, pureCodec.GetDefaultParameters());
        pureCodec.Decode(pureCompressed, pureDecoded, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(pureCompressed, nativeDecoded, nativeCodec.GetDefaultParameters());

        Assert.Equal(8, pureCompressed.BitsAllocated);
        Assert.Equal(8, pureDecoded.BitsAllocated);
        Assert.Equal(8, nativeDecoded.BitsAllocated);
        Assert.Equal(64, pureDecoded.GetFrame(0).Size);
        Assert.Equal(64, nativeDecoded.GetFrame(0).Size);
        Assert.All(ToArray(pureDecoded.GetFrame(0)), value => Assert.InRange(value, (byte)136, (byte)138));
        PixelDataAssertions.FramesMatchExactly(pureDecoded, nativeDecoded);
    }

    [Fact]
    public void Process2_4_12_bit_rgb_sf444_interoperates_in_both_directions()
    {
        const int tolerance = 160;
        var source = CreateRgb12Interleaved(rows: 16, columns: 16);
        var pureCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var nativeCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var nativeDecodedPureOutput = CreateRgbTargetPixelData(source);
        var pureDecodedNativeOutput = CreateRgbTargetPixelData(source);
        var pureCodec = new DicomJpegProcess2_4Codec();
        var nativeCodec = new NativeJpegProcess4Codec();
        var pureParameters = new JpegCodecParams
        {
            Quality = 90,
            SampleFactor = DicomJpegSampleFactor.SF444,
            ConvertColorspaceToRGB = true
        };
        var nativeParameters = new NativeJpegCodecParams
        {
            Quality = 90,
            SampleFactor = NativeJpegSampleFactor.SF444,
            ConvertColorSpaceToRGB = true
        };

        pureCodec.Encode(source, pureCompressed, pureParameters);
        nativeCodec.Decode(pureCompressed, nativeDecodedPureOutput, nativeParameters);
        Assert.Equal(12, ReadSofPrecision(ToArray(pureCompressed.GetFrame(0))));
        Assert.Equal(3, GetSofSamplingFactors(ToArray(pureCompressed.GetFrame(0))).Length);
        PixelDataAssertions.FramesMatchWithinTolerance(source, nativeDecodedPureOutput, tolerance);

        nativeCodec.Encode(source, nativeCompressed, nativeParameters);
        pureCodec.Decode(nativeCompressed, pureDecodedNativeOutput, pureParameters);
        PixelDataAssertions.FramesMatchWithinTolerance(source, pureDecodedNativeOutput, tolerance);
    }

    [Fact]
    public void Process2_4_12_bit_planar_rgb_is_normalized_before_native_decode()
    {
        const int tolerance = 160;
        var expectedInterleaved = CreateRgb12Interleaved(rows: 16, columns: 16);
        var planarSource = CreateRgb12Planar(expectedInterleaved);
        var pureCompressed = CreateTargetPixelData(planarSource, DicomTransferSyntax.JPEGProcess2_4);
        var nativeDecoded = CreateRgbTargetPixelData(expectedInterleaved);
        var pureCodec = new DicomJpegProcess2_4Codec();
        var nativeCodec = new NativeJpegProcess4Codec();

        pureCodec.Encode(
            planarSource,
            pureCompressed,
            new JpegCodecParams { Quality = 90, SampleFactor = DicomJpegSampleFactor.SF444 });
        nativeCodec.Decode(pureCompressed, nativeDecoded, nativeCodec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(expectedInterleaved, nativeDecoded, tolerance);
    }

    [Fact]
    public void Process2_4_decodes_16_bit_dqt_identically_with_native()
    {
        var frame = Enumerable.Repeat((byte)128, 64).ToArray();
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8, frame));
        var encoded = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var pureCodec = new DicomJpegProcess2_4Codec();
        var nativeCodec = new NativeJpegProcess4Codec();

        pureCodec.Encode(source, encoded, pureCodec.GetDefaultParameters());
        var sixteenBitDqt = ConvertFirstDqtTo16Bit(ToArray(encoded.GetFrame(0)));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        compressed.AddFrame(new MemoryByteBuffer(sixteenBitDqt));

        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(pureDecoded, nativeDecoded);
        Assert.All(ToArray(pureDecoded.GetFrame(0)), value => Assert.Equal((byte)128, value));
    }

    [Theory]
    [InlineData("precision")]
    [InlineData("zero")]
    [InlineData("truncated")]
    public void Process2_4_rejects_invalid_16_bit_dqt_payloads(string invalidKind)
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 8));
        var encoded = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        var decoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codec = new DicomJpegProcess2_4Codec();
        codec.Encode(source, encoded, codec.GetDefaultParameters());
        var invalid = ConvertFirstDqtTo16Bit(ToArray(encoded.GetFrame(0)));
        var dqtOffset = FindMarker(invalid, JpegMarker.DQT);
        switch (invalidKind)
        {
            case "precision":
                invalid[dqtOffset + 4] = 0x20;
                break;
            case "zero":
                invalid[dqtOffset + 5] = 0;
                invalid[dqtOffset + 6] = 0;
                break;
            case "truncated":
                invalid[dqtOffset + 3]--;
                break;
        }

        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess2_4);
        compressed.AddFrame(new MemoryByteBuffer(invalid));

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Decode(compressed, decoded, codec.GetDefaultParameters()));

        Assert.Contains(invalidKind, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process1_decodes_native_ybr_full_422_fixture_within_measured_native_tolerance()
    {
        var fixture = ExternalFixtureCatalog.Resolve().AcceptanceFixtures
            .Single(item => item.Name == "JPEG baseline YBR 4:2:2 acceptance sample");
        var compressed = DicomPixelData.Create(DicomFile.Open(fixture.Path).Dataset);
        var pureDecoded = CreateRgbTargetPixelData(compressed);
        var nativeDecoded = CreateRgbTargetPixelData(compressed);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();
        var nativeParameters = Assert.IsType<NativeJpegCodecParams>(nativeCodec.GetDefaultParameters());

        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(compressed, nativeDecoded, nativeParameters);

        var maxDifference = PixelDataAssertions.MaxSampleDifference(nativeDecoded, pureDecoded);
        Assert.InRange(maxDifference, 0, 3);
        Assert.Equal(PhotometricInterpretation.Rgb, pureDecoded.PhotometricInterpretation);
        Assert.Equal(PlanarConfiguration.Interleaved, pureDecoded.PlanarConfiguration);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Process1_decodes_native_narrow_ybr_full_422_within_measured_native_tolerance(int columns)
    {
        var fixture = ExternalFixtureCatalog.Resolve().AcceptanceFixtures
            .Single(item => item.Name == "JPEG baseline YBR 4:2:2 acceptance sample");
        var compressed = CreateNarrowYbrFull422Fixture(DicomPixelData.Create(DicomFile.Open(fixture.Path).Dataset), rows: 8, columns);
        var pureDecoded = CreateRgbTargetPixelData(compressed);
        var nativeDecoded = CreateRgbTargetPixelData(compressed);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();
        var nativeParameters = Assert.IsType<NativeJpegCodecParams>(nativeCodec.GetDefaultParameters());

        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(compressed, nativeDecoded, nativeParameters);

        var maxDifference = PixelDataAssertions.MaxSampleDifference(nativeDecoded, pureDecoded);
        Assert.Equal(0, maxDifference);
        Assert.Equal(new byte[] { 0x21, 0x11, 0x11 }, GetSofSamplingFactors(ToArray(compressed.GetFrame(0))));
    }

    [Fact]
    public void Process1_reencodes_native_ybr_full_422_fixture_for_native_decode_within_measured_tolerance()
    {
        var fixture = ExternalFixtureCatalog.Resolve().AcceptanceFixtures
            .Single(item => item.Name == "JPEG baseline YBR 4:2:2 acceptance sample");
        var source = DicomPixelData.Create(DicomFile.Open(fixture.Path).Dataset);
        var raw = CreateRgbTargetPixelData(source);
        var pureCompressed = CreateTargetPixelData(raw, DicomTransferSyntax.JPEGProcess1);
        var nativeDecoded = CreateRgbTargetPixelData(raw);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();
        var nativeParameters = Assert.IsType<NativeJpegCodecParams>(nativeCodec.GetDefaultParameters());

        pureCodec.Decode(source, raw, pureCodec.GetDefaultParameters());
        pureCodec.Encode(raw, pureCompressed, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(pureCompressed, nativeDecoded, nativeParameters);

        var maxDifference = PixelDataAssertions.MaxSampleDifference(raw, nativeDecoded);
        Assert.InRange(maxDifference, 0, 33);
        Assert.Equal(PhotometricInterpretation.YbrFull422, pureCompressed.PhotometricInterpretation);
        Assert.Equal(1, pureCompressed.NumberOfFrames);
    }

    [Fact]
    public void Process1_rejects_unsupported_photometric_interpretation()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = CreateUnsupportedPhotometricPixelData();
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters()));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("photometric", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process14_round_trip_covers_8_and_16_bit_data_for_integration_matrix()
    {
        AssertLosslessRoundTrip(new DicomJpegLossless14Codec(), DicomTransferSyntax.JPEGProcess14, bitsAllocated: 8);
        AssertLosslessRoundTrip(new DicomJpegLossless14Codec(), DicomTransferSyntax.JPEGProcess14, bitsAllocated: 12);
        AssertLosslessRoundTrip(new DicomJpegLossless14Codec(), DicomTransferSyntax.JPEGProcess14, bitsAllocated: 16);
    }

    [Fact]
    public void Process14_sv1_round_trip_covers_8_and_16_bit_data_for_integration_matrix()
    {
        AssertLosslessRoundTrip(new DicomJpegLossless14SV1Codec(), DicomTransferSyntax.JPEGProcess14SV1, bitsAllocated: 8);
        AssertLosslessRoundTrip(new DicomJpegLossless14SV1Codec(), DicomTransferSyntax.JPEGProcess14SV1, bitsAllocated: 12);
        AssertLosslessRoundTrip(new DicomJpegLossless14SV1Codec(), DicomTransferSyntax.JPEGProcess14SV1, bitsAllocated: 16);
    }

    private static void AssertLosslessRoundTrip(IDicomCodec codec, DicomTransferSyntax syntax, int bitsAllocated)
    {
        var dataset = bitsAllocated == 8
            ? DicomPixelDataFixtures.CreateMonochrome8()
            : bitsAllocated == 12
                ? CreateMonochrome12()
            : DicomPixelDataFixtures.CreateMonochrome16();
        var rawPixelData = DicomPixelData.Create(dataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, syntax);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
    }

    private static DicomPixelData CreateYbrFullPixelData()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, "YBR_FULL" },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)3 },
            { DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new MemoryByteBuffer(new byte[] { 76, 84, 255, 150, 128, 128 }));
        return pixelData;
    }

    private static (DicomPixelData Source, DicomPixelData ExpectedRgb) CreateRawYbrFull422AndExpectedRgb(
        ushort rows,
        ushort columns)
    {
        var pixelCount = rows * columns;
        var packed = new byte[pixelCount * 2];
        var rgb = new byte[pixelCount * 3];
        for (var pixel = 0; pixel < pixelCount; pixel += 2)
        {
            var first = (byte)((pixel * 7 + 19) % 240);
            var second = (byte)(((pixel + 1) * 7 + 19) % 240);
            var packedOffset = pixel * 2;
            packed[packedOffset] = first;
            packed[packedOffset + 1] = second;
            packed[packedOffset + 2] = 128;
            packed[packedOffset + 3] = 128;
            for (var component = 0; component < 3; component++)
            {
                rgb[pixel * 3 + component] = first;
                rgb[(pixel + 1) * 3 + component] = second;
            }
        }

        var sourceDataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 3,
            PhotometricInterpretation.YbrFull422,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: PlanarConfiguration.Interleaved,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            packed);
        var expectedDataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 3,
            PhotometricInterpretation.Rgb,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: PlanarConfiguration.Interleaved,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            rgb);
        return (DicomPixelData.Create(sourceDataset), DicomPixelData.Create(expectedDataset));
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
        pixelData.AddFrame(new MemoryByteBuffer(new byte[] { 1, 2, 3 }));
        return pixelData;
    }

    private static DicomDataset CreateMonochrome12()
    {
        var bytes = new byte[24];
        var samples = new[] { 100, 110, 95, 130, 4095, 4080, 3000, 2800, 2048, 2000, 1900, 1800 };
        for (var index = 0; index < samples.Length; index++)
        {
            bytes[index * 2] = (byte)samples[index];
            bytes[index * 2 + 1] = (byte)(samples[index] >> 8);
        }

        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)3 },
            { DicomTag.Columns, (ushort)4 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)12 },
            { DicomTag.HighBit, (ushort)11 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(bytes));
        return dataset;
    }

    private static DicomPixelData CreateMonochrome16Container8Stored(ushort rows, ushort columns, byte sample)
    {
        var frame = new byte[rows * columns * 2];
        for (var index = 0; index < rows * columns; index++)
        {
            frame[index * 2] = sample;
            frame[index * 2 + 1] = (byte)(0x40 + index % 0x40);
        }

        var dataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 1,
            PhotometricInterpretation.Monochrome2,
            bitsAllocated: 16,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            frame);
        return DicomPixelData.Create(dataset);
    }

    private static DicomPixelData CreateMonochromeEdgePattern(ushort rows, ushort columns)
    {
        var frame = new byte[rows * columns];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                frame[y * columns + x] = ((x / 2 + y / 2) & 1) == 0 ? (byte)24 : (byte)232;
            }
        }

        return DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows, columns, frame));
    }

    private static DicomPixelData CreateCompressedPixelData(
        DicomPixelData source,
        DicomTransferSyntax transferSyntax,
        byte[] frame)
    {
        var compressed = CreateTargetPixelData(source, transferSyntax);
        compressed.AddFrame(new MemoryByteBuffer(frame));
        return compressed;
    }

    private static DicomPixelData CreateRgb12Interleaved(ushort rows, ushort columns)
    {
        var frame = new byte[rows * columns * 3 * 2];
        for (var pixel = 0; pixel < rows * columns; pixel++)
        {
            var x = pixel % columns;
            var y = pixel / columns;
            WriteUInt16(frame, (pixel * 3) * 2, 512 + x * 96 + y * 16);
            WriteUInt16(frame, (pixel * 3 + 1) * 2, 768 + x * 32 + y * 80);
            WriteUInt16(frame, (pixel * 3 + 2) * 2, 1024 + x * 48 + y * 48);
        }

        var dataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 3,
            PhotometricInterpretation.Rgb,
            bitsAllocated: 16,
            bitsStored: 12,
            highBit: 11,
            planarConfiguration: PlanarConfiguration.Interleaved,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            frame);
        return DicomPixelData.Create(dataset);
    }

    private static DicomPixelData CreateRgb12Planar(DicomPixelData interleaved)
    {
        var interleavedBytes = ToArray(interleaved.GetFrame(0));
        var pixelCount = interleaved.Width * interleaved.Height;
        var planarBytes = new byte[interleavedBytes.Length];
        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            for (var component = 0; component < 3; component++)
            {
                var sourceOffset = (pixel * 3 + component) * 2;
                var targetOffset = (component * pixelCount + pixel) * 2;
                planarBytes[targetOffset] = interleavedBytes[sourceOffset];
                planarBytes[targetOffset + 1] = interleavedBytes[sourceOffset + 1];
            }
        }

        var dataset = DicomPixelDataFixtures.CreateBaseDataset(
            interleaved.Height,
            interleaved.Width,
            samplesPerPixel: 3,
            PhotometricInterpretation.Rgb,
            bitsAllocated: 16,
            bitsStored: 12,
            highBit: 11,
            planarConfiguration: PlanarConfiguration.Planar,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
            planarBytes);
        return DicomPixelData.Create(dataset);
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static DicomPixelData CreateRgbTargetPixelData(DicomPixelData source)
    {
        var dataset = CreateTargetDataset(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
        dataset.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        return DicomPixelData.Create(dataset, true);
    }

    private static DicomPixelData CreateNarrowYbrFull422Fixture(DicomPixelData source, int rows, int columns)
    {
        var dataset = CreateTargetDataset(source, DicomTransferSyntax.JPEGProcess1);
        dataset.AddOrUpdate(DicomTag.Rows, (ushort)rows);
        dataset.AddOrUpdate(DicomTag.Columns, (ushort)columns);
        var frame = ToArray(source.GetFrame(0));
        for (var index = 0; index + 8 < frame.Length; index++)
        {
            if (frame[index] != 0xff || frame[index + 1] != 0xc0)
            {
                continue;
            }

            frame[index + 5] = (byte)(rows >> 8);
            frame[index + 6] = (byte)rows;
            frame[index + 7] = (byte)(columns >> 8);
            frame[index + 8] = (byte)columns;
            var compressed = DicomPixelData.Create(dataset, true);
            compressed.AddFrame(new MemoryByteBuffer(frame));
            return compressed;
        }

        throw new Xunit.Sdk.XunitException("JPEG fixture does not contain an SOF0 marker.");
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        return DicomPixelData.Create(CreateTargetDataset(source, transferSyntax), true);
    }

    private static DicomDataset CreateTargetDataset(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.PhotometricInterpretation, source.Dataset.GetSingleValue<string>(DicomTag.PhotometricInterpretation) },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, source.BitsAllocated },
            { DicomTag.BitsStored, source.BitsStored },
            { DicomTag.HighBit, source.HighBit },
            { DicomTag.PixelRepresentation, (ushort)source.PixelRepresentation },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
        };

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)source.PlanarConfiguration);
        }

        return dataset;
    }

    private static byte[] ToArray(IByteBuffer buffer)
    {
        var bytes = new byte[buffer.Size];
        System.Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] GetSofSamplingFactors(byte[] jpeg)
    {
        for (var index = 0; index + 9 < jpeg.Length; index++)
        {
            if (jpeg[index] != 0xff
                || (jpeg[index + 1] != JpegMarker.SOF0 && jpeg[index + 1] != JpegMarker.SOF1))
            {
                continue;
            }

            var componentCount = jpeg[index + 9];
            var samplingFactors = new byte[componentCount];
            for (var component = 0; component < componentCount; component++)
            {
                samplingFactors[component] = jpeg[index + 11 + component * 3];
            }

            return samplingFactors;
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain a sequential SOF marker.");
    }

    private static int ReadSofPrecision(byte[] jpeg)
    {
        for (var index = 0; index + 4 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xff && (jpeg[index + 1] == JpegMarker.SOF0 || jpeg[index + 1] == JpegMarker.SOF1))
            {
                return jpeg[index + 4];
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain a sequential SOF marker.");
    }

    private static byte[] ConvertFirstDqtTo16Bit(byte[] jpeg)
    {
        var dqtOffset = FindMarker(jpeg, JpegMarker.DQT);
        var segmentLength = (jpeg[dqtOffset + 2] << 8) | jpeg[dqtOffset + 3];
        var payloadLength = segmentLength - 2;
        Assert.Equal(65, payloadLength);
        var result = new byte[jpeg.Length + 64];
        Buffer.BlockCopy(jpeg, 0, result, 0, dqtOffset + 2);
        result[dqtOffset + 2] = 0;
        result[dqtOffset + 3] = 131;
        result[dqtOffset + 4] = (byte)(0x10 | (jpeg[dqtOffset + 4] & 0x0F));
        var output = dqtOffset + 5;
        for (var index = 0; index < 64; index++)
        {
            result[output++] = 0;
            result[output++] = jpeg[dqtOffset + 5 + index];
        }

        Buffer.BlockCopy(
            jpeg,
            dqtOffset + 2 + segmentLength,
            result,
            output,
            jpeg.Length - (dqtOffset + 2 + segmentLength));
        var sofOffset = FindMarker(result, JpegMarker.SOF0);
        result[sofOffset + 1] = JpegMarker.SOF1;
        return result;
    }

    private static int FindMarker(byte[] jpeg, byte marker)
    {
        var offset = FindMarkerOffset(jpeg, marker);
        if (offset >= 0)
        {
            return offset;
        }

        throw new Xunit.Sdk.XunitException($"JPEG frame does not contain marker 0x{marker:X2}.");
    }

    private static int FindMarkerOffset(byte[] jpeg, byte marker)
    {
        for (var index = 0; index + 1 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xFF && jpeg[index + 1] == marker)
            {
                return index;
            }
        }

        return -1;
    }
}
