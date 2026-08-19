using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.JpegLs.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsInvalidStreamTests
{
    [Fact]
    public void Decode_rejects_stream_missing_soi_with_managed_exception()
    {
        var compressed = CreateCompressedPixelData(new byte[] { 0xFF, 0xD9 });
        var target = CreateRawTarget();
        var codec = new DicomJpegLsLosslessCodec();

        void Decode() => codec.Decode(compressed, target, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Decode);
        Assert.Contains("JPEG-LS", exception.Message);
        Assert.Contains("SOI", exception.Message);
    }

    [Fact]
    public void Decoder_explicitly_rejects_restart_intervals()
    {
        var target = CreateRawTarget();
        var codec = new JpegLsFrameCodec();
        var encoded = codec.EncodeFrame(target, new byte[] { 1, 2, 3, 4 }, nearLossless: 0, JpegLsInterleaveMode.None);
        var withRestartInterval = InsertBeforeMarker(
            encoded,
            JpegLsMarker.SOS,
            new byte[] { 0xFF, 0xDD, 0x00, 0x04, 0x00, 0x01 });

        var exception = Assert.Throws<DicomCodecException>(() => codec.DecodeFrame(target, withRestartInterval));

        Assert.Contains("restart", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decoder_explicitly_rejects_restart_markers()
    {
        var target = CreateRawTarget();
        var codec = new JpegLsFrameCodec();
        var encoded = codec.EncodeFrame(target, new byte[] { 1, 2, 3, 4 }, nearLossless: 0, JpegLsInterleaveMode.None);
        var withRestartMarker = InsertAtEntropyStart(encoded, new byte[] { 0xFF, JpegLsMarker.RST0 });

        var exception = Assert.Throws<DicomCodecException>(() => codec.DecodeFrame(target, withRestartMarker));

        Assert.Contains("restart", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DicomPixelData CreateCompressedPixelData(byte[] frame)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.JPEGLSLossless)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)2 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(frame));
        return DicomPixelData.Create(dataset);
    }

    private static DicomPixelData CreateRawTarget()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)2 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        return DicomPixelData.Create(dataset, true);
    }

    private static byte[] InsertBeforeMarker(byte[] frame, byte marker, byte[] insertion)
    {
        for (var index = 0; index + 1 < frame.Length; index++)
        {
            if (frame[index] == 0xFF && frame[index + 1] == marker)
            {
                return InsertAt(frame, index, insertion);
            }
        }

        throw new Xunit.Sdk.XunitException($"JPEG-LS frame does not contain marker 0x{marker:X2}.");
    }

    private static byte[] InsertAtEntropyStart(byte[] frame, byte[] insertion)
    {
        for (var index = 0; index + 3 < frame.Length; index++)
        {
            if (frame[index] != 0xFF || frame[index + 1] != JpegLsMarker.SOS)
            {
                continue;
            }

            var segmentLength = (frame[index + 2] << 8) | frame[index + 3];
            return InsertAt(frame, index + 2 + segmentLength, insertion);
        }

        throw new Xunit.Sdk.XunitException("JPEG-LS frame does not contain an SOS marker.");
    }

    private static byte[] InsertAt(byte[] source, int offset, byte[] insertion)
    {
        var result = new byte[source.Length + insertion.Length];
        Buffer.BlockCopy(source, 0, result, 0, offset);
        Buffer.BlockCopy(insertion, 0, result, offset, insertion.Length);
        Buffer.BlockCopy(source, offset, result, offset + insertion.Length, source.Length - offset);
        return result;
    }
}
