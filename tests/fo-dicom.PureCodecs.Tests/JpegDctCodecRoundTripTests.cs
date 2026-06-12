using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegDctCodecRoundTripTests
{
    [Fact]
    public void Process1_round_trip_preserves_8_bit_monochrome_with_tolerance()
    {
        AssertRoundTrip(new DicomJpegProcess1Codec(), DicomTransferSyntax.JPEGProcess1);
    }

    [Fact]
    public void Process2_4_round_trip_preserves_8_bit_monochrome_with_tolerance()
    {
        AssertRoundTrip(new DicomJpegProcess2_4Codec(), DicomTransferSyntax.JPEGProcess2_4);
    }

    [Fact]
    public void Process1_rejects_16_bit_source()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome16());
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters()));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("BitsAllocated", exception.Message);
    }

    [Fact]
    public void Process2_4_12_bit_path_currently_fails_with_managed_exception_when_fixture_is_not_available()
    {
        var codec = new DicomJpegProcess2_4Codec();
        var rawPixelData = CreateMonochrome12PixelData();
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess2_4);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters()));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("BitsAllocated", exception.Message);
    }

    private static void AssertRoundTrip(IDicomCodec codec, DicomTransferSyntax syntax)
    {
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 16, columns: 16));
        var compressedPixelData = CreateTargetPixelData(rawPixelData, syntax);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(rawPixelData, decodedPixelData, tolerance: 20);
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

        return DicomPixelData.Create(dataset, true);
    }

    private static DicomPixelData CreateMonochrome12PixelData()
    {
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

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(new byte[24]));
        return pixelData;
    }
}
