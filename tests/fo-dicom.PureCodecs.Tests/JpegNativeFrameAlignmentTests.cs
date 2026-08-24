using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegLossless14Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLossless14Codec;
using NativeJpegLossless14Sv1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLossless14SV1Codec;
using NativeJpegProcess1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess1Codec;
using NativeJpegProcess4Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess4Codec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegNativeFrameAlignmentTests
{
    [Theory]
    [InlineData("1.2.840.10008.1.2.4.50")]
    [InlineData("1.2.840.10008.1.2.4.51")]
    [InlineData("1.2.840.10008.1.2.4.57")]
    [InlineData("1.2.840.10008.1.2.4.70")]
    public void Rgb_frame_matches_native_output(string transferSyntaxUid)
    {
        var transferSyntax = DicomTransferSyntax.Parse(transferSyntaxUid);
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var pureCompressed = CreateTargetPixelData(source, transferSyntax);
        var nativeCompressed = CreateTargetPixelData(source, transferSyntax);
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codecs = CreateCodecs(transferSyntax);

        codecs.Pure.Encode(source, pureCompressed, codecs.Pure.GetDefaultParameters());
        codecs.Native.Encode(source, nativeCompressed, codecs.Native.GetDefaultParameters());

        AssertCrossDecode(source, pureCompressed, nativeCompressed, pureDecoded, nativeDecoded, codecs, tolerance: 64);
    }

    [Theory]
    [InlineData("1.2.840.10008.1.2.4.51", 12)]
    [InlineData("1.2.840.10008.1.2.4.57", 8)]
    [InlineData("1.2.840.10008.1.2.4.57", 12)]
    [InlineData("1.2.840.10008.1.2.4.57", 16)]
    [InlineData("1.2.840.10008.1.2.4.70", 8)]
    [InlineData("1.2.840.10008.1.2.4.70", 12)]
    [InlineData("1.2.840.10008.1.2.4.70", 16)]
    public void Monochrome_frame_matches_native_output(string transferSyntaxUid, int bitsStored)
    {
        var transferSyntax = DicomTransferSyntax.Parse(transferSyntaxUid);
        var source = CreateMonochromePixelData(bitsStored);
        var pureCompressed = CreateTargetPixelData(source, transferSyntax);
        var nativeCompressed = CreateTargetPixelData(source, transferSyntax);
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codecs = CreateCodecs(transferSyntax);

        codecs.Pure.Encode(source, pureCompressed, codecs.Pure.GetDefaultParameters());
        codecs.Native.Encode(source, nativeCompressed, codecs.Native.GetDefaultParameters());

        var tolerance = transferSyntax == DicomTransferSyntax.JPEGProcess2_4 ? 320 : 0;
        AssertCrossDecode(source, pureCompressed, nativeCompressed, pureDecoded, nativeDecoded, codecs, tolerance);
    }

    [Theory]
    [InlineData("1.2.840.10008.1.2.4.57")]
    [InlineData("1.2.840.10008.1.2.4.70")]
    public void Signed_16_bit_monochrome_frame_matches_native_output(string transferSyntaxUid)
    {
        var transferSyntax = DicomTransferSyntax.Parse(transferSyntaxUid);
        var source = CreateMonochromePixelData(bitsStored: 16, isSigned: true);
        var pureCompressed = CreateTargetPixelData(source, transferSyntax);
        var nativeCompressed = CreateTargetPixelData(source, transferSyntax);
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codecs = CreateCodecs(transferSyntax);

        codecs.Pure.Encode(source, pureCompressed, codecs.Pure.GetDefaultParameters());
        codecs.Native.Encode(source, nativeCompressed, codecs.Native.GetDefaultParameters());

        AssertCrossDecode(source, pureCompressed, nativeCompressed, pureDecoded, nativeDecoded, codecs, tolerance: 0);
    }

    private static (IDicomCodec Pure, IDicomCodec Native) CreateCodecs(DicomTransferSyntax transferSyntax)
    {
        if (transferSyntax == DicomTransferSyntax.JPEGProcess1)
        {
            return (new DicomJpegProcess1Codec(), new NativeJpegProcess1Codec());
        }

        if (transferSyntax == DicomTransferSyntax.JPEGProcess2_4)
        {
            return (new DicomJpegProcess2_4Codec(), new NativeJpegProcess4Codec());
        }

        if (transferSyntax == DicomTransferSyntax.JPEGProcess14)
        {
            return (new DicomJpegLossless14Codec(), new NativeJpegLossless14Codec());
        }

        if (transferSyntax == DicomTransferSyntax.JPEGProcess14SV1)
        {
            return (new DicomJpegLossless14SV1Codec(), new NativeJpegLossless14Sv1Codec());
        }

        throw new ArgumentOutOfRangeException(nameof(transferSyntax));
    }

    private static void AssertCrossDecode(
        DicomPixelData source,
        DicomPixelData pureCompressed,
        DicomPixelData nativeCompressed,
        DicomPixelData pureDecoded,
        DicomPixelData nativeDecoded,
        (IDicomCodec Pure, IDicomCodec Native) codecs,
        int tolerance)
    {
        var nativeParameters = codecs.Native.GetDefaultParameters();
        if (source.SamplesPerPixel == 3
            && nativeParameters is FellowOakDicom.Imaging.NativeCodec.DicomJpegParams nativeJpegParameters)
        {
            nativeJpegParameters.ConvertColorSpaceToRGB = true;
        }

        codecs.Pure.Decode(nativeCompressed, pureDecoded, codecs.Pure.GetDefaultParameters());
        codecs.Native.Decode(pureCompressed, nativeDecoded, nativeParameters);
        PixelDataAssertions.FramesMatchWithinTolerance(source, pureDecoded, tolerance);
        PixelDataAssertions.FramesMatchWithinTolerance(source, nativeDecoded, tolerance);
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
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
            { DicomTag.PlanarConfiguration, (ushort)source.PlanarConfiguration },
        };

        return DicomPixelData.Create(dataset, true);
    }

    private static DicomPixelData CreateMonochromePixelData(int bitsStored, bool isSigned = false)
    {
        var bitsAllocated = bitsStored == 8 ? 8 : 16;
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)16 },
            { DicomTag.Columns, (ushort)16 },
            { DicomTag.BitsAllocated, (ushort)bitsAllocated },
            { DicomTag.BitsStored, (ushort)bitsStored },
            { DicomTag.HighBit, (ushort)(bitsStored - 1) },
            { DicomTag.PixelRepresentation, (ushort)(isSigned ? 1 : 0) },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };
        var frame = new byte[256 * (bitsAllocated / 8)];
        var maximum = (1 << bitsStored) - 1;
        var step = Math.Max(1, maximum / 31);
        for (var index = 0; index < 256; index++)
        {
            var value = isSigned
                ? (((index * step) - 32768) & maximum)
                : (index * step) & maximum;
            frame[index * (bitsAllocated / 8)] = (byte)value;
            if (bitsAllocated == 16)
            {
                frame[index * 2 + 1] = (byte)(value >> 8);
            }
        }

        var pixelData = DicomPixelData.Create(dataset, true);
        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(frame));
        return pixelData;
    }
}
