using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegLossless14Sv1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLossless14SV1Codec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLosslessCodecRoundTripTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Process14_round_trip_preserves_monochrome_samples(int bitsAllocated)
    {
        AssertRoundTrip(new DicomJpegLossless14Codec(), DicomTransferSyntax.JPEGProcess14, bitsAllocated);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Process14_sv1_round_trip_preserves_monochrome_samples(int bitsAllocated)
    {
        AssertRoundTrip(new DicomJpegLossless14SV1Codec(), DicomTransferSyntax.JPEGProcess14SV1, bitsAllocated);
    }

    [Fact]
    public void Process14_round_trip_preserves_rgb_interleaved_samples()
    {
        var codec = new DicomJpegLossless14Codec();
        var rawPixelData = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved());
        var compressedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.JPEGProcess14);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
    }

    [Fact]
    public void Process14_sv1_rgb_encoding_matches_native_optimized_huffman_table()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 16, columns: 16));
        var pureCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess14SV1);
        var nativeCompressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess14SV1);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var pureCodec = new DicomJpegLossless14SV1Codec();
        var nativeCodec = new NativeJpegLossless14Sv1Codec();

        pureCodec.Encode(source, pureCompressed, pureCodec.GetDefaultParameters());
        nativeCodec.Encode(source, nativeCompressed, nativeCodec.GetDefaultParameters());

        var nativeDht = GetDhtPayload(nativeCompressed.GetFrame(0).Data);
        var pureDht = GetDhtPayload(pureCompressed.GetFrame(0).Data);
        Assert.True(
            nativeDht.SequenceEqual(pureDht),
            $"Native DHT: {Convert.ToHexString(nativeDht)}{Environment.NewLine}Pure DHT: {Convert.ToHexString(pureDht)}");
        Assert.Equal(nativeCompressed.GetFrame(0).Data, pureCompressed.GetFrame(0).Data);

        nativeCodec.Decode(pureCompressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        PixelDataAssertions.FramesMatchExactly(source, nativeDecoded);
    }

    private static void AssertRoundTrip(IDicomCodec codec, DicomTransferSyntax syntax, int bitsAllocated)
    {
        var dataset = bitsAllocated switch
        {
            8 => DicomPixelDataFixtures.CreateMonochrome8(frame: new byte[] { 10, 12, 18, 21, 30, 31, 32, 40, 41, 55, 60, 63 }),
            12 => CreateMonochrome12(CreateUInt16Frame(100, 110, 95, 130, 4095, 4080, 3000, 2800, 2048, 2000, 1900, 1800)),
            _ => DicomPixelDataFixtures.CreateMonochrome16(frame: CreateUInt16Frame(1000, 1010, 65000, 65010, 32000, 32001, 1, 0, 40000, 41000, 42000, 43000)),
        };

        var rawPixelData = DicomPixelData.Create(dataset);
        var compressedPixelData = CreateTargetPixelData(rawPixelData, syntax);
        var decodedPixelData = CreateTargetPixelData(rawPixelData, DicomTransferSyntax.ExplicitVRLittleEndian);

        codec.Encode(rawPixelData, compressedPixelData, codec.GetDefaultParameters());
        codec.Decode(compressedPixelData, decodedPixelData, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(rawPixelData, decodedPixelData);
        PixelDataAssertions.AssertFrameCount(rawPixelData, compressedPixelData);
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

        if (source.NumberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, source.NumberOfFrames.ToString());
        }

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)source.PlanarConfiguration);
        }

        return DicomPixelData.Create(dataset, true);
    }

    private static byte[] GetDhtPayload(byte[] jpeg)
    {
        for (var index = 0; index + 3 < jpeg.Length; index++)
        {
            if (jpeg[index] != 0xff || jpeg[index + 1] != JpegMarker.DHT)
            {
                continue;
            }

            var length = (jpeg[index + 2] << 8) | jpeg[index + 3];
            var payload = new byte[length - 2];
            Buffer.BlockCopy(jpeg, index + 4, payload, 0, payload.Length);
            return payload;
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain a DHT marker.");
    }

    private static byte[] CreateUInt16Frame(params int[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            bytes[index * 2] = (byte)samples[index];
            bytes[index * 2 + 1] = (byte)(samples[index] >> 8);
        }

        return bytes;
    }

    private static DicomDataset CreateMonochrome12(byte[] frame)
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

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(frame));
        return dataset;
    }
}
