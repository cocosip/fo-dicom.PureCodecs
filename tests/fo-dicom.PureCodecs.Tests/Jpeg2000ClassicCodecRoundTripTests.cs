using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpeg2000LosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000LosslessCodec;

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

    [Fact]
    public void Jpeg2000_lossless_decodes_multi_tile_codestream_accepted_by_fo_dicom_codecs()
    {
        const ushort rows = 32;
        const ushort tileColumns = 32;
        const ushort columns = tileColumns * 2;
        var frame = new byte[rows * columns];
        for (var index = 0; index < frame.Length; index++)
        {
            frame[index] = (byte)((index * 29 + index / columns * 17) % 251);
        }

        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows, columns, frame);
        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        compressed.AddFrame(new MemoryByteBuffer(CreateTwoTileCodestream(frame, rows, columns, tileColumns)));
        var nativeDecoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var pureDecoded = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);

        var nativeCodec = new NativeJpeg2000LosslessCodec();
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, nativeDecoded);

        var pureCodec = new DicomJpeg2000LosslessCodec();
        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, pureDecoded);
    }

    private static byte[] CreateTwoTileCodestream(byte[] frame, ushort rows, ushort columns, ushort tileColumns)
    {
        var tileCodestreams = new byte[2][];
        for (var tile = 0; tile < tileCodestreams.Length; tile++)
        {
            var tileFrame = new byte[rows * tileColumns];
            for (var row = 0; row < rows; row++)
            {
                Buffer.BlockCopy(
                    frame,
                    row * columns + tile * tileColumns,
                    tileFrame,
                    row * tileColumns,
                    tileColumns);
            }

            var tileDataset = DicomPixelDataFixtures.CreateMonochrome8(rows, tileColumns, tileFrame);
            var tileSource = DicomPixelData.Create(tileDataset);
            var tileCompressed = DicomPixelData.Create(CloneForTransferSyntax(tileDataset, DicomTransferSyntax.JPEG2000Lossless), true);
            var codec = new DicomJpeg2000LosslessCodec();
            codec.Encode(tileSource, tileCompressed, new DicomJpeg2000Params
            {
                Irreversible = false,
                Rate = 0,
                RateLevels = Array.Empty<int>(),
                AllowMCT = false
            });
            tileCodestreams[tile] = tileCompressed.GetFrame(0).Data;
        }

        var first = ReadSingleTileCodestream(tileCodestreams[0]);
        var second = ReadSingleTileCodestream(tileCodestreams[1]);
        var writer = new Jpeg2000CodestreamWriter();
        writer.WriteStandalone(Jpeg2000Marker.SOC);
        foreach (var segment in first.MainHeader)
        {
            var payload = segment.Code == Jpeg2000Marker.SIZ
                ? WithReferenceGridWidth(segment.Payload, columns)
                : segment.Payload;
            writer.WriteSegment(segment.Code, payload);
        }

        var tileHeader = first.MainHeader
            .Where(segment => segment.Code == Jpeg2000Marker.COD || segment.Code == Jpeg2000Marker.QCD)
            .ToArray();
        WriteTile(writer, tileIndex: 0, first.TileData, tileHeader);
        WriteTile(writer, tileIndex: 1, second.TileData, tileHeader);
        writer.WriteStandalone(Jpeg2000Marker.EOC);
        var codestream = writer.ToArray();
        if ((codestream.Length & 1) == 0)
        {
            return codestream;
        }

        var padded = new byte[codestream.Length + 1];
        Buffer.BlockCopy(codestream, 0, padded, 0, codestream.Length);
        return padded;
    }

    private static (IReadOnlyList<Jpeg2000MarkerSegment> MainHeader, byte[] TileData) ReadSingleTileCodestream(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        Assert.Equal(Jpeg2000Marker.SOC, reader.ReadNext().Code);
        var mainHeader = new List<Jpeg2000MarkerSegment>();
        Jpeg2000StartOfTilePart? startOfTile = null;
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.SOT)
            {
                startOfTile = Jpeg2000StartOfTilePart.Parse(segment, tileCount: 1);
                continue;
            }

            if (segment.Code == Jpeg2000Marker.SOD)
            {
                Assert.NotNull(startOfTile);
                return (mainHeader, reader.ReadTileData(startOfTile));
            }

            mainHeader.Add(segment);
        }

        throw new InvalidOperationException("Single-tile JPEG 2000 codestream is missing SOD.");
    }

    private static byte[] WithReferenceGridWidth(byte[] sizePayload, uint width)
    {
        var payload = (byte[])sizePayload.Clone();
        payload[2] = (byte)(width >> 24);
        payload[3] = (byte)(width >> 16);
        payload[4] = (byte)(width >> 8);
        payload[5] = (byte)width;
        return payload;
    }

    private static void WriteTile(
        Jpeg2000CodestreamWriter writer,
        int tileIndex,
        byte[] tileData,
        IReadOnlyList<Jpeg2000MarkerSegment> tileHeader)
    {
        var tileHeaderLength = tileHeader.Sum(segment => segment.Payload.Length + 4);
        var tilePartLength = tileData.Length + tileHeaderLength + 14;
        writer.WriteSegment(Jpeg2000Marker.SOT, new[]
        {
            (byte)(tileIndex >> 8),
            (byte)tileIndex,
            (byte)(tilePartLength >> 24),
            (byte)(tilePartLength >> 16),
            (byte)(tilePartLength >> 8),
            (byte)tilePartLength,
            (byte)0,
            (byte)1
        });
        foreach (var segment in tileHeader)
        {
            writer.WriteSegment(segment.Code, segment.Payload);
        }

        writer.WriteStandalone(Jpeg2000Marker.SOD);
        writer.WriteRaw(tileData);
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
