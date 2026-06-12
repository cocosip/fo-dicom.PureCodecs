using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.JpegLs;
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
}
