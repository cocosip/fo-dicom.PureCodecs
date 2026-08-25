using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
using NativeJpegProcess1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess1Codec;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegSequentialDctCodecTests
{
    [Fact]
    public void Baseline_sequential_round_trip_preserves_8_bit_samples_with_tolerance()
    {
        var samples = CreateGradient(width: 16, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var encoded = codec.Encode(samples, width: 16, height: 16, quality: 95);
        var decoded = codec.Decode(encoded, expectedWidth: 16, expectedHeight: 16);

        AssertWithinTolerance(samples, decoded, tolerance: 20);
    }

    [Fact]
    public void Extended_sequential_round_trip_preserves_8_bit_samples_with_tolerance()
    {
        var samples = CreateGradient(width: 8, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Extended);

        var encoded = codec.Encode(samples, width: 8, height: 16, quality: 95);
        var decoded = codec.Decode(encoded, expectedWidth: 8, expectedHeight: 16);

        AssertWithinTolerance(samples, decoded, tolerance: 20);
    }

    [Fact]
    public void Baseline_rgb_encoding_generates_image_specific_compact_huffman_tables()
    {
        var uniform = new byte[16 * 16 * 3];
        var detailed = CreateRgbGradient(width: 16, height: 16);
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var uniformJpeg = codec.Encode(uniform, width: 16, height: 16, componentCount: 3, quality: 90);
        var detailedJpeg = codec.Encode(detailed, width: 16, height: 16, componentCount: 3, quality: 90);

        var uniformDhtSize = GetDhtPayloadSize(uniformJpeg);
        var detailedDhtSize = GetDhtPayloadSize(detailedJpeg);
        Assert.True(uniformDhtSize < 544);
        Assert.True(detailedDhtSize < 544);
        Assert.NotEqual(uniformDhtSize, detailedDhtSize);
    }

    [Fact]
    public void Baseline_decoder_rejects_missing_quantization_table()
    {
        var bytes = new byte[]
        {
            0xFF, JpegMarker.SOI,
            0xFF, JpegMarker.SOF0, 0x00, 0x0B, 8, 0, 8, 0, 8, 1, 1, 0x11, 0,
            0xFF, JpegMarker.SOS, 0x00, 0x08, 1, 1, 0, 0, 63, 0,
            0xFF, JpegMarker.EOI,
        };
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Decode(bytes, expectedWidth: 8, expectedHeight: 8));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("quantization", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_decoder_decodes_restart_intervals_and_resets_dc_predictor()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);
        var encoded = CreateRestartIntervalFrame(JpegMarker.RST0);

        var decoded = codec.Decode(encoded, expectedWidth: 16, expectedHeight: 8);

        Assert.All(decoded, sample => Assert.Equal(decoded[0], sample));
        Assert.True(decoded[0] > 128, "The non-zero DC coefficient should raise both MCU sample blocks above level shift.");
    }

    [Fact]
    public void Baseline_decoder_rejects_out_of_order_restart_marker()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);
        var encoded = CreateRestartIntervalFrame((byte)(JpegMarker.RST0 + 1));

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Decode(encoded, expectedWidth: 16, expectedHeight: 8));

        Assert.Contains("restart", exception.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RST0", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_restart_interval_fixture_decodes_identically_through_public_pure_and_native_codecs()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateMonochrome8(rows: 8, columns: 16));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        compressed.AddFrame(new MemoryByteBuffer(CreateRestartIntervalFrame(JpegMarker.RST0)));
        var pureDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var nativeDecoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var pureCodec = new DicomJpegProcess1Codec();
        var nativeCodec = new NativeJpegProcess1Codec();

        pureCodec.Decode(compressed, pureDecoded, pureCodec.GetDefaultParameters());
        nativeCodec.Decode(compressed, nativeDecoded, nativeCodec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(nativeDecoded, pureDecoded);
    }

    [Fact]
    public void Baseline_three_non_interleaved_scans_with_dht_redefinition_decode_exactly_with_fo_dicom_codecs()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(
            rows: 8,
            columns: 8,
            frame: CreateConstantRgbFrame(8, 8, 128)));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        compressed.AddFrame(new MemoryByteBuffer(CreateThreeScanSequentialFrame()));
        var decoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codec = new NativeJpegProcess1Codec();

        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Baseline_three_non_interleaved_scans_with_dht_redefinition_decode_exactly_with_pure_codec()
    {
        var source = DicomPixelData.Create(DicomPixelDataFixtures.CreateRgbInterleaved(
            rows: 8,
            columns: 8,
            frame: CreateConstantRgbFrame(8, 8, 128)));
        var compressed = CreateTargetPixelData(source, DicomTransferSyntax.JPEGProcess1);
        compressed.AddFrame(new MemoryByteBuffer(CreateThreeScanSequentialFrame()));
        var decoded = CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian);
        var codec = new DicomJpegProcess1Codec();

        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Baseline_multi_scan_missing_final_component_is_rejected()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(() =>
            codec.Decode(CreateThreeScanSequentialFrame(new byte[] { 1, 2 }), 8, 8, 3));

        Assert.Contains("missing scan data for component", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_multi_scan_duplicate_component_is_rejected()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(() =>
            codec.Decode(CreateThreeScanSequentialFrame(new byte[] { 1, 1, 2, 3 }), 8, 8, 3));

        Assert.Contains("duplicate scan coverage", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_multi_scan_unknown_component_is_rejected()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(() =>
            codec.Decode(CreateThreeScanSequentialFrame(new byte[] { 1, 2, 4 }), 8, 8, 3));

        Assert.Contains("unknown component", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_multi_scan_eoi_before_final_scan_data_completes_is_rejected()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(() =>
            codec.Decode(CreateThreeScanSequentialFrame(new byte[] { 1, 2, 3 }, truncateFinalScan: true), 8, 8, 3));

        Assert.Contains("entropy data ended unexpectedly", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Baseline_decoder_rejects_missing_restart_marker()
    {
        var codec = new JpegSequentialDctCodec(JpegSequentialProcess.Baseline);
        var encoded = CreateRestartIntervalFrame(JpegMarker.RST0);
        var restartOffset = System.Array.IndexOf(encoded, (byte)0xFF, FindEntropyStart(encoded));
        var withoutRestartMarker = RemoveAt(encoded, restartOffset, count: 2);

        var exception = Assert.Throws<FellowOakDicom.Imaging.Codec.DicomCodecException>(
            () => codec.Decode(withoutRestartMarker, expectedWidth: 16, expectedHeight: 8));

        Assert.Contains("restart marker", exception.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MCU 1", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateGradient(int width, int height)
    {
        var samples = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[y * width + x] = (byte)((x * 13 + y * 9 + 17) % 256);
            }
        }

        return samples;
    }

    private static byte[] CreateRgbGradient(int width, int height)
    {
        var samples = new byte[width * height * 3];
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            samples[pixel * 3] = (byte)((pixel * 17 + 31) % 256);
            samples[pixel * 3 + 1] = (byte)((pixel * 29 + 71) % 256);
            samples[pixel * 3 + 2] = (byte)((pixel * 43 + 113) % 256);
        }

        return samples;
    }

    private static int GetDhtPayloadSize(byte[] jpeg)
    {
        for (var index = 0; index + 3 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xff && jpeg[index + 1] == 0xc4)
            {
                return (jpeg[index + 2] << 8) | jpeg[index + 3];
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain a DHT marker.");
    }

    private static byte[] CreateRestartIntervalFrame(byte restartMarker)
    {
        var quantizationTable = new byte[65];
        for (var index = 1; index < quantizationTable.Length; index++)
        {
            quantizationTable[index] = 1;
        }

        var dcTable = new byte[18];
        dcTable[1] = 1;
        dcTable[17] = 4;

        var acTable = new byte[18];
        acTable[0] = 0x10;
        acTable[1] = 1;
        acTable[17] = 0;

        var writer = new JpegMarkerWriter();
        writer.WriteStandalone(JpegMarker.SOI);
        writer.WriteSegment(JpegMarker.DQT, quantizationTable);
        writer.WriteSegment(JpegMarker.SOF0, new byte[] { 8, 0, 8, 0, 16, 1, 1, 0x11, 0 });
        writer.WriteSegment(JpegMarker.DHT, dcTable);
        writer.WriteSegment(JpegMarker.DHT, acTable);
        writer.WriteSegment(JpegMarker.DRI, new byte[] { 0, 1 });
        writer.WriteSegment(JpegMarker.SOS, new byte[] { 1, 1, 0, 0, 63, 0 });

        // Each MCU encodes DC category 4, magnitude 8, and EOB. Padding fills the byte with ones.
        writer.WriteRaw(new byte[] { 0x43, 0xFF, restartMarker, 0x43 });
        writer.WriteStandalone(JpegMarker.EOI);
        return writer.ToArray();
    }

    private static byte[] CreateThreeScanSequentialFrame()
    {
        return CreateThreeScanSequentialFrame(new byte[] { 1, 2, 3 });
    }

    private static byte[] CreateThreeScanSequentialFrame(byte[] componentSelectors, bool truncateFinalScan = false)
    {
        var quantizationTable = new byte[65];
        for (var index = 1; index < quantizationTable.Length; index++)
        {
            quantizationTable[index] = 1;
        }

        var writer = new JpegMarkerWriter();
        writer.WriteStandalone(JpegMarker.SOI);
        writer.WriteSegment(JpegMarker.DQT, quantizationTable);
        writer.WriteSegment(JpegMarker.SOF0, new byte[]
        {
            8, 0, 8, 0, 8, 3,
            1, 0x11, 0,
            2, 0x11, 0,
            3, 0x11, 0,
        });
        for (var scanIndex = 0; scanIndex < componentSelectors.Length; scanIndex++)
        {
            var codeLength = scanIndex % 3 + 1;
            var dcTable = new byte[18];
            dcTable[codeLength] = 1;
            var acTable = new byte[18];
            acTable[0] = 0x10;
            acTable[codeLength] = 1;
            writer.WriteSegment(JpegMarker.DHT, dcTable);
            writer.WriteSegment(JpegMarker.DHT, acTable);
            writer.WriteSegment(JpegMarker.SOS, new byte[] { 1, componentSelectors[scanIndex], 0, 0, 63, 0 });
            if (!truncateFinalScan || scanIndex != componentSelectors.Length - 1)
            {
                writer.WriteRaw(new byte[] { (byte)(0xFF >> (codeLength * 2)) });
            }
        }

        writer.WriteStandalone(JpegMarker.EOI);
        return writer.ToArray();
    }

    private static byte[] CreateConstantRgbFrame(int width, int height, byte value)
    {
        var frame = new byte[width * height * 3];
        System.Array.Fill(frame, value);
        return frame;
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.PhotometricInterpretation, source.PhotometricInterpretation.Value },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, source.BitsAllocated },
            { DicomTag.BitsStored, source.BitsStored },
            { DicomTag.HighBit, source.HighBit },
            { DicomTag.PixelRepresentation, (ushort)source.PixelRepresentation },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
        };

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        }

        return DicomPixelData.Create(dataset, true);
    }

    private static int FindEntropyStart(byte[] jpeg)
    {
        for (var index = 0; index + 3 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xFF && jpeg[index + 1] == JpegMarker.SOS)
            {
                return index + 2 + ((jpeg[index + 2] << 8) | jpeg[index + 3]);
            }
        }

        throw new Xunit.Sdk.XunitException("JPEG frame does not contain an SOS marker.");
    }

    private static byte[] RemoveAt(byte[] source, int offset, int count)
    {
        var result = new byte[source.Length - count];
        System.Buffer.BlockCopy(source, 0, result, 0, offset);
        System.Buffer.BlockCopy(source, offset + count, result, offset, source.Length - offset - count);
        return result;
    }

    private static void AssertWithinTolerance(byte[] expected, byte[] actual, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var difference = System.Math.Abs(expected[index] - actual[index]);
            Assert.True(difference <= tolerance, $"Sample {index} differed by {difference}, tolerance {tolerance}.");
        }
    }
}
