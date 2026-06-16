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
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
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
    public void Htj2k_lossless_writes_standard_codestream_without_managed_payload()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        var codestream = compressed.GetFrame(0).Data;

        Assert.True(StartsWith(codestream, new byte[] { 0xFF, Jpeg2000Marker.SOC }));
        Assert.Contains(Jpeg2000Marker.CAP, ReadMarkerCodes(codestream));
        Assert.Equal(0x40, ReadCodingStyle(codestream).CodeBlockStyle);
        Assert.False(ContainsSubsequence(codestream, new byte[] { (byte)'P', (byte)'H', (byte)'T', (byte)'J' }));
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

        var maxDifference = PixelDataAssertions.MaxSampleDifference(source, decoded);
        Assert.InRange(maxDifference, 1, 120);
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

    [Theory]
    [InlineData((ushort)888, (ushort)459)]
    public void Htj2k_lossless_round_trips_16_bit_frames_larger_than_ushort_exactly(ushort columns, ushort rows)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome16(rows, columns);
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

    private static Jpeg2000ProgressionOrder ReadProgressionOrder(byte[] codestream)
    {
        return ReadCodingStyle(codestream).ProgressionOrder;
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

    private static IReadOnlyList<byte> ReadMarkerCodes(byte[] codestream)
    {
        var codes = new List<byte>();
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            codes.Add(segment.Code);
            if (segment.Code == Jpeg2000Marker.SOD || segment.Code == Jpeg2000Marker.EOC)
            {
                break;
            }
        }

        return codes;
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsSubsequence(byte[] bytes, byte[] subsequence)
    {
        for (var offset = 0; offset <= bytes.Length - subsequence.Length; offset++)
        {
            var matched = true;
            for (var i = 0; i < subsequence.Length; i++)
            {
                if (bytes[offset + i] != subsequence[i])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
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
