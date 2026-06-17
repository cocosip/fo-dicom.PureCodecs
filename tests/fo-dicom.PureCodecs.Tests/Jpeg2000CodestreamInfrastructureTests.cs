using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000CodestreamInfrastructureTests
{
    [Fact]
    public void Marker_constants_expose_common_jpeg2000_markers()
    {
        Assert.Equal(0x4F, Jpeg2000Marker.SOC);
        Assert.Equal(0x51, Jpeg2000Marker.SIZ);
        Assert.Equal(0x52, Jpeg2000Marker.COD);
        Assert.Equal(0x53, Jpeg2000Marker.COC);
        Assert.Equal(0x5C, Jpeg2000Marker.QCD);
        Assert.Equal(0x5D, Jpeg2000Marker.QCC);
        Assert.Equal(0x5F, Jpeg2000Marker.POC);
        Assert.Equal(0x5E, Jpeg2000Marker.RGN);
        Assert.Equal(0x50, Jpeg2000Marker.CAP);
        Assert.Equal(0x59, Jpeg2000Marker.CPF);
        Assert.Equal(0x64, Jpeg2000Marker.COM);
        Assert.Equal(0x90, Jpeg2000Marker.SOT);
        Assert.Equal(0x93, Jpeg2000Marker.SOD);
        Assert.Equal(0xD9, Jpeg2000Marker.EOC);
    }

    [Fact]
    public void Reader_parses_soc_sod_and_eoc_markers()
    {
        var reader = new Jpeg2000CodestreamReader(new byte[]
        {
            0xFF, Jpeg2000Marker.SOC,
            0xFF, Jpeg2000Marker.SOD,
            0xFF, Jpeg2000Marker.EOC
        });

        var soc = reader.ReadNext();
        var sod = reader.ReadNext();
        var eoc = reader.ReadNext();

        Assert.Equal(Jpeg2000Marker.SOC, soc.Code);
        Assert.False(soc.HasPayload);
        Assert.Equal(Jpeg2000Marker.SOD, sod.Code);
        Assert.False(sod.HasPayload);
        Assert.Equal(Jpeg2000Marker.EOC, eoc.Code);
        Assert.False(eoc.HasPayload);
        Assert.True(reader.EndOfData);
    }

    [Fact]
    public void Reader_parses_siz_payload()
    {
        var reader = new Jpeg2000CodestreamReader(CreateSegment(
            Jpeg2000Marker.SIZ,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x20,
            0x00, 0x00, 0x00, 0x10,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x10,
            0x00, 0x00, 0x00, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x02,
            0x07, 0x01, 0x01,
            0x8F, 0x01, 0x01));

        var siz = Jpeg2000SizeSegment.Parse(reader.ReadNext());

        Assert.Equal(32u, siz.ReferenceGridWidth);
        Assert.Equal(16u, siz.ReferenceGridHeight);
        Assert.Equal(16u, siz.TileWidth);
        Assert.Equal(8u, siz.TileHeight);
        Assert.Equal(2, siz.Components.Count);
        Assert.False(siz.Components[0].IsSigned);
        Assert.Equal(8, siz.Components[0].Precision);
        Assert.True(siz.Components[1].IsSigned);
        Assert.Equal(16, siz.Components[1].Precision);
    }

    [Fact]
    public void Reader_parses_cod_payload()
    {
        var cod = Jpeg2000CodingStyleDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.COD,
            new byte[] { 0x01, 0x00, 0x00, 0x03, 0x00, 0x01, 0x04, 0x04, 0x00, 0x01, 0xAA, 0xBB }));

        Assert.True(cod.HasPrecinctSizes);
        Assert.Equal(Jpeg2000ProgressionOrder.LRCP, cod.ProgressionOrder);
        Assert.Equal(3, cod.LayerCount);
        Assert.False(cod.UsesMultipleComponentTransform);
        Assert.Equal(1, cod.DecompositionLevels);
        Assert.Equal(64, cod.CodeBlockWidth);
        Assert.Equal(64, cod.CodeBlockHeight);
        Assert.Equal(2, cod.PrecinctSizes.Count);
    }

    [Fact]
    public void COC_inherits_component_coding_style_from_default_COD()
    {
        var cod = Jpeg2000CodingStyleDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.COD,
            new byte[] { 0x00, 0x02, 0x00, 0x02, 0x00, 0x01, 0x03, 0x03, 0x00, 0x01 }));
        var coc = Jpeg2000CodingStyleComponent.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.COC,
            new byte[] { 0x01, 0x01, 0x01, 0x04, 0x04, 0x00, 0x01, 0xAA, 0xBB }),
            componentCount: 2);

        var inherited = coc.InheritFrom(cod);

        Assert.Equal(1, inherited.ComponentIndex);
        Assert.Equal(cod.ProgressionOrder, inherited.ProgressionOrder);
        Assert.Equal(cod.LayerCount, inherited.LayerCount);
        Assert.Equal(64, inherited.CodeBlockWidth);
        Assert.Equal(64, inherited.CodeBlockHeight);
        Assert.Equal(2, inherited.PrecinctSizes.Count);
    }

    [Fact]
    public void Reader_parses_qcd_payload()
    {
        var qcd = Jpeg2000QuantizationDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCD,
            new byte[] { 0x21, 0x12, 0x34, 0x56, 0x78 }));

        Assert.Equal(Jpeg2000QuantizationStyle.ScalarDerived, qcd.Style);
        Assert.Equal(1, qcd.GuardBits);
        Assert.Equal(new ushort[] { 0x1234, 0x5678 }, qcd.StepSizes);
    }

    [Fact]
    public void QCC_inherits_component_quantization_from_default_QCD()
    {
        var qcd = Jpeg2000QuantizationDefault.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCD,
            new byte[] { 0x00, 0x05, 0x06 }));
        var qcc = Jpeg2000QuantizationComponent.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.QCC,
            new byte[] { 0x01, 0x21, 0x12, 0x34 }),
            componentCount: 2);

        var inherited = qcc.InheritFrom(qcd);

        Assert.Equal(1, inherited.ComponentIndex);
        Assert.Equal(Jpeg2000QuantizationStyle.ScalarDerived, inherited.Style);
        Assert.Equal(0x1234, Assert.Single(inherited.StepSizes));
        Assert.Equal(qcd.GuardBits, inherited.DefaultGuardBits);
    }

    [Fact]
    public void POC_rejects_unknown_progression_order()
    {
        var exception = Assert.Throws<DicomCodecException>(() =>
            Jpeg2000ProgressionOrderChange.Parse(new Jpeg2000MarkerSegment(
                Jpeg2000Marker.POC,
                new byte[] { 0, 0, 1, 0, 0, 1, 0xFF }),
                componentCount: 2));

        Assert.Contains("progression", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void POC_parses_progression_order_changes()
    {
        var changes = Jpeg2000ProgressionOrderChange.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.POC,
            new byte[] { 0, 2, 0, 0, 3, 2, 2 }),
            componentCount: 2);

        var change = Assert.Single(changes);
        Assert.Equal(0, change.LayerStart);
        Assert.Equal(2, change.ResolutionEnd);
        Assert.Equal(0, change.ComponentStart);
        Assert.Equal(3, change.LayerEnd);
        Assert.Equal(2, change.ComponentEnd);
        Assert.Equal(Jpeg2000ProgressionOrder.RPCL, change.ProgressionOrder);
    }

    [Fact]
    public void RGN_parses_marker_and_documents_unsupported_ROI_behavior()
    {
        var rgn = Jpeg2000RegionOfInterest.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.RGN,
            new byte[] { 0x01, 0x00, 0x03 }),
            componentCount: 2);

        Assert.Equal(1, rgn.ComponentIndex);
        Assert.Equal(0, rgn.Style);
        Assert.Equal(3, rgn.Shift);
        Assert.False(rgn.IsSupportedForDecoding);
        Assert.Contains("unsupported", rgn.UnsupportedBehavior);
    }

    [Fact]
    public void COM_preserves_binary_payload()
    {
        var comment = Jpeg2000Comment.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.COM,
            new byte[] { 0x00, 0x01, 0x66, 0x6F, 0x6F }));

        Assert.Equal(1, comment.RegistrationValue);
        Assert.Equal(new byte[] { 0x66, 0x6F, 0x6F }, comment.Payload);
    }

    [Fact]
    public void SOT_parses_and_validates_tile_part_indexes()
    {
        var sot = Jpeg2000StartOfTilePart.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.SOT,
            new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00, 0x10, 0x01, 0x03 }),
            tileCount: 4);

        Assert.Equal(2, sot.TileIndex);
        Assert.Equal(16u, sot.TilePartLength);
        Assert.Equal(1, sot.TilePartIndex);
        Assert.Equal(3, sot.TilePartCount);
    }

    [Fact]
    public void Reader_uses_sot_length_for_tile_data_that_contains_eoc_like_bytes()
    {
        var reader = new Jpeg2000CodestreamReader(new byte[]
        {
            0xFF, Jpeg2000Marker.SOT,
            0x00, 0x0A,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x12,
            0x00,
            0x01,
            0xFF, Jpeg2000Marker.SOD,
            0x01, 0xFF, Jpeg2000Marker.EOC, 0x02,
            0xFF, Jpeg2000Marker.EOC
        });
        var sot = Jpeg2000StartOfTilePart.Parse(reader.ReadNext(), tileCount: 1);
        Assert.Equal(Jpeg2000Marker.SOD, reader.ReadNext().Code);

        var tileData = reader.ReadTileData(sot);

        Assert.Equal(new byte[] { 0x01, 0xFF, Jpeg2000Marker.EOC, 0x02 }, tileData);
        Assert.Equal(Jpeg2000Marker.EOC, reader.ReadNext().Code);
    }

    [Fact]
    public void Reader_detects_raw_j2k_codestreams()
    {
        Assert.True(Jpeg2000CodestreamReader.IsRawCodestream(new byte[] { 0xFF, Jpeg2000Marker.SOC }));
        Assert.False(Jpeg2000CodestreamReader.IsRawCodestream(new byte[] { 0x00, 0x00, 0x00, 0x0C }));
    }

    [Fact]
    public void Reader_detects_jp2_wrapper_frames()
    {
        Assert.True(Jpeg2000CodestreamReader.IsJp2Wrapped(new byte[]
        {
            0x00, 0x00, 0x00, 0x0C,
            0x6A, 0x50, 0x20, 0x20,
            0x0D, 0x0A, 0x87, 0x0A
        }));
    }

    [Fact]
    public void Reader_rejects_jp2_wrapper_frames_explicitly()
    {
        var exception = Assert.Throws<DicomCodecException>(() =>
            Jpeg2000CodestreamReader.EnsureRawCodestream(new byte[]
            {
                0x00, 0x00, 0x00, 0x0C,
                0x6A, 0x50, 0x20, 0x20,
                0x0D, 0x0A, 0x87, 0x0A
            }));

        Assert.Contains("JP2", exception.Message);
        Assert.Contains("unsupported", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reader_rejects_invalid_marker_length()
    {
        var reader = new Jpeg2000CodestreamReader(new byte[] { 0xFF, Jpeg2000Marker.SIZ, 0x00, 0x01 });

        var exception = Assert.Throws<DicomCodecException>(() => reader.ReadNext());

        Assert.Contains("JPEG 2000", exception.Message);
        Assert.Contains("length", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SOT_rejects_invalid_tile_part_indexes()
    {
        Assert.Throws<DicomCodecException>(() =>
            Jpeg2000StartOfTilePart.Parse(new Jpeg2000MarkerSegment(
                Jpeg2000Marker.SOT,
                new byte[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x10, 0x00, 0x01 }),
                tileCount: 4));

        Assert.Throws<DicomCodecException>(() =>
            Jpeg2000StartOfTilePart.Parse(new Jpeg2000MarkerSegment(
                Jpeg2000Marker.SOT,
                new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00, 0x10, 0x03, 0x03 }),
                tileCount: 4));
    }

    [Fact]
    public void PLT_parses_packet_length_table()
    {
        var table = Jpeg2000PacketLengthTable.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.PLT,
            new byte[] { 0x02, 0x05, 0x81, 0x00, 0x81, 0x80, 0x00 }));

        Assert.Equal(2, table.Index);
        Assert.Equal(new uint[] { 5u, 128u, 16384u }, table.PacketLengths);
    }

    [Theory]
    [InlineData(Jpeg2000Marker.PPM)]
    [InlineData(Jpeg2000Marker.PPT)]
    public void Packed_packet_header_markers_fail_with_managed_rejection(byte marker)
    {
        var exception = Assert.Throws<DicomCodecException>(() =>
            Jpeg2000UnsupportedMarker.RejectPackedPacketHeaders(new Jpeg2000MarkerSegment(marker, new byte[] { 0x00 })));

        Assert.Contains("JPEG 2000", exception.Message);
        Assert.Contains("unsupported", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SOP_parses_packet_sequence_number()
    {
        var sop = Jpeg2000StartOfPacket.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.SOP,
            new byte[] { 0x00, 0x2A }));

        Assert.Equal(42, sop.SequenceNumber);
    }

    [Fact]
    public void EPH_is_recognized_as_standalone_marker()
    {
        var reader = new Jpeg2000CodestreamReader(new byte[] { 0xFF, Jpeg2000Marker.EPH });

        var eph = reader.ReadNext();

        Assert.Equal(Jpeg2000Marker.EPH, eph.Code);
        Assert.False(eph.HasPayload);
    }

    [Fact]
    public void Image_model_builds_multi_tile_geometry_from_SIZ()
    {
        var siz = Jpeg2000SizeSegment.Parse(new Jpeg2000MarkerSegment(
            Jpeg2000Marker.SIZ,
            new byte[]
            {
                0x00, 0x00,
                0x00, 0x00, 0x00, 0x20,
                0x00, 0x00, 0x00, 0x10,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x10,
                0x00, 0x00, 0x00, 0x08,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x01,
                0x07, 0x01, 0x01
            }));

        var image = Jpeg2000ImageModel.FromSizeSegment(siz);

        Assert.Equal(32u, image.Width);
        Assert.Equal(16u, image.Height);
        Assert.Equal(4, image.Tiles.Count);
        Assert.Equal(2, image.TilesWide);
        Assert.Equal(2, image.TilesHigh);
        Assert.Equal(16u, image.Tiles[3].X0);
        Assert.Equal(8u, image.Tiles[3].Y0);
        Assert.Equal(32u, image.Tiles[3].X1);
        Assert.Equal(16u, image.Tiles[3].Y1);
        Assert.Single(image.Components);
    }

    [Fact]
    public void Precinct_codeblock_and_packet_models_capture_indexing_bounds()
    {
        var precinct = new Jpeg2000PrecinctModel(
            resolutionLevel: 1,
            componentIndex: 2,
            precinctX: 3,
            precinctY: 4,
            x0: 32,
            y0: 64,
            x1: 96,
            y1: 128);
        var codeBlock = new Jpeg2000CodeBlockModel(
            componentIndex: 2,
            resolutionLevel: 1,
            precinctIndex: 0,
            codeBlockX: 1,
            codeBlockY: 2,
            x0: 40,
            y0: 72,
            x1: 56,
            y1: 88);
        var packet = new Jpeg2000PacketModel(
            layerIndex: 5,
            resolutionLevel: 1,
            componentIndex: 2,
            precinctIndex: 0);

        Assert.Equal(64u, precinct.Width);
        Assert.Equal(64u, precinct.Height);
        Assert.Equal(16u, codeBlock.Width);
        Assert.Equal(16u, codeBlock.Height);
        Assert.Equal(5, packet.LayerIndex);
        Assert.Equal(1, packet.ResolutionLevel);
        Assert.Equal(2, packet.ComponentIndex);
        Assert.Equal(0, packet.PrecinctIndex);
    }

    [Theory]
    [InlineData(Jpeg2000ProgressionOrder.LRCP, "L0-R0-C0-P0,L0-R0-C0-P1,L0-R0-C1-P0,L0-R0-C1-P1,L0-R1-C0-P0,L0-R1-C0-P1,L0-R1-C1-P0,L0-R1-C1-P1,L1-R0-C0-P0,L1-R0-C0-P1,L1-R0-C1-P0,L1-R0-C1-P1,L1-R1-C0-P0,L1-R1-C0-P1,L1-R1-C1-P0,L1-R1-C1-P1")]
    [InlineData(Jpeg2000ProgressionOrder.RLCP, "L0-R0-C0-P0,L0-R0-C0-P1,L0-R0-C1-P0,L0-R0-C1-P1,L1-R0-C0-P0,L1-R0-C0-P1,L1-R0-C1-P0,L1-R0-C1-P1,L0-R1-C0-P0,L0-R1-C0-P1,L0-R1-C1-P0,L0-R1-C1-P1,L1-R1-C0-P0,L1-R1-C0-P1,L1-R1-C1-P0,L1-R1-C1-P1")]
    [InlineData(Jpeg2000ProgressionOrder.RPCL, "L0-R0-C0-P0,L1-R0-C0-P0,L0-R0-C1-P0,L1-R0-C1-P0,L0-R0-C0-P1,L1-R0-C0-P1,L0-R0-C1-P1,L1-R0-C1-P1,L0-R1-C0-P0,L1-R1-C0-P0,L0-R1-C1-P0,L1-R1-C1-P0,L0-R1-C0-P1,L1-R1-C0-P1,L0-R1-C1-P1,L1-R1-C1-P1")]
    [InlineData(Jpeg2000ProgressionOrder.PCRL, "L0-R0-C0-P0,L1-R0-C0-P0,L0-R1-C0-P0,L1-R1-C0-P0,L0-R0-C1-P0,L1-R0-C1-P0,L0-R1-C1-P0,L1-R1-C1-P0,L0-R0-C0-P1,L1-R0-C0-P1,L0-R1-C0-P1,L1-R1-C0-P1,L0-R0-C1-P1,L1-R0-C1-P1,L0-R1-C1-P1,L1-R1-C1-P1")]
    [InlineData(Jpeg2000ProgressionOrder.CPRL, "L0-R0-C0-P0,L1-R0-C0-P0,L0-R1-C0-P0,L1-R1-C0-P0,L0-R0-C0-P1,L1-R0-C0-P1,L0-R1-C0-P1,L1-R1-C0-P1,L0-R0-C1-P0,L1-R0-C1-P0,L0-R1-C1-P0,L1-R1-C1-P0,L0-R0-C1-P1,L1-R0-C1-P1,L0-R1-C1-P1,L1-R1-C1-P1")]
    public void Progression_order_iterator_enumerates_packets_in_expected_order(Jpeg2000ProgressionOrder order, string expected)
    {
        var packets = Jpeg2000ProgressionOrderIterator
            .Enumerate(order, layerCount: 2, resolutionCount: 2, componentCount: 2, precinctCount: 2);

        Assert.Equal(expected, string.Join(",", packets));
    }

    [Fact]
    public void Writer_writes_marker_segments_with_big_endian_lengths()
    {
        var writer = new Jpeg2000CodestreamWriter();

        writer.WriteStandalone(Jpeg2000Marker.SOC);
        writer.WriteSegment(Jpeg2000Marker.COD, new byte[] { 0x01, 0x02 });
        writer.WriteStandalone(Jpeg2000Marker.EOC);

        Assert.Equal(new byte[]
        {
            0xFF, Jpeg2000Marker.SOC,
            0xFF, Jpeg2000Marker.COD, 0x00, 0x04, 0x01, 0x02,
            0xFF, Jpeg2000Marker.EOC
        }, writer.ToArray());
    }

    [Fact]
    public void Htj2k_encoder_writes_openjph_compatible_jph_header_shape()
    {
        var pixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 8, columns: 8));
        var frame = pixelData.GetFrame(0).Data;
        var codec = new Jpeg2000HtFrameCodec();

        var codestream = codec.EncodeFrame(pixelData, frame, lossy: false, qualityTolerance: 0, Jpeg2000ProgressionOrder.RPCL);

        var reader = new Jpeg2000CodestreamReader(codestream);
        Assert.Equal(Jpeg2000Marker.SOC, reader.ReadNext().Code);
        var siz = Jpeg2000SizeSegment.Parse(reader.ReadNext());
        var cap = reader.ReadNext();
        var cod = Jpeg2000CodingStyleDefault.Parse(reader.ReadNext());
        var qcd = Jpeg2000QuantizationDefault.Parse(reader.ReadNext());
        var tlm = reader.ReadNext();
        var sot = Jpeg2000StartOfTilePart.Parse(reader.ReadNext(), tileCount: 1);
        Assert.Equal(Jpeg2000Marker.SOD, reader.ReadNext().Code);
        var tileData = reader.ReadTileData(sot);

        Assert.Equal(0x4000, siz.Capabilities);
        Assert.Equal(Jpeg2000Marker.CAP, cap.Code);
        Assert.Equal(new byte[] { 0x00, 0x02, 0x00, 0x00 }, cap.Payload[..4]);
        Assert.Equal(3, cod.DecompositionLevels);
        Assert.Equal(64, cod.CodeBlockWidth);
        Assert.Equal(64, cod.CodeBlockHeight);
        Assert.Equal(0x40, cod.CodeBlockStyle);
        Assert.Equal(1, cod.Transformation);
        Assert.Equal(Jpeg2000QuantizationStyle.None, qcd.Style);
        Assert.Equal(10, qcd.StepSizes.Count);
        Assert.Equal(1, qcd.GuardBits);
        Assert.Equal(EncodeCcapMagnitudeBound(MaxMagnitudeBound(qcd)), ReadUInt16(cap.Payload, 4));
        Assert.Equal(Jpeg2000Marker.TLM, tlm.Code);
        Assert.Equal(26, tlm.Payload.Length);
        Assert.Equal(0x60, tlm.Payload[1]);
        Assert.Equal(0, sot.TilePartIndex);
        Assert.Equal(cod.DecompositionLevels + 1, sot.TilePartCount);
        Assert.Equal(11, (qcd.StepSizes[0] >> 3) + qcd.GuardBits);
        Assert.Equal(12, (qcd.StepSizes[1] >> 3) + qcd.GuardBits);
        Assert.Equal(12, (qcd.StepSizes[3] >> 3) + qcd.GuardBits);
        Assert.NotEqual(new byte[] { (byte)'P', (byte)'H', (byte)'T', (byte)'J' }, tileData[..4]);
    }

    [Fact]
    public void Htj2k_capability_marker_uses_openjph_magb_from_quantization()
    {
        var pixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome16(rows: 459, columns: 888));
        var frame = pixelData.GetFrame(0).Data;
        var codec = new Jpeg2000HtFrameCodec();

        var codestream = codec.EncodeFrame(pixelData, frame, lossy: false, qualityTolerance: 0, Jpeg2000ProgressionOrder.LRCP);

        var reader = new Jpeg2000CodestreamReader(codestream);
        Assert.Equal(Jpeg2000Marker.SOC, reader.ReadNext().Code);
        var siz = Jpeg2000SizeSegment.Parse(reader.ReadNext());
        var cap = reader.ReadNext();
        var cod = Jpeg2000CodingStyleDefault.Parse(reader.ReadNext());
        var qcd = Jpeg2000QuantizationDefault.Parse(reader.ReadNext());

        Assert.Equal(0x4000, siz.Capabilities);
        Assert.Equal(5, cod.DecompositionLevels);
        Assert.Equal(EncodeCcapMagnitudeBound(MaxMagnitudeBound(qcd)), ReadUInt16(cap.Payload, 4));
        Assert.Equal(18, MaxMagnitudeBound(qcd));
    }

    private static byte[] CreateSegment(byte marker, params byte[] payload)
    {
        var bytes = new byte[payload.Length + 4];
        bytes[0] = 0xFF;
        bytes[1] = marker;
        bytes[2] = (byte)((payload.Length + 2) >> 8);
        bytes[3] = (byte)(payload.Length + 2);
        System.Buffer.BlockCopy(payload, 0, bytes, 4, payload.Length);
        return bytes;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static int MaxMagnitudeBound(Jpeg2000QuantizationDefault qcd)
    {
        var max = 0;
        foreach (var step in qcd.StepSizes)
        {
            max = Math.Max(max, (step >> 3) + qcd.GuardBits - 1);
        }

        return max;
    }

    private static int EncodeCcapMagnitudeBound(int magnitudeBound)
    {
        return magnitudeBound <= 8
            ? 0
            : magnitudeBound < 28
                ? magnitudeBound - 8
                : 13 + (magnitudeBound >> 2);
    }
}
