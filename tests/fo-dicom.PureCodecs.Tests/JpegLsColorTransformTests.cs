using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.JpegLs.Internal;
using Xunit;
using NativeJpegLsLosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLsLosslessCodec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsColorTransformTests
{
    private const byte ApplicationData8 = JpegLsMarker.APP0 + 8;

    public static TheoryData<byte, int> SupportedTransformsAndPrecisions => new()
    {
        { 1, 8 },
        { 2, 8 },
        { 3, 8 },
        { 1, 16 },
        { 2, 16 },
        { 3, 16 },
    };

    [Theory]
    [MemberData(nameof(SupportedTransformsAndPrecisions))]
    public void Lossless_decodes_charls_hp_transform_exactly_like_fo_dicom_codecs(byte transform, int bitsPerSample)
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample);
        var frame = CreateColorFrame(expectedSamples, bitsPerSample, transform, new[] { CreateMrfx(transform) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample, componentCount: 3);
        var nativeDecoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample, componentCount: 3);
        var pureDecoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample, componentCount: 3);
        var expectedBytes = SamplesToBytes(expectedSamples, bitsPerSample);

        var nativeCodec = new NativeJpegLsLosslessCodec();
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());
        Assert.Equal(expectedBytes, nativeDecoded.GetFrame(0).Data);

        var pureCodec = new DicomJpegLsLosslessCodec();
        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        Assert.Equal(expectedBytes, pureDecoded.GetFrame(0).Data);
    }

    [Theory]
    [InlineData(new byte[] { (byte)'S', (byte)'P', (byte)'I', (byte)'F', (byte)'F', 0, 1, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 6, 10, 8, 6, 0, 0, 0, 1, 0, 0, 0, 1, 0 })]
    [InlineData(new byte[] { (byte)'m', (byte)'r', (byte)'f', (byte)'x', 1, 0 })]
    [InlineData(new byte[] { (byte)'n', (byte)'o', (byte)'t', (byte)'e', 3 })]
    public void Lossless_ignores_spiff_and_unrelated_application_data8(byte[] applicationData)
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample: 8);
        var frame = CreateColorFrame(expectedSamples, bitsPerSample: 8, transform: 0, new[] { applicationData });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 8, componentCount: 3);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 8, componentCount: 3);

        var codec = new DicomJpegLsLosslessCodec();
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(SamplesToBytes(expectedSamples, bitsPerSample: 8), decoded.GetFrame(0).Data);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(255)]
    public void Lossless_rejects_unsupported_mrfx_transform_with_managed_exception(byte transform)
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample: 8);
        var frame = CreateColorFrame(expectedSamples, bitsPerSample: 8, transform: 0, new[] { CreateMrfx(transform) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 8, componentCount: 3);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 8, componentCount: 3);
        var codec = new DicomJpegLsLosslessCodec();

        void Decode() => codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Decode);
        Assert.Contains("color transform", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lossless_rejects_conflicting_mrfx_declarations()
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample: 8);
        var frame = CreateColorFrame(
            expectedSamples,
            bitsPerSample: 8,
            transform: 1,
            new[] { CreateMrfx(1), CreateMrfx(2) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 8, componentCount: 3);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 8, componentCount: 3);
        var codec = new DicomJpegLsLosslessCodec();

        void Decode() => codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Decode);
        Assert.Contains("conflicting", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lossless_accepts_duplicate_matching_mrfx_declarations()
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample: 8);
        var frame = CreateColorFrame(
            expectedSamples,
            bitsPerSample: 8,
            transform: 2,
            new[] { CreateMrfx(2), CreateMrfx(2) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 8, componentCount: 3);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 8, componentCount: 3);

        var codec = new DicomJpegLsLosslessCodec();
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        Assert.Equal(SamplesToBytes(expectedSamples, bitsPerSample: 8), decoded.GetFrame(0).Data);
    }

    [Fact]
    public void Lossless_rejects_hp_transform_for_non_rgb_frame()
    {
        var samples = new[] { 0, 1, 127, 128, 254, 255 };
        var frame = CreateFrame(samples, bitsPerSample: 8, componentCount: 1, new[] { CreateMrfx(1) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 8, componentCount: 1);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 8, componentCount: 1);
        var codec = new DicomJpegLsLosslessCodec();

        void Decode() => codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Decode);
        Assert.Contains("three components", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lossless_rejects_hp_transform_for_non_charls_precision()
    {
        var expectedSamples = CreateRgbSamples(bitsPerSample: 12);
        var frame = CreateColorFrame(expectedSamples, bitsPerSample: 12, transform: 1, new[] { CreateMrfx(1) });
        var compressed = CreatePixelData(DicomTransferSyntax.JPEGLSLossless, frame, bitsPerSample: 12, componentCount: 3);
        var decoded = CreatePixelData(DicomTransferSyntax.ExplicitVRLittleEndian, null, bitsPerSample: 12, componentCount: 3);
        var codec = new DicomJpegLsLosslessCodec();

        void Decode() => codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        var exception = Assert.Throws<DicomCodecException>((Action)Decode);
        Assert.Contains("8-bit or 16-bit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateColorFrame(int[] rgb, int bitsPerSample, byte transform, IReadOnlyList<byte[]> applicationData)
    {
        return CreateFrame(ForwardTransform(rgb, bitsPerSample, transform), bitsPerSample, componentCount: 3, applicationData);
    }

    private static byte[] CreateFrame(int[] samples, int bitsPerSample, int componentCount, IReadOnlyList<byte[]> applicationData)
    {
        const int height = 1;
        var width = samples.Length / componentCount;
        var writer = new JpegLsMarkerWriter();
        writer.WriteStandalone(JpegLsMarker.SOI);
        foreach (var payload in applicationData)
        {
            writer.WriteSegment(ApplicationData8, payload);
        }

        var startOfFrame = new byte[6 + componentCount * 3];
        startOfFrame[0] = (byte)bitsPerSample;
        startOfFrame[2] = height;
        startOfFrame[4] = (byte)width;
        startOfFrame[5] = (byte)componentCount;
        for (var component = 0; component < componentCount; component++)
        {
            var offset = 6 + component * 3;
            startOfFrame[offset] = (byte)(component + 1);
            startOfFrame[offset + 1] = 0x11;
        }

        writer.WriteSegment(JpegLsMarker.SOF55, startOfFrame);
        var startOfScan = new byte[1 + componentCount * 2 + 3];
        startOfScan[0] = (byte)componentCount;
        for (var component = 0; component < componentCount; component++)
        {
            var offset = 1 + component * 2;
            startOfScan[offset] = (byte)(component + 1);
        }

        startOfScan[startOfScan.Length - 2] = (byte)(componentCount == 1
            ? JpegLsInterleaveMode.None
            : JpegLsInterleaveMode.Sample);
        writer.WriteSegment(JpegLsMarker.SOS, startOfScan);
        writer.WriteRaw(new JpegLsScanCodec(
            width,
            height,
            componentCount,
            bitsPerSample,
            nearLossless: 0,
            componentCount == 1 ? JpegLsInterleaveMode.None : JpegLsInterleaveMode.Sample).Encode(samples));
        writer.WriteStandalone(JpegLsMarker.EOI);
        return writer.ToArray();
    }

    private static DicomPixelData CreatePixelData(
        DicomTransferSyntax transferSyntax,
        byte[]? frame,
        int bitsPerSample,
        int componentCount)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.PhotometricInterpretation, componentCount == 3 ? PhotometricInterpretation.Rgb.Value : PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)1 },
            { DicomTag.Columns, (ushort)6 },
            { DicomTag.BitsAllocated, (ushort)(bitsPerSample <= 8 ? 8 : 16) },
            { DicomTag.BitsStored, (ushort)bitsPerSample },
            { DicomTag.HighBit, (ushort)(bitsPerSample - 1) },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)componentCount },
        };
        if (componentCount == 3)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        }

        var pixelData = DicomPixelData.Create(dataset, true);
        if (frame != null)
        {
            pixelData.AddFrame(new MemoryByteBuffer(frame));
        }

        return DicomPixelData.Create(dataset);
    }

    private static int[] CreateRgbSamples(int bitsPerSample)
    {
        var maximum = (1 << bitsPerSample) - 1;
        var half = 1 << (bitsPerSample - 1);
        return new[]
        {
            0, maximum, 1,
            maximum, 0, maximum - 1,
            10, maximum - 55, 30,
            maximum - 15, 10, 20,
            half, half, half,
            1, 2, 3,
        };
    }

    private static int[] ForwardTransform(int[] rgb, int bitsPerSample, byte transform)
    {
        if (transform == 0)
        {
            return (int[])rgb.Clone();
        }

        var range = bitsPerSample <= 8 ? 256 : 65536;
        var half = range / 2;
        var mask = range - 1;
        var result = new int[rgb.Length];
        for (var index = 0; index < rgb.Length; index += 3)
        {
            var red = rgb[index];
            var green = rgb[index + 1];
            var blue = rgb[index + 2];
            switch (transform)
            {
                case 1:
                    result[index] = (red - green + half) & mask;
                    result[index + 1] = green;
                    result[index + 2] = (blue - green + half) & mask;
                    break;
                case 2:
                    result[index] = (red - green + half) & mask;
                    result[index + 1] = green;
                    result[index + 2] = (blue - ((red + green) >> 1) - half) & mask;
                    break;
                case 3:
                    var v2 = (blue - green + half) & mask;
                    var v3 = (red - green + half) & mask;
                    result[index] = (green + ((v2 + v3) >> 2) - range / 4) & mask;
                    result[index + 1] = v2;
                    result[index + 2] = v3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transform));
            }
        }

        return result;
    }

    private static byte[] SamplesToBytes(int[] samples, int bitsPerSample)
    {
        if (bitsPerSample <= 8)
        {
            return Array.ConvertAll(samples, sample => (byte)sample);
        }

        var bytes = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            bytes[index * 2] = (byte)samples[index];
            bytes[index * 2 + 1] = (byte)(samples[index] >> 8);
        }

        return bytes;
    }

    private static byte[] CreateMrfx(byte transform)
    {
        return new[] { (byte)'m', (byte)'r', (byte)'f', (byte)'x', transform };
    }
}
