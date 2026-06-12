using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegMarkerTests
{
    [Fact]
    public void Marker_constants_expose_common_jpeg_markers()
    {
        Assert.Equal(0xD8, JpegMarker.SOI);
        Assert.Equal(0xD9, JpegMarker.EOI);
        Assert.Equal(0xC0, JpegMarker.SOF0);
        Assert.Equal(0xC1, JpegMarker.SOF1);
        Assert.Equal(0xC3, JpegMarker.SOF3);
        Assert.Equal(0xC4, JpegMarker.DHT);
        Assert.Equal(0xDB, JpegMarker.DQT);
        Assert.Equal(0xDD, JpegMarker.DRI);
        Assert.Equal(0xDA, JpegMarker.SOS);
    }

    [Fact]
    public void Reader_parses_soi_and_eoi_markers()
    {
        var reader = new JpegMarkerReader(new byte[] { 0xFF, JpegMarker.SOI, 0xFF, JpegMarker.EOI });

        var soi = reader.ReadNext();
        var eoi = reader.ReadNext();

        Assert.Equal(JpegMarker.SOI, soi.Code);
        Assert.False(soi.HasPayload);
        Assert.Equal(JpegMarker.EOI, eoi.Code);
        Assert.False(eoi.HasPayload);
        Assert.True(reader.EndOfData);
    }

    [Fact]
    public void Reader_parses_sof0_payload()
    {
        var reader = new JpegMarkerReader(CreateSegment(
            JpegMarker.SOF0,
            8,
            0x00, 0x10,
            0x00, 0x20,
            1,
            1, 0x11, 0));

        var segment = reader.ReadNext();
        var frame = JpegStartOfFrame.Parse(segment);

        Assert.Equal(JpegMarker.SOF0, segment.Code);
        Assert.Equal(8, frame.SamplePrecision);
        Assert.Equal(16, frame.Height);
        Assert.Equal(32, frame.Width);
        var component = Assert.Single(frame.Components);
        Assert.Equal(1, component.Identifier);
        Assert.Equal(1, component.HorizontalSamplingFactor);
        Assert.Equal(1, component.VerticalSamplingFactor);
        Assert.Equal(0, component.QuantizationTableId);
    }

    [Theory]
    [InlineData(JpegMarker.SOF1)]
    [InlineData(JpegMarker.SOF3)]
    public void Reader_parses_other_start_of_frame_payloads(byte marker)
    {
        var reader = new JpegMarkerReader(CreateSegment(
            marker,
            12,
            0x00, 0x02,
            0x00, 0x03,
            1,
            7, 0x22, 0));

        var frame = JpegStartOfFrame.Parse(reader.ReadNext());

        Assert.Equal(marker, frame.Marker);
        Assert.Equal(12, frame.SamplePrecision);
        Assert.Equal(2, frame.Height);
        Assert.Equal(3, frame.Width);
        Assert.Equal(2, frame.Components[0].HorizontalSamplingFactor);
        Assert.Equal(2, frame.Components[0].VerticalSamplingFactor);
    }

    [Fact]
    public void Reader_parses_table_and_restart_segments()
    {
        var bytes = Combine(
            CreateSegment(JpegMarker.DHT, 0, 1, 2, 3),
            CreateSegment(JpegMarker.DQT, 0, 4, 5, 6),
            CreateSegment(JpegMarker.DRI, 0, 8));
        var reader = new JpegMarkerReader(bytes);

        Assert.Equal(new byte[] { 0, 1, 2, 3 }, reader.ReadNext().Payload);
        Assert.Equal(new byte[] { 0, 4, 5, 6 }, reader.ReadNext().Payload);
        Assert.Equal(new byte[] { 0, 8 }, reader.ReadNext().Payload);
    }

    [Fact]
    public void Reader_parses_sos_payload()
    {
        var reader = new JpegMarkerReader(CreateSegment(
            JpegMarker.SOS,
            1,
            1, 0,
            0,
            63,
            0));

        var scan = JpegStartOfScan.Parse(reader.ReadNext());

        var component = Assert.Single(scan.Components);
        Assert.Equal(1, component.Selector);
        Assert.Equal(0, component.DcTableId);
        Assert.Equal(0, component.AcTableId);
        Assert.Equal(0, scan.SpectralSelectionStart);
        Assert.Equal(63, scan.SpectralSelectionEnd);
        Assert.Equal(0, scan.SuccessiveApproximationHigh);
        Assert.Equal(0, scan.SuccessiveApproximationLow);
    }

    [Fact]
    public void Reader_skips_app_and_comment_segments()
    {
        var reader = new JpegMarkerReader(Combine(
            CreateSegment(JpegMarker.APP0, 1, 2, 3),
            CreateSegment(JpegMarker.COM, 4, 5),
            new byte[] { 0xFF, JpegMarker.EOI }));

        var marker = reader.ReadNextSkippingMetadata();

        Assert.Equal(JpegMarker.EOI, marker.Code);
    }

    [Fact]
    public void Reader_rejects_invalid_marker_length()
    {
        var reader = new JpegMarkerReader(new byte[] { 0xFF, JpegMarker.DQT, 0x00, 0x01 });

        var exception = Assert.Throws<DicomCodecException>(() => reader.ReadNext());

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("length", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writer_writes_soi_eoi_and_segment_lengths()
    {
        var writer = new JpegMarkerWriter();

        writer.WriteStandalone(JpegMarker.SOI);
        writer.WriteSegment(JpegMarker.DRI, new byte[] { 0x00, 0x08 });
        writer.WriteStandalone(JpegMarker.EOI);

        Assert.Equal(new byte[] { 0xFF, JpegMarker.SOI, 0xFF, JpegMarker.DRI, 0x00, 0x04, 0x00, 0x08, 0xFF, JpegMarker.EOI }, writer.ToArray());
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
