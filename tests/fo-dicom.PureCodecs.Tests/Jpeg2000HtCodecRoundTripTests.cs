using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000HtCodecRoundTripTests
{
    [Fact]
    public void Htj2k_lossless_round_trips_8_bit_monochrome_exactly()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(source.GetFrame(0).Data, decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Htj2k_lossless_rpcl_round_trips_exactly_and_writes_rpcl_progression()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 4, columns: 4);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLosslessRPCL);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomHtJpeg2000LosslessRpclCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(source.GetFrame(0).Data, decoded.GetFrame(0).Data);
        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Htj2k_lossy_round_trips_8_bit_monochrome_with_tolerance()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2K);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomHtJpeg2000LossyCodec();

        codec.Encode(source, compressed, new DicomHtJpeg2000Params { TargetRatio = 3.0 });
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(source, decoded, tolerance: 2);
    }

    [Fact]
    public void Htj2k_lossless_preserves_frame_count_and_required_compression_tags()
    {
        var dataset = DicomPixelDataFixtures.CreateMultiFrameMonochrome8(rows: 2, columns: 3, frameCount: 3);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(3, compressed.NumberOfFrames);
        Assert.Equal(3, decoded.NumberOfFrames);
        Assert.Equal("3", compressedDataset.GetSingleValue<string>(DicomTag.NumberOfFrames));
        Assert.Equal(PhotometricInterpretation.Monochrome2.Value, compressed.PhotometricInterpretation.Value);
        Assert.Equal((ushort)8, compressed.BitsAllocated);
        Assert.Equal((ushort)8, compressed.BitsStored);
        for (var frame = 0; frame < source.NumberOfFrames; frame++)
        {
            Assert.Equal(source.GetFrame(frame).Data, decoded.GetFrame(frame).Data);
        }
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
