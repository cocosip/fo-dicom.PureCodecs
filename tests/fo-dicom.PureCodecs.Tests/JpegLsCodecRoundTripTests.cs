using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsCodecRoundTripTests
{
    [Fact]
    public void Default_parameters_match_fo_dicom_codecs_near_lossless_error()
    {
        var parameters = new DicomJpegLsNearLosslessCodec().GetDefaultParameters();
        var jpegLsParameters = Assert.IsType<DicomJpegLsParams>(parameters);

        Assert.Equal(2, jpegLsParameters.AllowedError);
    }

    [Fact]
    public void Lossless_round_trip_preserves_8_bit_monochrome_samples()
    {
        AssertLosslessRoundTrip(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 }));
    }

    [Fact]
    public void Lossless_round_trip_preserves_16_bit_monochrome_samples()
    {
        AssertLosslessRoundTrip(DicomPixelDataFixtures.CreateMonochrome16(frame: CreateUInt16Frame(1000, 1010, 65000, 65010, 32000, 32001, 1, 0, 40000, 41000, 42000, 43000)));
    }

    [Fact]
    public void Lossless_round_trip_preserves_rgb_interleaved_samples()
    {
        AssertLosslessRoundTrip(DicomPixelDataFixtures.CreateRgbInterleaved(frame: new byte[]
        {
            10, 20, 30, 11, 21, 31, 12, 22, 32,
            13, 23, 33, 14, 24, 34, 15, 25, 35,
            16, 26, 36, 17, 27, 37, 18, 28, 38,
        }));
    }

    [Fact]
    public void Lossless_round_trip_preserves_multi_frame_data()
    {
        AssertLosslessRoundTrip(DicomPixelDataFixtures.CreateMultiFrameMonochrome8());
    }

    [Fact]
    public void Near_lossless_round_trip_preserves_8_bit_samples_within_allowed_error()
    {
        AssertNearLosslessRoundTrip(DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 }), allowedError: 2);
    }

    [Fact]
    public void Near_lossless_round_trip_preserves_16_bit_samples_within_allowed_error()
    {
        AssertNearLosslessRoundTrip(DicomPixelDataFixtures.CreateMonochrome16(frame: CreateUInt16Frame(1000, 1010, 1025, 1030, 2048, 2050, 3000, 3002, 4000, 4001, 5000, 5002)), allowedError: 2);
    }

    [Fact]
    public void Near_lossless_encode_pads_odd_length_jpeg_ls_frames_to_even_length()
    {
        const string inputPath = @"D:\1.dcm";
        if (!File.Exists(inputPath))
        {
            return;
        }

        var codec = new DicomJpegLsNearLosslessCodec();
        var rawPixelData = DicomPixelData.Create(DicomFile.Open(inputPath, FileReadOption.ReadAll).Dataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGLSNearLossless);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        var frame = compressedPixelData.GetFrame(0).Data;

        Assert.Equal(0, frame.Length % 2);
        Assert.Equal(0, frame[frame.Length - 1]);
        Assert.Equal(0xD9, frame[frame.Length - 2]);
        Assert.Equal(0xFF, frame[frame.Length - 3]);
    }

    [Fact]
    public void Near_lossless_tolerance_assertion_compares_16_bit_sample_values()
    {
        var expected = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome16(frame: CreateUInt16Frame(0x0100)));
        var actual = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome16(frame: CreateUInt16Frame(0x00FF)));

        PixelDataAssertions.FramesMatchWithinTolerance(expected, actual, tolerance: 1);
    }

    [Fact]
    public void Lossless_rejects_ybr_partial_422_photometric_interpretation()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.YbrPartial422.Value },
            { DicomTag.Rows, (ushort)2 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)3 },
            { DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved },
        };
        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(new byte[12]));
        var source = DicomPixelData.Create(dataset);
        var target = CreateTargetPixelData(source, DicomTransferSyntax.JPEGLSLossless);
        var codec = new DicomJpegLsLosslessCodec();

        void Encode() => codec.Encode(source, target, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Encode);

        Assert.Contains("Photometric", exception.Message);
    }

    [Fact]
    public void Near_lossless_rejects_negative_allowed_error()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8());
        var target = CreateTargetPixelData(source, DicomTransferSyntax.JPEGLSNearLossless);
        var codec = new DicomJpegLsNearLosslessCodec();
        var parameters = new DicomJpegLsParams { AllowedError = -1 };

        void Encode() => codec.Encode(source, target, parameters);

        var exception = Assert.Throws<DicomCodecException>((Action)Encode);

        Assert.Contains("AllowedError", exception.Message);
    }

    private static void AssertLosslessRoundTrip(DicomDataset dataset)
    {
        var codec = new DicomJpegLsLosslessCodec();
        var rawPixelData = DicomPixelData.Create(dataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGLSLossless);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
    }

    private static void AssertNearLosslessRoundTrip(DicomDataset dataset, int allowedError)
    {
        var codec = new DicomJpegLsNearLosslessCodec();
        var rawPixelData = DicomPixelData.Create(dataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGLSNearLossless);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);
        var parameters = new DicomJpegLsParams { AllowedError = allowedError };

        codec.Encode(rawPixelData, compressedPixelData, parameters);
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(rawPixelData, decodedPixelData, allowedError);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
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

    private static byte[] CreateUInt16Frame(params int[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            bytes[index * 2] = (byte)samples[index];
            bytes[index * 2 + 1] = (byte)(samples[index] >> 8);
        }

        return bytes;
    }
}
