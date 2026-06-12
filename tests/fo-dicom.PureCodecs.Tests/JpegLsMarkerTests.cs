using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.JpegLs.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsMarkerTests
{
    [Fact]
    public void Marker_constants_expose_common_jpeg_ls_markers()
    {
        Assert.Equal(0xF7, JpegLsMarker.SOF55);
        Assert.Equal(0xD8, JpegLsMarker.SOI);
        Assert.Equal(0xD9, JpegLsMarker.EOI);
        Assert.Equal(0xDA, JpegLsMarker.SOS);
        Assert.Equal(0xF8, JpegLsMarker.LSE);
        Assert.Equal(0xE0, JpegLsMarker.APP0);
        Assert.Equal(0xEF, JpegLsMarker.APP15);
        Assert.Equal(0xFE, JpegLsMarker.COM);
    }

    [Fact]
    public void Reader_parses_soi_and_eoi_markers()
    {
        var reader = new JpegLsMarkerReader(new byte[] { 0xFF, JpegLsMarker.SOI, 0xFF, JpegLsMarker.EOI });

        var soi = reader.ReadNext();
        var eoi = reader.ReadNext();

        Assert.Equal(JpegLsMarker.SOI, soi.Code);
        Assert.False(soi.HasPayload);
        Assert.Equal(JpegLsMarker.EOI, eoi.Code);
        Assert.False(eoi.HasPayload);
        Assert.True(reader.EndOfData);
    }

    [Fact]
    public void Reader_parses_sof55_payload()
    {
        var reader = new JpegLsMarkerReader(CreateSegment(
            JpegLsMarker.SOF55,
            8,
            0x00, 0x10,
            0x00, 0x20,
            1,
            1, 0x11, 0));

        var frame = JpegLsFrameInfo.Parse(reader.ReadNext());

        Assert.Equal(8, frame.BitsPerSample);
        Assert.Equal(16, frame.Height);
        Assert.Equal(32, frame.Width);
        var component = Assert.Single(frame.Components);
        Assert.Equal(1, component.Identifier);
        Assert.Equal(1, component.HorizontalSamplingFactor);
        Assert.Equal(1, component.VerticalSamplingFactor);
        Assert.Equal(0, component.MappingTableSelector);
    }

    [Fact]
    public void Reader_parses_sos_payload()
    {
        var reader = new JpegLsMarkerReader(CreateSegment(
            JpegLsMarker.SOS,
            1,
            1, 0,
            0,
            0,
            0));

        var scan = JpegLsStartOfScan.Parse(reader.ReadNext());

        var component = Assert.Single(scan.Components);
        Assert.Equal(1, component.Selector);
        Assert.Equal(0, component.MappingTableSelector);
        Assert.Equal(0, scan.NearLossless);
        Assert.Equal(JpegLsInterleaveMode.None, scan.InterleaveMode);
        Assert.Equal(0, scan.PointTransform);
    }

    [Fact]
    public void Reader_parses_lse_preset_coding_parameters()
    {
        var reader = new JpegLsMarkerReader(CreateSegment(
            JpegLsMarker.LSE,
            1,
            0x00, 0xFF,
            0x00, 0x11,
            0x00, 0x12,
            0x00, 0x13,
            0x00, 0x40));

        var preset = JpegLsPresetCodingParameters.Parse(reader.ReadNext());

        Assert.Equal(255, preset.MaximumSampleValue);
        Assert.Equal(17, preset.Threshold1);
        Assert.Equal(18, preset.Threshold2);
        Assert.Equal(19, preset.Threshold3);
        Assert.Equal(64, preset.Reset);
    }

    [Fact]
    public void Reader_skips_app_and_comment_segments()
    {
        var reader = new JpegLsMarkerReader(Combine(
            CreateSegment(JpegLsMarker.APP0, 1, 2, 3),
            CreateSegment(JpegLsMarker.COM, 4, 5),
            new byte[] { 0xFF, JpegLsMarker.EOI }));

        var marker = reader.ReadNextSkippingMetadata();

        Assert.Equal(JpegLsMarker.EOI, marker.Code);
    }

    [Fact]
    public void Reader_rejects_invalid_marker_length()
    {
        var reader = new JpegLsMarkerReader(new byte[] { 0xFF, JpegLsMarker.LSE, 0x00, 0x01 });

        var exception = Assert.Throws<DicomCodecException>(() => reader.ReadNext());

        Assert.Contains("JPEG-LS", exception.Message);
        Assert.Contains("length", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writer_writes_soi_eoi_and_segment_lengths()
    {
        var writer = new JpegLsMarkerWriter();

        writer.WriteStandalone(JpegLsMarker.SOI);
        writer.WriteSegment(JpegLsMarker.LSE, new byte[] { 1, 0, 255, 0, 3, 0, 7, 0, 21, 0, 64 });
        writer.WriteStandalone(JpegLsMarker.EOI);

        Assert.Equal(
            new byte[] { 0xFF, JpegLsMarker.SOI, 0xFF, JpegLsMarker.LSE, 0x00, 0x0D, 1, 0, 255, 0, 3, 0, 7, 0, 21, 0, 64, 0xFF, JpegLsMarker.EOI },
            writer.ToArray());
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

    private static byte[] Combine(params byte[][] arrays)
    {
        var length = 0;
        foreach (var array in arrays)
        {
            length += array.Length;
        }

        var combined = new byte[length];
        var offset = 0;
        foreach (var array in arrays)
        {
            System.Buffer.BlockCopy(array, 0, combined, offset, array.Length);
            offset += array.Length;
        }

        return combined;
    }
}
