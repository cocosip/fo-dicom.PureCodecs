using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegProcess4Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess4Codec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegDctCodecRoundTripTests
{
    [Fact]
    public void Process1_round_trip_preserves_8_bit_monochrome_with_tolerance()
    {
        AssertRoundTrip(new DicomJpegProcess1Codec(), DicomTransferSyntax.JPEGProcess1);
    }

    [Fact]
    public void Process2_4_round_trip_preserves_8_bit_monochrome_with_tolerance()
    {
        AssertRoundTrip(new DicomJpegProcess2_4Codec(), DicomTransferSyntax.JPEGProcess2_4);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sequential_dct_round_trip_preserves_8_bit_palette_color_with_tolerance(bool baseline)
    {
        IDicomCodec codec = baseline ? new DicomJpegProcess1Codec() : new DicomJpegProcess2_4Codec();
        var syntax = baseline ? DicomTransferSyntax.JPEGProcess1 : DicomTransferSyntax.JPEGProcess2_4;

        AssertRoundTrip(codec, syntax, DicomPixelDataFixtures.CreatePaletteColor8(rows: 16, columns: 16));
    }

    [Fact]
    public void Process1_rejects_16_bit_source()
    {
        var codec = new DicomJpegProcess1Codec();
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome16());
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess1);

        var exception = Assert.Throws<DicomCodecException>(
            () => codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters()));

        Assert.Equal("Unable to create JPEG Process 1 codec for bits stored == 16", exception.Message);
    }

    [Fact]
    public void Process2_4_round_trip_preserves_12_bit_monochrome_with_native_decoder_interoperability()
    {
        var codec = new DicomJpegProcess2_4Codec();
        var rawPixelData = CreateMonochrome12PixelData();
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess2_4);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());
        var nativeCodec = new NativeJpegProcess4Codec();
        nativeCodec.Decode(compressedPixelData, nativeDecodedPixelData, nativeCodec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(rawPixelData, decodedPixelData, tolerance: 320);
        PixelDataAssertions.FramesMatchWithinTolerance(rawPixelData, nativeDecodedPixelData, tolerance: 320);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
        Assert.Equal((ushort)16, decodedPixelData.BitsAllocated);
        Assert.Equal((ushort)12, decodedPixelData.BitsStored);
        Assert.Equal((ushort)11, decodedPixelData.HighBit);
        PixelDataAssertions.AssertRequiredCompressionTags(compressedPixelData.Dataset, DicomTransferSyntax.JPEGProcess2_4);
        Assert.Equal((byte)12, GetSof1Precision(ToArray(compressedPixelData.GetFrame(0))));
    }

    private static void AssertRoundTrip(IDicomCodec codec, DicomTransferSyntax syntax)
    {
        AssertRoundTrip(codec, syntax, DicomPixelDataFixtures.CreateMonochrome8(rows: 16, columns: 16));
    }

    private static void AssertRoundTrip(IDicomCodec codec, DicomTransferSyntax syntax, DicomDataset source)
    {
        var rawPixelData = DicomPixelData.Create(source);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, syntax);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(rawPixelData, decodedPixelData, tolerance: 20);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
        Assert.Equal(rawPixelData.PhotometricInterpretation, compressedPixelData.PhotometricInterpretation);
        Assert.Equal(rawPixelData.PhotometricInterpretation, decodedPixelData.PhotometricInterpretation);
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.SOPClassUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage) },
            { DicomTag.SOPInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, DicomUID.Generate()) },
            { DicomTag.StudyInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, DicomUID.Generate()) },
            { DicomTag.SeriesInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, DicomUID.Generate()) },
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

    private static DicomPixelData CreateMonochrome12PixelData()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)3 },
            { DicomTag.Columns, (ushort)4 },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)12 },
            { DicomTag.HighBit, (ushort)11 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        var pixelData = DicomPixelData.Create(dataset, true);
        const ushort maximum = 4095;
        var samples = new ushort[] { 0, (ushort)(maximum / 16), (ushort)(maximum / 8), (ushort)(maximum / 4), (ushort)(maximum / 2), (ushort)(maximum * 3 / 4), maximum, (ushort)(maximum * 15 / 16), (ushort)(maximum * 11 / 16), (ushort)(maximum * 7 / 16), (ushort)(maximum * 3 / 16), (ushort)(maximum / 32) };
        var frame = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            frame[index * 2] = (byte)samples[index];
            frame[index * 2 + 1] = (byte)(samples[index] >> 8);
        }

        pixelData.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(frame));
        return pixelData;
    }

    private static byte GetSof1Precision(byte[] jpeg)
    {
        for (var index = 0; index + 4 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xff && jpeg[index + 1] == 0xc1)
            {
                return jpeg[index + 4];
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain an SOF1 marker.");
    }

    private static byte[] ToArray(FellowOakDicom.IO.Buffer.IByteBuffer buffer)
    {
        var bytes = new byte[buffer.Size];
        System.Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
