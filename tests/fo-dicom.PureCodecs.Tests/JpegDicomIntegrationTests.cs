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

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegDicomIntegrationTests
{
    [Fact]
    public void Default_jpeg_parameters_match_fo_dicom_color_conversion_default()
    {
        var parameters = Assert.IsType<JpegCodecParams>(new DicomJpegProcess1Codec().GetDefaultParameters());

        Assert.Equal(90, parameters.Quality);
        Assert.True(parameters.ConvertColorspaceToRGB);
        Assert.Equal(1, parameters.Predictor);
        Assert.Equal(0, parameters.PointTransform);
    }

    [Fact]
    public void Ybr_full_to_rgb_conversion_maps_samples()
    {
        var rgb = JpegColorConverter.YbrFullToRgb(new byte[] { 100, 128, 128, 76, 84, 255 });

        Assert.Equal(new byte[] { 100, 100, 100, 254, 0, 0 }, rgb);
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

    private static DicomPixelData CreateRgbTargetPixelData(DicomPixelData source)
    {
        var dataset = CreateTargetDataset(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
        dataset.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        return DicomPixelData.Create(dataset, true);
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
            if (jpeg[index] != 0xff || jpeg[index + 1] != 0xc0)
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

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain an SOF0 marker.");
    }
}
