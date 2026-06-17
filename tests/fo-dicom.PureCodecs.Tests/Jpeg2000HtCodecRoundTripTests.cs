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
    public void Htj2k_lossless_default_progression_matches_openjph_default_rpcl()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());

        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Fact]
    public void Htj2k_lossless_and_lossy_ignore_progression_parameters_like_fo_dicom_codecs()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var losslessDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var lossless = DicomPixelData.Create(losslessDataset, true);
        var lossyDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2K);
        var lossy = DicomPixelData.Create(lossyDataset, true);
        var parameters = new DicomHtJpeg2000Params { Jpeg2000ProgressionOrder = Jpeg2000ProgressionOrder.CPRL };

        new DicomHtJpeg2000LosslessCodec().Encode(source, lossless, parameters);
        new DicomHtJpeg2000LossyCodec().Encode(source, lossy, parameters);

        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(lossless.GetFrame(0).Data));
        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, ReadProgressionOrder(lossy.GetFrame(0).Data));
    }

    [Fact]
    public void Htj2k_lossless_rpcl_honors_progression_parameter_like_fo_dicom_codecs()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLosslessRPCL);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var parameters = new DicomHtJpeg2000Params { Jpeg2000ProgressionOrder = Jpeg2000ProgressionOrder.CPRL };

        new DicomHtJpeg2000LosslessRpclCodec().Encode(source, compressed, parameters);

        Assert.Equal(Jpeg2000ProgressionOrder.CPRL, ReadProgressionOrder(compressed.GetFrame(0).Data));
    }

    [Theory]
    [InlineData(Jpeg2000ProgressionOrder.LRCP, 6)]
    [InlineData(Jpeg2000ProgressionOrder.RLCP, 6)]
    [InlineData(Jpeg2000ProgressionOrder.RPCL, 6)]
    [InlineData(Jpeg2000ProgressionOrder.PCRL, 1)]
    [InlineData(Jpeg2000ProgressionOrder.CPRL, 1)]
    public void Htj2k_lossless_rpcl_matches_openjph_tilepart_division_corrections(
        Jpeg2000ProgressionOrder progressionOrder,
        int expectedTilePartCount)
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLosslessRPCL);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var parameters = new DicomHtJpeg2000Params { Jpeg2000ProgressionOrder = progressionOrder };

        new DicomHtJpeg2000LosslessRpclCodec().Encode(source, compressed, parameters);
        var codestream = compressed.GetFrame(0).Data;

        Assert.Equal(progressionOrder, ReadProgressionOrder(codestream));
        Assert.Equal(expectedTilePartCount, ReadStartOfTiles(codestream).Count);
        Assert.All(ReadStartOfTiles(codestream), sot => Assert.Equal(expectedTilePartCount, sot.TilePartCount));
        Assert.Equal(2 + (expectedTilePartCount * 6), ReadTlmPayload(codestream).Length);
    }

    [Theory]
    [InlineData(4, 4, "1.2.840.10008.1.2.4.201", Jpeg2000ProgressionOrder.RPCL)]
    [InlineData(32, 32, "1.2.840.10008.1.2.4.202", Jpeg2000ProgressionOrder.CPRL)]
    [InlineData(32, 32, "1.2.840.10008.1.2.4.203", Jpeg2000ProgressionOrder.RPCL)]
    public void Htj2k_codestream_shape_matches_fo_dicom_codecs_openjph_defaults(
        ushort rows,
        ushort columns,
        string transferSyntaxUid,
        Jpeg2000ProgressionOrder expectedProgressionOrder)
    {
        var transferSyntax = DicomTransferSyntax.Parse(transferSyntaxUid);
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows, columns);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, transferSyntax);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = CreateCodec(transferSyntax);

        codec.Encode(source, compressed, new DicomHtJpeg2000Params
        {
            Jpeg2000ProgressionOrder = Jpeg2000ProgressionOrder.CPRL,
            NumLayers = 3,
            TargetRatio = 12
        });
        var codestream = compressed.GetFrame(0).Data;
        var coding = ReadCodingStyle(codestream);

        Assert.Equal(expectedProgressionOrder, coding.ProgressionOrder);
        Assert.Equal(1, coding.LayerCount);
        Assert.Equal(5, coding.DecompositionLevels);
        Assert.Equal(64, coding.CodeBlockWidth);
        Assert.Equal(64, coding.CodeBlockHeight);
        Assert.Equal(0x40, coding.CodeBlockStyle);
        Assert.Contains(Jpeg2000Marker.TLM, ReadMarkerCodes(codestream));
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
    public void Htj2k_lossless_codestream_ends_at_eoc_without_fragment_padding()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome16(rows: 459, columns: 888);
        var source = DicomPixelData.Create(dataset);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        var codec = new DicomHtJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        var codestream = compressed.GetFrame(0).Data;

        Assert.True(StartsWith(codestream, new byte[] { 0xFF, Jpeg2000Marker.SOC }));
        Assert.Equal(codestream.Length - 2, LastMarkerOffset(codestream, Jpeg2000Marker.EOC));
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

    private static IReadOnlyList<Jpeg2000StartOfTilePart> ReadStartOfTiles(byte[] codestream)
    {
        var tiles = new List<Jpeg2000StartOfTilePart>();
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.SOT)
            {
                var sot = Jpeg2000StartOfTilePart.Parse(segment, tileCount: 1);
                tiles.Add(sot);
                Assert.Equal(Jpeg2000Marker.SOD, reader.ReadNext().Code);
                _ = reader.ReadTileData(sot);
            }
            else if (segment.Code == Jpeg2000Marker.EOC)
            {
                break;
            }
        }

        return tiles;
    }

    private static byte[] ReadTlmPayload(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.TLM)
            {
                return segment.Payload;
            }
        }

        throw new Xunit.Sdk.XunitException("TLM marker not found.");
    }

    private static FellowOakDicom.Imaging.Codec.IDicomCodec CreateCodec(DicomTransferSyntax transferSyntax)
    {
        if (transferSyntax == DicomTransferSyntax.HTJ2KLossless)
        {
            return new DicomHtJpeg2000LosslessCodec();
        }

        if (transferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL)
        {
            return new DicomHtJpeg2000LosslessRpclCodec();
        }

        if (transferSyntax == DicomTransferSyntax.HTJ2K)
        {
            return new DicomHtJpeg2000LossyCodec();
        }

        throw new ArgumentException("Unsupported HTJ2K transfer syntax.", nameof(transferSyntax));
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

    private static int LastMarkerOffset(byte[] bytes, byte marker)
    {
        for (var index = bytes.Length - 2; index >= 0; index--)
        {
            if (bytes[index] == 0xFF && bytes[index + 1] == marker)
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"Marker 0xFF{marker:X2} not found.");
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
