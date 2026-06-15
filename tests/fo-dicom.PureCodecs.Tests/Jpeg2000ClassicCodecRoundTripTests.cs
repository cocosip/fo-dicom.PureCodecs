using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000ClassicCodecRoundTripTests
{
    [Fact]
    public void Jpeg2000_lossless_round_trips_8_bit_monochrome_exactly()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(source.GetFrame(0).Data, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Jpeg2000_lossless_round_trips_16_bit_monochrome_exactly()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome16(rows: 4, columns: 5);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(source.GetFrame(0).Data, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Jpeg2000_lossy_round_trips_8_bit_monochrome_with_tolerance()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new DicomJpeg2000Params { Irreversible = true, TargetRatio = 3.0, NumLayers = 2 });
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(source, decoded, tolerance: 16);
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
