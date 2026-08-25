using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.JpegLs.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegLsLosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLsLosslessCodec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsRestartCodecTests
{
    private static readonly byte[] SourceFrame =
    {
        3, 17, 91, 204,
        11, 38, 117, 229,
        7, 63, 149, 251,
    };

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Restart_interval_fixture_decodes_exactly_with_fo_dicom_codecs(int intervalByteCount)
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(3, 4, SourceFrame));
        var compressed = CreateCompressedPixelData(source, CreateRestartFrame(intervalByteCount));
        var decoded = CreateRawTarget(source);
        var codec = new NativeJpegLsLosslessCodec();

        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Restart_interval_fixture_decodes_exactly_with_pure_codec(int intervalByteCount)
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(3, 4, SourceFrame));
        var compressed = CreateCompressedPixelData(source, CreateRestartFrame(intervalByteCount));
        var decoded = CreateRawTarget(source);
        var codec = new DicomJpegLsLosslessCodec();

        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Restart_marker_sequence_rolls_over_from_rst7_to_rst0()
    {
        const ushort rows = 10;
        const ushort columns = 8;
        var frame = new byte[rows * columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                frame[row * columns + column] = (byte)(row * 19 + column / 3);
            }
        }

        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows, columns, frame));
        var compressed = CreateCompressedPixelData(source, CreateRestartFrame(rows, columns, frame, new byte[] { 0, 1 }));
        var nativeDecoded = CreateRawTarget(source);
        var pureDecoded = CreateRawTarget(source);

        var nativeCodec = new NativeJpegLsLosslessCodec();
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, nativeDecoded);

        var pureCodec = new DicomJpegLsLosslessCodec();
        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, pureDecoded);
    }

    [Fact]
    public void Restart_interval_spanning_multiple_lines_decodes_exactly_in_both_codecs()
    {
        const ushort rows = 4;
        const ushort columns = 4;
        var frame = new byte[]
        {
            5, 19, 73, 141,
            12, 47, 108, 211,
            9, 66, 155, 243,
            31, 88, 174, 252,
        };
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows, columns, frame));
        var compressed = CreateCompressedPixelData(
            source,
            CreateRestartFrame(rows, columns, frame, new byte[] { 0, 2 }, restartIntervalLines: 2));
        var nativeDecoded = CreateRawTarget(source);
        var pureDecoded = CreateRawTarget(source);

        var nativeCodec = new NativeJpegLsLosslessCodec();
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, nativeDecoded);

        var pureCodec = new DicomJpegLsLosslessCodec();
        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, pureDecoded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Decoder_rejects_invalid_dri_payload_length_with_managed_exception(int payloadLength)
    {
        var driPayload = new byte[payloadLength];
        driPayload[driPayload.Length - 1] = 1;
        var frame = CreateRestartFrame(3, 4, SourceFrame, driPayload);

        var exception = AssertPureDecodeThrows(frame, rows: 3, columns: 4);

        Assert.Contains("DRI", exception.Message);
        Assert.Contains("length", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_missing_restart_marker_with_line_context()
    {
        var frame = CreateRestartFrame(2);
        var markerOffset = FindRestartMarkerOffset(frame, occurrence: 0);
        var corrupted = RemoveBytes(frame, markerOffset, count: 2);

        var exception = AssertPureDecodeThrows(corrupted, rows: 3, columns: 4);

        Assert.Contains("restart marker RST0", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_out_of_sequence_restart_marker_with_line_context()
    {
        var corrupted = CreateRestartFrame(2);
        var markerOffset = FindRestartMarkerOffset(corrupted, occurrence: 0);
        corrupted[markerOffset + 1] = JpegLsMarker.RST0 + 1;

        var exception = AssertPureDecodeThrows(corrupted, rows: 3, columns: 4);

        Assert.Contains("expected restart marker RST0", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_duplicate_restart_marker_before_next_line_boundary()
    {
        var frame = CreateRestartFrame(2);
        var markerOffset = FindRestartMarkerOffset(frame, occurrence: 0);
        var corrupted = InsertBytes(frame, markerOffset + 2, new byte[] { 0xFF, JpegLsMarker.RST0 + 1 });

        var exception = AssertPureDecodeThrows(corrupted, rows: 3, columns: 4);

        Assert.Contains("restart marker", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_restart_marker_without_dri()
    {
        var frame = CreateRestartFrame(2);
        var driOffset = FindMarkerOffset(frame, JpegLsMarker.DRI);
        var driLength = (frame[driOffset + 2] << 8) | frame[driOffset + 3];
        var corrupted = RemoveBytes(frame, driOffset, count: driLength + 2);

        var exception = AssertPureDecodeThrows(corrupted, rows: 3, columns: 4);

        Assert.Contains("restart marker", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_truncated_restart_marker_with_managed_exception()
    {
        var frame = CreateRestartFrame(2);
        var markerOffset = FindRestartMarkerOffset(frame, occurrence: 0);
        var corrupted = new byte[markerOffset + 1];
        Buffer.BlockCopy(frame, 0, corrupted, 0, corrupted.Length);

        var exception = AssertPureDecodeThrows(corrupted, rows: 3, columns: 4);

        Assert.Contains("JPEG-LS", exception.Message);
        Assert.Contains("marker code is missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_rejects_wrong_marker_after_rst7_rollover()
    {
        const ushort rows = 10;
        const ushort columns = 8;
        var frame = new byte[rows * columns];
        for (var index = 0; index < frame.Length; index++)
        {
            frame[index] = (byte)(index * 13 + 7);
        }

        var corrupted = CreateRestartFrame(rows, columns, frame, new byte[] { 0, 1 });
        var rolloverOffset = FindRestartMarkerOffset(corrupted, occurrence: 8);
        corrupted[rolloverOffset + 1] = JpegLsMarker.RST0 + 1;

        var exception = AssertPureDecodeThrows(corrupted, rows, columns);

        Assert.Contains("expected restart marker RST0", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 9", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateRestartFrame(int intervalByteCount)
    {
        var restartInterval = new byte[intervalByteCount];
        restartInterval[restartInterval.Length - 1] = 1;
        return CreateRestartFrame(3, 4, SourceFrame, restartInterval);
    }

    private static byte[] CreateRestartFrame(
        ushort rows,
        ushort columns,
        byte[] sourceFrame,
        byte[] restartInterval,
        int restartIntervalLines = 1)
    {
        var writer = new JpegLsMarkerWriter();
        writer.WriteStandalone(JpegLsMarker.SOI);
        writer.WriteSegment(JpegLsMarker.SOF55, new byte[]
        {
            8,
            (byte)(rows >> 8), (byte)rows,
            (byte)(columns >> 8), (byte)columns,
            1,
            1, 0x11, 0,
        });

        writer.WriteSegment(JpegLsMarker.DRI, restartInterval);
        writer.WriteSegment(JpegLsMarker.SOS, new byte[]
        {
            1,
            1, 0,
            0,
            (byte)JpegLsInterleaveMode.None,
            0,
        });

        var restartMarkerIndex = 0;
        for (var startRow = 0; startRow < rows; startRow += restartIntervalLines)
        {
            var lineCount = Math.Min(restartIntervalLines, rows - startRow);
            var samples = new int[columns * lineCount];
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = sourceFrame[startRow * columns + index];
            }

            writer.WriteRaw(new JpegLsScanCodec(columns, lineCount, 1, 8, 0).Encode(samples));
            if (startRow + lineCount < rows)
            {
                writer.WriteStandalone((byte)(JpegLsMarker.RST0 + restartMarkerIndex));
                restartMarkerIndex = (restartMarkerIndex + 1) & 7;
            }
        }

        writer.WriteStandalone(JpegLsMarker.EOI);
        return writer.ToArray();
    }

    private static DicomCodecException AssertPureDecodeThrows(byte[] frame, ushort rows, ushort columns)
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows, columns, new byte[rows * columns]));
        var compressed = CreateCompressedPixelData(source, frame);
        var decoded = CreateRawTarget(source);
        var codec = new DicomJpegLsLosslessCodec();

        return Assert.Throws<DicomCodecException>(() => codec.Decode(compressed, decoded, codec.GetDefaultParameters()));
    }

    private static int FindRestartMarkerOffset(byte[] frame, int occurrence)
    {
        for (var index = 0; index + 1 < frame.Length; index++)
        {
            if (frame[index] != 0xFF || !JpegLsMarker.IsRestart(frame[index + 1]))
            {
                continue;
            }

            if (occurrence == 0)
            {
                return index;
            }

            occurrence--;
        }

        throw new Xunit.Sdk.XunitException("JPEG-LS restart marker was not found in the fixture.");
    }

    private static int FindMarkerOffset(byte[] frame, byte marker)
    {
        for (var index = 0; index + 1 < frame.Length; index++)
        {
            if (frame[index] == 0xFF && frame[index + 1] == marker)
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException($"JPEG-LS marker 0x{marker:X2} was not found in the fixture.");
    }

    private static byte[] RemoveBytes(byte[] source, int offset, int count)
    {
        var result = new byte[source.Length - count];
        Buffer.BlockCopy(source, 0, result, 0, offset);
        Buffer.BlockCopy(source, offset + count, result, offset, source.Length - offset - count);
        return result;
    }

    private static byte[] InsertBytes(byte[] source, int offset, byte[] insertion)
    {
        var result = new byte[source.Length + insertion.Length];
        Buffer.BlockCopy(source, 0, result, 0, offset);
        Buffer.BlockCopy(insertion, 0, result, offset, insertion.Length);
        Buffer.BlockCopy(source, offset, result, offset + insertion.Length, source.Length - offset);
        return result;
    }

    private static DicomPixelData CreateCompressedPixelData(DicomPixelData source, byte[] frame)
    {
        var compressed = CreatePixelData(source, DicomTransferSyntax.JPEGLSLossless);
        compressed.AddFrame(new MemoryByteBuffer(frame));
        return compressed;
    }

    private static DicomPixelData CreateRawTarget(DicomPixelData source)
    {
        return CreatePixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
    }

    private static DicomPixelData CreatePixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
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
}
