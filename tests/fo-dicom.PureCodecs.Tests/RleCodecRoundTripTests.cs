using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Rle;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class RleCodecRoundTripTests
{
    [Fact]
    public void Rle_round_trip_preserves_8_bit_monochrome_frame()
    {
        AssertRoundTrip(DicomPixelDataFixtures.CreateMonochrome8());
    }

    [Fact]
    public void Rle_round_trip_preserves_16_bit_monochrome_frame()
    {
        AssertRoundTrip(DicomPixelDataFixtures.CreateMonochrome16());
    }

    [Fact]
    public void Rle_round_trip_preserves_rgb_interleaved_frame()
    {
        AssertRoundTrip(DicomPixelDataFixtures.CreateRgbInterleaved());
    }

    [Fact]
    public void Rle_round_trip_preserves_rgb_planar_frame()
    {
        AssertRoundTrip(DicomPixelDataFixtures.CreateRgbPlanar());
    }

    [Fact]
    public void Rle_round_trip_preserves_multi_frame_pixel_data()
    {
        AssertRoundTrip(DicomPixelDataFixtures.CreateMultiFrameMonochrome8());
    }

    [Fact]
    public void Rle_encode_rejects_more_than_15_segments()
    {
        var oldPixelData = CreateUnsupportedSegmentPixelData();
        var newPixelData = CreateTargetPixelData(oldPixelData, DicomTransferSyntax.RLELossless);
        var codec = new DicomRleLosslessCodec();

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Encode(oldPixelData, newPixelData, codec.GetDefaultParameters()));

        Assert.Contains("RLE Lossless", exception.Message);
        Assert.Contains("segment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rle_dicom_file_can_be_saved_and_reopened()
    {
        var rawDataset = DicomPixelDataFixtures.CreateMonochrome8();
        var rawPixelData = DicomPixelData.Create(rawDataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.RLELossless);
        var codec = new DicomRleLosslessCodec();
        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());

        var path = Path.Combine(Path.GetTempPath(), $"purecodecs-rle-{Guid.NewGuid():N}.dcm");
        try
        {
            new DicomFile(compressedPixelData.Dataset).Save(path);

            var reopened = DicomFile.Open(path);
            var reopenedPixelData = DicomPixelData.Create(reopened.Dataset);
            var decodedPixelData = CreateTargetPixelData(reopenedPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);
            codec.Decode(reopenedPixelData, decodedPixelData, codec.GetDefaultParameters());

            Assert.Same(DicomTransferSyntax.RLELossless, reopened.Dataset.InternalTransferSyntax);
            PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Rle_issue_regression_preserves_16_bit_low_entropy_single_row_frames()
    {
        var random = new Random(53717);
        var codec = new DicomRleLosslessCodec();

        for (var width = 1; width < 1024; width++)
        {
            var bytes = new byte[width * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)(random.Next() % 2);
            }

            var rawPixelData = CreateSignedMonochrome16PixelData(width, bytes);
            var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.RLELossless);
            var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

            codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
            codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

            PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
        }
    }

    private static void AssertRoundTrip(DicomDataset rawDataset)
    {
        var rawPixelData = DicomPixelData.Create(rawDataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.RLELossless);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codec = new DicomRleLosslessCodec();

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
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

    private static DicomPixelData CreateSignedMonochrome16PixelData(int width, byte[] frame)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)width },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)PixelRepresentation.Signed },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new MemoryByteBuffer(frame));
        return pixelData;
    }

    private static DicomPixelData CreateUnsupportedSegmentPixelData()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)1 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)16 },
            { DicomTag.HighBit, (ushort)15 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)8 },
            { DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new MemoryByteBuffer(new byte[16]));
        return pixelData;
    }
}
