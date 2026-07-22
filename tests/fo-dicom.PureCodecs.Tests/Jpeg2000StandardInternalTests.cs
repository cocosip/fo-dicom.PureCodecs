using System;
using System.Collections;
using System.IO;
using System.Linq;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000StandardInternalTests
{
    private const int Tier1FractionalBits = 6;

    [Fact]
    public void Local_real_2_lossless_packet_contributions_match_openjpeg()
    {
        var referencePath = RegressionFixturePaths.Jpeg2000Baseline("fo_dicom_codecs_local2_j2k_lossless.dcm");
        var sourceFile = DicomFile.Open(RegressionFixturePaths.LocalReal2, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(sourceFile.Dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(sourceFile.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);

        new DicomJpeg2000LosslessCodec().Encode(source, compressed, new DicomJpeg2000Params { Irreversible = false });

        var reference = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
        var actual = compressed.GetFrame(0).Data;
        var expectedContributions = DescribePacketContributions(reference);
        var actualContributions = DescribePacketContributions(actual);

        Assert.True(
            expectedContributions.SequenceEqual(actualContributions),
            DescribeFirstPacketContributionDifference(reference, actual)
                + Environment.NewLine
                + DescribeManagedTargetBlockPasses(source, reference, actual)
                + Environment.NewLine
                + DescribeNativePassBoundaryDifferences(source, reference));
    }

    [Fact]
    public void Rate_control_header_budget_matches_native_main_header_length()
    {
        var bytes = (int)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EstimateMainHeaderBytesBeforeSot",
            1,
            false);

        Assert.Equal(119, bytes);
    }

    [Fact]
    public void Rate_control_target_rounds_up_like_openjpeg()
    {
        var target = (int)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EstimateTargetTileBytes",
            1280d,
            1281281);

        Assert.Equal(1002, target);
    }

    [Fact]
    public void Mq_encoder_initial_byte_count_preserves_openjpeg_pre_start_offset()
    {
        var encoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardMqEncoder", 1);

        Assert.Equal(-1, Property<int>(encoder, "CurrentLength"));
    }

    [Fact]
    public void Packet_bit_writer_flushes_stuffing_byte_after_ff_header_byte()
    {
        var writer = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000PacketBitWriter");
        for (var bit = 0; bit < 8; bit++)
        {
            Invoke(writer, "WriteBit", 1);
        }

        Invoke(writer, "Align");

        Assert.Equal(new byte[] { 0xff, 0x00 }, ((IEnumerable)Property<object>(writer, "Bytes")).Cast<byte>());
    }

    [Fact]
    public void Tier1_round_trips_small_code_block_coefficients()
    {
        var coefficients = new[]
        {
            24, -12, 0, 5,
            -7, 31, 2, 0,
            0, -3, 18, -22,
            9, 0, -1, 14
        };
        var scaled = coefficients.Select(value => value << Tier1FractionalBits).ToArray();
        var bitCount = CalculateBitCount(scaled) - Tier1FractionalBits;
        var passCount = (bitCount * 3) - 2;

        var encoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var bytes = (byte[])Invoke(encoder, "Encode", scaled, passCount);
        var decoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder", 4, 4, 0, (byte)0);
        var decoded = (int[])Invoke(decoder, "Decode", bytes, passCount, bitCount);

        for (var index = 0; index < coefficients.Length; index++)
        {
            Assert.True(
                Math.Abs(coefficients[index] - decoded[index]) <= (1 << Tier1FractionalBits),
                $"coefficient {index} differed by {Math.Abs(coefficients[index] - decoded[index])}");
        }
    }

    [Fact]
    public void Tier1_input_shift_matches_pre_scaled_coefficients()
    {
        var coefficients = new[]
        {
            24, -12, 0, 5,
            -7, 31, 2, 0,
            0, -3, 18, -22,
            9, 0, -1, 14
        };
        var scaled = coefficients.Select(value => value << Tier1FractionalBits).ToArray();
        var bitCount = CalculateBitCount(scaled) - Tier1FractionalBits;
        var passCount = (bitCount * 3) - 2;

        var preScaledEncoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var expected = (byte[])Invoke(preScaledEncoder, "Encode", scaled, passCount);
        var inputShiftEncoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder",
            4,
            4,
            0,
            (byte)0,
            0,
            1,
            1.0,
            1.0,
            Tier1FractionalBits);

        var actual = (byte[])Invoke(inputShiftEncoder, "Encode", coefficients, passCount);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tier1_encoders_share_immutable_context_lookup_tables()
    {
        var first = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var second = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        foreach (var fieldName in new[] { "_zeroCodingContexts", "_signCodingContexts", "_signPredictions" })
        {
            var field = first.GetType().GetField(fieldName, flags);

            Assert.NotNull(field);
            Assert.Same(field!.GetValue(first), field.GetValue(second));
        }
    }

    [Fact]
    public void Irreversible_wavelet_workspace_path_matches_original_forward_transform()
    {
        var expected = new[] { 1.25f, -2.5f, 3.75f, -4.0f, 5.5f };
        var actual = (float[])expected.Clone();

        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97_1D",
            expected);
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97_1DWithWorkspace",
            actual,
            new float[actual.Length]);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tier1_pass_lengths_truncate_final_code_block_prefix()
    {
        var coefficients = new[]
        {
            24, -12, 0, 5,
            -7, 31, 2, 0,
            0, -3, 18, -22,
            9, 0, -1, 14
        };
        var scaled = coefficients.Select(value => value << Tier1FractionalBits).ToArray();
        var bitCount = CalculateBitCount(scaled) - Tier1FractionalBits;
        var passCount = (bitCount * 3) - 2;
        var truncatedPassCount = passCount - 1;

        var encoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var args = new object[] { scaled, passCount, null!, null! };
        var bytes = (byte[])Invoke(encoder, "Encode", args);
        var passLengths = (int[])args[2];
        var decoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder", 4, 4, 0, (byte)0);
        var truncatedLength = passLengths[truncatedPassCount - 1];
        var truncated = new byte[truncatedLength];
        Buffer.BlockCopy(bytes, 0, truncated, 0, truncated.Length);
        var decoded = (int[])Invoke(decoder, "Decode", truncated, truncatedPassCount, bitCount);

        Assert.Contains(decoded, value => value != 0);
        Assert.All(decoded.Zip(coefficients, (actual, expected) => Math.Abs(actual - expected)), difference => Assert.True(difference <= 1));
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(2, 2, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(2, 3, 2)]
    public void Tier1_round_trips_edge_sized_code_blocks(int width, int height, int orientation)
    {
        var coefficients = Enumerable.Range(0, width * height)
            .Select(i => ((i * 17) % 41) - 20)
            .ToArray();
        var scaled = coefficients.Select(value => value << Tier1FractionalBits).ToArray();
        var bitCount = CalculateBitCount(scaled) - Tier1FractionalBits;
        var passCount = (bitCount * 3) - 2;

        var encoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", width, height, orientation, (byte)0);
        var bytes = (byte[])Invoke(encoder, "Encode", scaled, passCount);
        var decoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder", width, height, orientation, (byte)0);
        var decoded = (int[])Invoke(decoder, "Decode", bytes, passCount, bitCount);

        for (var index = 0; index < coefficients.Length; index++)
        {
            Assert.True(
                Math.Abs(coefficients[index] - decoded[index]) <= (1 << Tier1FractionalBits),
                $"coefficient {index} differed by {Math.Abs(coefficients[index] - decoded[index])}");
        }
    }

    [Fact]
    public void Classic_jpeg2000_default_layer_rates_match_openjpeg_fo_dicom_codecs_order()
    {
        var parameters = new DicomJpeg2000Params();
        var lossless = new DicomJpeg2000LosslessCodec();
        var lossy = new DicomJpeg2000LossyCodec();

        var losslessRates = (double[])Invoke(lossless, "ResolveLayerRates", parameters, 16, 16);
        var lossyRates = (double[])Invoke(lossy, "ResolveLayerRates", parameters, 16, 16);

        Assert.Equal(new[] { 1280d, 640d, 320d, 160d, 80d, 40d, 20d, 0d }, losslessRates);
        Assert.Equal(new[] { 1280d, 640d, 320d, 160d, 80d, 40d, 20d }, lossyRates);
    }

    [Fact]
    public void Classic_jpeg2000_layer_rates_scale_final_rate_by_stored_precision()
    {
        var parameters = new DicomJpeg2000Params { Rate = 20 };
        var lossy = new DicomJpeg2000LossyCodec();

        var rates = (double[])Invoke(lossy, "ResolveLayerRates", parameters, 12, 16);

        Assert.Equal(new[] { 1280d, 640d, 320d, 160d, 80d, 40d, 15d }, rates);
    }

    [Fact]
    public void Classic_lossy_rate_allocation_uses_stored_precision_for_twelve_bit_pixels()
    {
        const ushort rows = 512;
        const ushort columns = 512;
        var frame = new byte[rows * columns * 2];
        uint state = 0x9e3779b9;
        for (var sample = 0; sample < rows * columns; sample++)
        {
            state = (state * 1664525) + 1013904223;
            var value = (ushort)(state & 0x0fff);
            frame[sample * 2] = (byte)value;
            frame[(sample * 2) + 1] = (byte)(value >> 8);
        }

        var dataset = DicomPixelDataFixtures.CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 16,
            bitsStored: 12,
            highBit: 11,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);
        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(frame));

        var source = DicomPixelData.Create(dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossy), true);

        new DicomJpeg2000LossyCodec().Encode(source, compressed, new DicomJpeg2000Params { Irreversible = true, Rate = 16 });

        Assert.InRange(compressed.GetFrame(0).Size, 1, 33_500);
    }

    [Fact]
    public void Standard_encoder_sample_mapping_matches_openjpeg_for_sixteen_bit_signed_input()
    {
        var frame = new byte[] { 0xFF, 0xFF, 0x00, 0x80, 0x00, 0x00, 0xFF, 0x7F };

        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            16,
            16,
            true,
            false);

        Assert.Equal(new[] { -1, -32768, 0, 32767 }, samples);
    }

    [Fact]
    public void Standard_encoder_sample_mapping_masks_unsigned_input_to_bits_stored()
    {
        var frame = new byte[] { 0xFF, 0x0F, 0x00, 0x10, 0x34, 0x12 };

        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            16,
            12,
            false,
            false);

        Assert.Equal(new[] { 0x0FFF, 0x0000, 0x0234 }, samples);
    }

    [Fact]
    public void Packet_encoder_round_trips_single_layer_block_contribution()
    {
        var data = new byte[] { 0x12, 0x34, 0x56 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var blocks = Array.CreateInstance(blockType, 1);
        blocks.SetValue(Create(blockType, 0, 0, 1, 1, 2, 4, data), 0);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { blocks })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 1, 1, 0, 8, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            1,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var codeBlocks = (IEnumerable)Invoke(component, "AllCodeBlocks");
        var block = codeBlocks.Cast<object>().Single();

        Assert.Equal(2, Property<int>(block, "ZeroBitPlanes"));
        Assert.Equal(4, Property<int>(block, "TotalPasses"));
        Assert.Equal(data, Property<byte[]>(block, "Data"));
    }

    [Fact]
    public void Packet_encoder_uses_truncated_block_data_prefix_without_copying()
    {
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var fullBlock = Create(
            blockType,
            0,
            0,
            1,
            1,
            2,
            4,
            data,
            new[] { 1, 2, 3, 4 },
            Array.Empty<byte[]>(),
            Array.Empty<double>(),
            0,
            0,
            0);
        var truncated = Invoke(fullBlock, "TruncateToPasses", 2);
        var copiedPrefix = Create(blockType, 0, 0, 1, 1, 2, 2, new byte[] { 0x12, 0x34 });
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;

        var truncatedPacket = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { ToArray(blockType, truncated) })!;
        var copiedPrefixPacket = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { ToArray(blockType, copiedPrefix) })!;

        Assert.Same(data, Property<byte[]>(truncated, "Data"));
        Assert.Equal(copiedPrefixPacket, truncatedPacket);
    }

    [Fact]
    public void Packet_encoder_round_trips_final_layer_block_contribution()
    {
        var complete = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var layers = Array.CreateInstance(blockType.MakeArrayType(), 2);
        var layer0 = Array.CreateInstance(blockType, 1);
        var layer1 = Array.CreateInstance(blockType, 1);
        layer0.SetValue(Create(blockType, 0, 0, 1, 1, 2, 0, Array.Empty<byte>()), 0);
        layer1.SetValue(Create(blockType, 0, 0, 1, 1, 2, 4, complete), 0);
        layers.SetValue(layer0, 0);
        layers.SetValue(layer1, 1);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeLayeredPackets")!.Invoke(null, new object[] { layers })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 1, 1, 0, 8, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            2,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var codeBlocks = (IEnumerable)Invoke(component, "AllCodeBlocks");
        var block = codeBlocks.Cast<object>().Single();

        Assert.Equal(2, Property<int>(block, "ZeroBitPlanes"));
        Assert.Equal(4, Property<int>(block, "TotalPasses"));
        Assert.Equal(complete, Property<byte[]>(block, "Data"));
    }

    [Fact]
    public void Packet_encoder_writes_openjpeg_present_header_for_layer_without_contribution()
    {
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var layers = Array.CreateInstance(blockType.MakeArrayType(), 1);
        var layer0 = Array.CreateInstance(blockType, 1);
        layer0.SetValue(Create(blockType, 0, 0, 1, 1, 2, 0, Array.Empty<byte>()), 0);
        layers.SetValue(layer0, 0);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;

        var encoded = (byte[])packetEncoder.GetMethod("EncodeLayeredPackets")!.Invoke(null, new object[] { layers })!;

        Assert.Equal(new byte[] { 0x80 }, encoded);
    }

    [Fact]
    public void Packet_encoder_preserves_pass_contribution_when_layer_adds_no_bytes()
    {
        var data = new byte[] { 0x12 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var layers = Array.CreateInstance(blockType.MakeArrayType(), 2);
        var layer0 = Array.CreateInstance(blockType, 1);
        var layer1 = Array.CreateInstance(blockType, 1);
        layer0.SetValue(Create(blockType, 0, 0, 1, 1, 2, 1, data), 0);
        layer1.SetValue(Create(blockType, 0, 0, 1, 1, 2, 2, data), 0);
        layers.SetValue(layer0, 0);
        layers.SetValue(layer1, 1);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeLayeredPackets")!.Invoke(null, new object[] { layers })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 1, 1, 0, 8, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            2,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var block = ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>().Single();

        Assert.Equal(2, Property<int>(block, "ZeroBitPlanes"));
        Assert.Equal(2, Property<int>(block, "TotalPasses"));
        Assert.Equal(data, Property<byte[]>(block, "Data"));
    }

    [Fact]
    public void Packet_encoder_preserves_zero_bit_plane_tag_tree_value()
    {
        var data = new byte[] { 0x12 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var blocks = Array.CreateInstance(blockType, 1);
        blocks.SetValue(Create(blockType, 0, 0, 1, 1, 10, 1, data), 0);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { blocks })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 1, 1, 0, 16, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            1,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var block = ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>().Single();

        Assert.Equal(10, Property<int>(block, "ZeroBitPlanes"));
    }

    [Fact]
    public void Packet_encoder_preserves_zero_bit_plane_values_for_multiple_leaves()
    {
        var data = new byte[] { 0x12 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var blocks = Array.CreateInstance(blockType, 2);
        blocks.SetValue(Create(blockType, 0, 0, 2, 1, 10, 1, data), 0);
        blocks.SetValue(Create(blockType, 1, 0, 2, 1, 10, 1, data), 1);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { blocks })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 128, 64, 0, 16, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            1,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var decoded = ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>().ToArray();

        Assert.Equal(10, Property<int>(decoded[0], "ZeroBitPlanes"));
        Assert.Equal(10, Property<int>(decoded[1], "ZeroBitPlanes"));
    }

    [Fact]
    public void Packet_encoder_round_trips_multiple_block_tag_trees()
    {
        var first = new byte[] { 0x11, 0x22 };
        var second = new byte[] { 0x33, 0x44, 0x55 };
        var blockType = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000EncodedBlock", throwOnError: true)!;
        var blocks = Array.CreateInstance(blockType, 4);
        blocks.SetValue(Create(blockType, 0, 0, 2, 2, 1, 2, first), 0);
        blocks.SetValue(Create(blockType, 1, 0, 2, 2, 0, 0, Array.Empty<byte>()), 1);
        blocks.SetValue(Create(blockType, 0, 1, 2, 2, 3, 1, second), 2);
        blocks.SetValue(Create(blockType, 1, 1, 2, 2, 0, 0, Array.Empty<byte>()), 3);
        var packetEncoder = Jpeg2000Assembly.GetType("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketEncoder", throwOnError: true)!;
        var encoded = (byte[])packetEncoder.GetMethod("EncodeSingleLayerPacket")!.Invoke(null, new object[] { blocks })!;
        var component = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", 0, 0, 0, 128, 128, 0, 8, true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            encoded,
            1,
            1,
            1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);

        Invoke(decoder, "Decode");
        var decoded = ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>().ToArray();

        Assert.Equal(first, Property<byte[]>(decoded[0], "Data"));
        Assert.Empty(Property<byte[]>(decoded[1], "Data"));
        Assert.Equal(second, Property<byte[]>(decoded[2], "Data"));
        Assert.Empty(Property<byte[]>(decoded[3], "Data"));
    }

    [Fact]
    public void Reversible_wavelet_forward_is_inverted_by_standard_inverse()
    {
        var source = Enumerable.Range(0, 30).Select(i => (i * 13) - 91).ToArray();
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            source,
            5,
            6,
            2,
            0,
            0);

        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Inverse53",
            coefficients,
            5,
            6,
            2,
            0,
            0);

        Assert.Equal(source, coefficients);
    }

    [Fact]
    public void Standard_reversible_wavelet_inverse_restores_small_codec_fixture_shape()
    {
        var source = Enumerable.Range(0, 30).Select(i => ((i * 13) + 11) - 128).ToArray();
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            source,
            6,
            5,
            5,
            0,
            0);

        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Inverse53",
            coefficients,
            6,
            5,
            5,
            0,
            0);

        Assert.Equal(source, coefficients);
    }

    [Fact]
    public void Reversible_tile_packets_decode_to_original_wavelet_coefficients()
    {
        var source = Enumerable.Range(0, 30).Select(i => ((i * 13) + 11) - 128).ToArray();
        var expected = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            source,
            6,
            5,
            5,
            0,
            0);
        var tileData = (byte[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EncodeReversibleTile",
            source,
            6,
            5,
            8);
        var actual = DecodeTileCoefficients(tileData, width: 6, height: 5, levels: 5, precision: 8);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Standard_frame_encoder_decodes_8_bit_monochrome_fixture_exactly()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var pixelData = DicomPixelData.Create(dataset);
        var frame = pixelData.GetFrame(0).Data;
        var codec = new Jpeg2000ClassicFrameCodec();

        var encoded = codec.EncodeFrame(
            pixelData,
            frame,
            irreversible: false,
            qualityTolerance: 1,
            Jpeg2000ProgressionOrder.LRCP,
            layerCount: 1,
            usesMultipleComponentTransform: false,
            encodeSignedPixelValuesAsUnsigned: false);
        var decoded = codec.DecodeFrame(pixelData, encoded);

        Assert.Equal(frame, decoded);
    }

    [Fact]
    public void Standard_frame_encoder_default_multi_layer_codestream_writes_non_empty_first_quality_layer()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
        var pixelData = DicomPixelData.Create(dataset);
        var frame = pixelData.GetFrame(0).Data;
        var codec = new DicomJpeg2000LosslessCodec();
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(dataset, DicomTransferSyntax.JPEG2000Lossless), true);

        codec.Encode(pixelData, compressed, codec.GetDefaultParameters());

        var tileData = ExtractTileData(compressed.GetFrame(0).Data);
        Assert.Contains(tileData.Take(6), value => value != 0);
    }

    [Theory]
    [InlineData("fo_dicom_codecs_j2k_lossless.dcm", false)]
    [InlineData("fo_dicom_codecs_j2k_lossy.dcm", true)]
    public void Openjpeg_real_fixture_packet_layers_match_managed_default_layer_shape(string referenceFileName, bool lossy)
    {
        var inputPath = RegressionFixturePaths.LocalReal1;
        var referencePath = RegressionFixturePaths.Jpeg2000Baseline(referenceFileName);

        var referenceFrame = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(file.Dataset);
        var transferSyntax = lossy ? DicomTransferSyntax.JPEG2000Lossy : DicomTransferSyntax.JPEG2000Lossless;
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, transferSyntax), true);
        if (lossy)
        {
            new DicomJpeg2000LossyCodec().Encode(source, compressed, new DicomJpeg2000Params { Irreversible = true });
        }
        else
        {
            new DicomJpeg2000LosslessCodec().Encode(source, compressed, new DicomJpeg2000Params { Irreversible = false });
        }

        var pureFrame = compressed.GetFrame(0).Data;
        var referenceLayers = DescribePacketLayers(referenceFrame);
        var pureLayers = DescribePacketLayers(pureFrame);
        var referenceContributions = DescribePacketContributions(referenceFrame);
        var pureContributions = DescribePacketContributions(pureFrame);

        var reference = string.Join(Environment.NewLine, referenceLayers);
        var pure = string.Join(Environment.NewLine, pureLayers);
        Assert.True(
            reference == pure,
            "reference:" + Environment.NewLine + reference + Environment.NewLine
            + "pure:" + Environment.NewLine + pure + Environment.NewLine
            + DescribeFirstPacketContributionDifference(referenceFrame, pureFrame));
        Assert.Equal(referenceContributions, pureContributions);
    }

    [Fact]
    public void Openjpeg_rgb_lossy_fixture_packet_layers_match_managed_layer_shape()
    {
        var referencePath = RegressionFixturePaths.Jpeg2000Baseline("fo_dicom_codecs_unit8_j2k_lossy.j2k");
        var purePath = RegressionFixturePaths.Jpeg2000Baseline("purecodecs_unit8_j2k_lossy.j2k");

        var referenceFrame = File.ReadAllBytes(referencePath);
        var pureFrame = File.ReadAllBytes(purePath);
        var referenceLayers = DescribePacketLayers(referenceFrame);
        var pureLayers = DescribePacketLayers(pureFrame);

        var reference = string.Join(Environment.NewLine, referenceLayers);
        var pure = string.Join(Environment.NewLine, pureLayers);
        Assert.True(
            reference == pure,
            "reference:" + Environment.NewLine + reference + Environment.NewLine
            + "pure:" + Environment.NewLine + pure + Environment.NewLine
            + DescribeFirstPacketContributionDifference(referenceFrame, pureFrame));
    }

    [Fact]
    public void Standard_irreversible_encoder_preserves_signed_16_bit_fixture_with_full_rate_tolerance()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.UnitFixtures.Single(item => item.Name == "Efferent unit 16-bit sample");
        var file = DicomFile.Open(fixture.Path);
        var source = DicomPixelData.Create(file.Dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.JPEG2000Lossy), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LossyCodec();

        codec.Encode(source, compressed, new DicomJpeg2000Params { Irreversible = true, Rate = 0 });
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchWithinTolerance(source, decoded, tolerance: 64);
    }

    [Fact]
    public void Standard_reversible_encoder_preserves_local_real_fixture_through_default_layers()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(file.Dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new DicomJpeg2000LosslessCodec();

        codec.Encode(source, compressed, codec.GetDefaultParameters());
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Standard_reversible_encoder_preserves_local_real_fixture_with_single_layer()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(file.Dataset);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);
        var decoded = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        var codec = new Jpeg2000ClassicFrameCodec();
        var encoded = codec.EncodeFrame(
            source,
            source.GetFrame(0).Data,
            irreversible: false,
            qualityTolerance: 1,
            Jpeg2000ProgressionOrder.LRCP,
            layerCount: 1,
            usesMultipleComponentTransform: false,
            encodeSignedPixelValuesAsUnsigned: false,
            rate: 0,
            layerRates: new[] { 0d });

        compressed.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(encoded));
        new DicomJpeg2000LosslessCodec().Decode(compressed, decoded, new DicomJpeg2000Params { Irreversible = false, Rate = 0, RateLevels = Array.Empty<int>() });

        PixelDataAssertions.FramesMatchExactly(source, decoded);
    }

    [Fact]
    public void Standard_reversible_layered_tile_packets_decode_to_original_coefficients_for_local_real_fixture()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var frame = pixelData.GetFrame(0).Data;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var expected = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            samples,
            pixelData.Width,
            pixelData.Height,
            5,
            0,
            0);
        var codec = new DicomJpeg2000LosslessCodec();
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);

        codec.Encode(pixelData, compressed, codec.GetDefaultParameters());

        var codestream = compressed.GetFrame(0).Data;
        var actual = DecodeTileCoefficients(ExtractTileData(codestream), pixelData.Width, pixelData.Height, 5, pixelData.BitsStored, ReadCoding(codestream).LayerCount);
        for (var index = 0; index < expected.Length; index++)
        {
            if (expected[index] == actual[index])
            {
                continue;
            }

            var context = DescribeDecodedBlockForCoefficient(codestream, pixelData.Width, pixelData.Height, pixelData.BitsStored, index);
            Assert.Fail($"coefficient {index} expected={expected[index]} actual={actual[index]}{Environment.NewLine}{context}");
        }
    }

    [Fact]
    public void Standard_reversible_layered_tile_packets_preserve_encoder_codeblock_payloads_for_local_real_fixture()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var frame = pixelData.GetFrame(0).Data;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            samples,
            pixelData.Width,
            pixelData.Height,
            5,
            0,
            0);
        var expectedBlocks = ((IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocks",
            coefficients,
            pixelData.Width,
            pixelData.Height,
            4,
            pixelData.BitsStored,
            true,
            null!)).Cast<object>().ToArray();
        var codec = new DicomJpeg2000LosslessCodec();
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(file.Dataset, DicomTransferSyntax.JPEG2000Lossless), true);

        codec.Encode(pixelData, compressed, codec.GetDefaultParameters());

        var actualBlocks = DecodeTileBlocks(compressed.GetFrame(0).Data, pixelData.Width, pixelData.Height, pixelData.BitsStored);
        foreach (var expected in expectedBlocks)
        {
            var actual = actualBlocks.Single(block =>
                Property<int>(block, "Orientation") == Property<int>(expected, "Orientation")
                && Property<int>(block, "LocalX") == Property<int>(expected, "X")
                && Property<int>(block, "LocalY") == Property<int>(expected, "Y")
                && ResolutionForBlock(5, Property<int>(block, "Orientation"), Property<int>(block, "X0"), Property<int>(block, "Y0"), pixelData.Width, pixelData.Height) == Property<int>(expected, "Resolution"));
            Assert.True(
                Property<int>(expected, "ZeroBitPlanes") == Property<int>(actual, "ZeroBitPlanes"),
                $"zero-bit mismatch resolution={Property<int>(expected, "Resolution")} orientation={Property<int>(expected, "Orientation")} x={Property<int>(expected, "X")} y={Property<int>(expected, "Y")} expected={Property<int>(expected, "ZeroBitPlanes")} actual={Property<int>(actual, "ZeroBitPlanes")}");
            Assert.True(
                Property<int>(expected, "PassCount") == Property<int>(actual, "TotalPasses"),
                $"pass mismatch resolution={Property<int>(expected, "Resolution")} orientation={Property<int>(expected, "Orientation")} x={Property<int>(expected, "X")} y={Property<int>(expected, "Y")}");
            Assert.True(
                Property<byte[]>(expected, "Data").SequenceEqual(Property<byte[]>(actual, "Data")),
                $"payload mismatch resolution={Property<int>(expected, "Resolution")} orientation={Property<int>(expected, "Orientation")} x={Property<int>(expected, "X")} y={Property<int>(expected, "Y")}");
        }
    }

    [Fact]
    public void Tier1_reversible_real_fixture_codeblock_restores_small_coefficients_after_openjpeg_postscale()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            pixelData.GetFrame(0).Data,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            samples,
            pixelData.Width,
            pixelData.Height,
            5,
            0,
            0);
        var blocks = ((IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocks",
            coefficients,
            pixelData.Width,
            pixelData.Height,
            4,
            pixelData.BitsStored,
            true,
            null!)).Cast<object>().ToArray();
        var block = blocks.Single(item => Property<int>(item, "Orientation") == 1
            && Property<int>(item, "X") == 0
            && Property<int>(item, "Y") == 0);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder",
            64,
            64,
            1,
            (byte)0);

        var decoded = (int[])Invoke(
            decoder,
            "Decode",
            Property<byte[]>(block, "Data"),
            Property<int>(block, "PassCount"),
            pixelData.BitsStored + GainBits(1) + 1 - Property<int>(block, "ZeroBitPlanes"));

        Assert.Equal(coefficients[286], decoded[0]);
    }

    [Fact]
    public void Irreversible_real_fixture_codeblock_payload_prefix_matches_openjpeg_after_float32_dwt()
    {
        var inputPath = RegressionFixturePaths.LocalReal1;

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            pixelData.GetFrame(0).Data,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var values = samples.Select(sample => (double)sample).ToArray();
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97",
            values,
            pixelData.Width,
            pixelData.Height,
            5);
        var steps = (double[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "CreateIrreversibleSteps",
            5,
            80);
        var encodedSteps = steps
            .Select(step => (ushort)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "EncodeStepSize",
                step,
                pixelData.BitsStored))
            .ToArray();
        var quantized = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversibleOpenJpeg",
            values,
            pixelData.Width,
            pixelData.Height,
            encodedSteps,
            pixelData.BitsStored);
        var blocks = ((IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocks",
            quantized,
            pixelData.Width,
            pixelData.Height,
            3,
            pixelData.BitsStored,
            false,
            encodedSteps)).Cast<object>().ToArray();
        var block = blocks.Single(item => Property<int>(item, "Orientation") == 2
            && Property<int>(item, "X") == 0
            && Property<int>(item, "Y") == 0);
        var passLengths = Property<int[]>(block, "PassLengths");
        var blockData = Property<byte[]>(block, "Data");
        var referencePath = RegressionFixturePaths.Jpeg2000Baseline("fo_dicom_codecs_j2k_lossy.dcm");

        var referenceFrame = DicomPixelData.Create(DicomFile.Open(referencePath, FileReadOption.ReadAll).Dataset).GetFrame(0).Data;
        var referenceBlockData = ExtractDecodedBlockData(referenceFrame, resolution: 3, orientation: 2, x: 0, y: 0);

        Assert.Equal(new[] { 67, 151, 262, 429, 715, 1071 }, new[] { passLengths[4], passLengths[7], passLengths[10], passLengths[13], passLengths[17], passLengths[21] });
        Assert.True(
            blockData.Take(referenceBlockData.Length).SequenceEqual(referenceBlockData),
            DescribeByteDifference("referenceBlock", referenceBlockData, "managedBlock", blockData));
    }

    [Fact]
    public void Irreversible_quantization_preserves_openjpeg_tier1_fractional_bits()
    {
        var coefficients = new[] { 0.0, 0.49, -0.49, 1.51 };
        var quantized = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversible",
            coefficients,
            2,
            2,
            Enumerable.Repeat(1.0, 16).ToArray());

        Assert.Equal(new[] { 0, 31, -31, 97 }, quantized);
    }

    [Fact]
    public void Irreversible_encoder_band_steps_apply_openjpeg_subband_gain()
    {
        var encodedStep = (ushort)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "EncodeStepSize",
            1.0,
            16);
        var encodedSteps = Enumerable.Repeat(encodedStep, 16).ToArray();
        var ll = (double)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ResolveEncodedStepSize",
            encodedSteps,
            16,
            0,
            0);
        var hl = (double)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ResolveEncodedStepSize",
            encodedSteps,
            16,
            1,
            1);
        var hh = (double)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ResolveEncodedStepSize",
            encodedSteps,
            16,
            3,
            3);

        Assert.Equal(1.0, ll, tolerance: 0.0);
        Assert.Equal(2.0, hl, tolerance: 0.0);
        Assert.Equal(4.0, hh, tolerance: 0.0);
    }

    [Fact]
    public void Irreversible_managed_decoder_dequantizes_preserved_fractional_tier1_output()
    {
        var encodedStep = (ushort)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "EncodeStepSize",
            1.0,
            16);
        var restored = DequantizeLikeManagedDecoder(
            new[] { 64, -64, 128, -128 },
            width: 2,
            height: 2,
            precision: 16,
            Enumerable.Repeat(encodedStep, 16).ToArray());

        Assert.Equal(new[] { 1.0, -1.0, 2.0, -2.0 }, restored);
    }

    [Fact]
    public void Irreversible_legacy_quantization_loop_is_not_openjpeg_decoder_contract()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.UnitFixtures.Single(item => item.Name == "Efferent unit 16-bit sample");
        var file = DicomFile.Open(fixture.Path);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var frame = pixelData.GetFrame(0).Data;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var values = samples.Select(sample => (double)sample).ToArray();
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97",
            values,
            pixelData.Width,
            pixelData.Height,
            5);
        var steps = (double[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "CreateIrreversibleSteps",
            5,
            80);
        var encodedSteps = steps
            .Select(step => (ushort)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "EncodeStepSize",
                step,
                pixelData.BitsStored))
            .ToArray();
        var quantized = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversible",
            values,
            pixelData.Width,
            pixelData.Height,
            steps);
        var restored = DequantizeLikeManagedDecoder(
            quantized,
            pixelData.Width,
            pixelData.Height,
            pixelData.BitsStored,
            encodedSteps);
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Inverse97",
            restored,
            pixelData.Width,
            pixelData.Height,
            5);

        Assert.True(Math.Abs(samples[0] - Round(restored[0])) > 64, "legacy quantization helper unexpectedly matched the OpenJPEG decoder path");
    }

    [Fact]
    public void Irreversible_openjpeg_quantization_loop_preserves_fixture_without_tier1()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.UnitFixtures.Single(item => item.Name == "Efferent unit 16-bit sample");
        var file = DicomFile.Open(fixture.Path);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var frame = pixelData.GetFrame(0).Data;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var values = samples.Select(sample => (double)sample).ToArray();
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97",
            values,
            pixelData.Width,
            pixelData.Height,
            5);
        var steps = (double[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "CreateIrreversibleSteps",
            5,
            80);
        var encodedSteps = steps
            .Select(step => (ushort)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "EncodeStepSize",
                step,
                pixelData.BitsStored))
            .ToArray();
        var quantized = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversibleOpenJpeg",
            values,
            pixelData.Width,
            pixelData.Height,
            encodedSteps,
            pixelData.BitsStored);
        var restored = DequantizeLikeManagedDecoder(
            quantized,
            pixelData.Width,
            pixelData.Height,
            pixelData.BitsStored,
            encodedSteps);
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Inverse97",
            restored,
            pixelData.Width,
            pixelData.Height,
            5);

        Assert.True(Math.Abs(samples[0] - Round(restored[0])) <= 64, $"sample 0 differed by {Math.Abs(samples[0] - Round(restored[0]))}");
    }

    [Fact]
    public void Tier1_round_trips_openjpeg_fractional_irreversible_code_block_coefficients()
    {
        var coefficients = new[]
        {
            0, 31, -31, 97,
            -128, 255, -384, 512,
            5, -7, 64, -65,
            1024, -1536, 2048, -4096
        };
        var bitCount = CalculateBitCount(coefficients) - Tier1FractionalBits;
        var passCount = (bitCount * 3) - 2;

        var encoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Encoder", 4, 4, 0, (byte)0);
        var bytes = (byte[])Invoke(encoder, "Encode", coefficients, passCount);
        var decoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder", 4, 4, 0, (byte)0);
        var decoded = (int[])Invoke(decoder, "DecodeScaled", bytes, passCount, bitCount);

        for (var index = 0; index < coefficients.Length; index++)
        {
            Assert.True(
                Math.Abs(coefficients[index] - decoded[index]) <= (1 << Tier1FractionalBits),
                $"coefficient {index} differed by {Math.Abs(coefficients[index] - decoded[index])}");
        }
    }

    [Fact]
    public void Irreversible_tile_packets_decode_to_quantized_coefficients_at_full_rate()
    {
        var source = Enumerable.Range(0, 30).Select(i => ((i * 17) - 211) * 3).ToArray();
        var values = source.Select(sample => (double)sample).ToArray();
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97",
            values,
            6,
            5,
            5);
        var steps = (double[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "CreateIrreversibleSteps",
            5,
            80);
        var encodedSteps = steps
            .Select(step => (ushort)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "EncodeStepSize",
                step,
                16))
            .ToArray();
        var expected = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversibleOpenJpeg",
            values,
            6,
            5,
            encodedSteps,
            16);
        var tileData = (byte[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EncodeIrreversibleTile",
            source,
            6,
            5,
            16,
            steps,
            encodedSteps,
            0d,
            source.Length * 2,
            1,
            new[] { 0d },
            0);
        var actual = DecodeIrreversibleTileCoefficients(tileData, width: 6, height: 5, levels: 5, precision: 16, encodedSteps);

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.True(
                Math.Abs(expected[index] - actual[index]) <= (1 << Tier1FractionalBits),
                $"coefficient {index} differed by {Math.Abs(expected[index] - actual[index])}");
        }
    }

    [Fact]
    public void Irreversible_real_fixture_tile_packets_decode_to_quantized_coefficients_at_full_rate()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.UnitFixtures.Single(item => item.Name == "Efferent unit 16-bit sample");
        var file = DicomFile.Open(fixture.Path);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var frame = pixelData.GetFrame(0).Data;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            frame,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            true,
            false);
        var values = samples.Select(sample => (double)sample).ToArray();
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Forward97",
            values,
            pixelData.Width,
            pixelData.Height,
            5);
        var steps = (double[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
            "CreateIrreversibleSteps",
            5,
            80);
        var encodedSteps = steps
            .Select(step => (ushort)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "EncodeStepSize",
                step,
                pixelData.BitsStored))
            .ToArray();
        var expected = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "QuantizeIrreversibleOpenJpeg",
            values,
            pixelData.Width,
            pixelData.Height,
            encodedSteps,
            pixelData.BitsStored);
        var tileData = (byte[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EncodeIrreversibleTile",
            samples,
            pixelData.Width,
            pixelData.Height,
            pixelData.BitsStored,
            steps,
            encodedSteps,
            0d,
            frame.Length,
            1,
            new[] { 0d },
            0);
        var actual = DecodeIrreversibleTileCoefficients(tileData, pixelData.Width, pixelData.Height, 5, pixelData.BitsStored, encodedSteps);

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.True(
                Math.Abs(expected[index] - actual[index]) <= (1 << Tier1FractionalBits),
                $"coefficient {index} differed by {Math.Abs(expected[index] - actual[index])}");
        }
    }

    [Fact]
    public void Irreversible_inverse_wavelet_uses_openjpeg_high_pass_scaling()
    {
        var coefficients = new[] { 0.0, 128.0 };

        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardIrreversibleWavelet",
            "Inverse97",
            coefficients,
            2,
            1,
            1);

        Assert.Equal(-127.995771949918, coefficients[0], precision: 6);
        Assert.Equal(127.995769159094, coefficients[1], precision: 6);
    }

    private static DicomDataset CloneForTransferSyntax(DicomDataset source, DicomTransferSyntax transferSyntax)
    {
        var clone = new DicomDataset(transferSyntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }

    [Fact]
    public void Standard_frame_codestream_tile_payload_reconstructs_shifted_samples_exactly()
    {
        var dataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 5, columns: 6);
        var pixelData = DicomPixelData.Create(dataset);
        var frame = pixelData.GetFrame(0).Data;
        var shifted = frame.Select(value => (int)value - 128).ToArray();
        var codec = new Jpeg2000ClassicFrameCodec();
        var encoded = codec.EncodeFrame(
            pixelData,
            frame,
            irreversible: false,
            qualityTolerance: 1,
            Jpeg2000ProgressionOrder.LRCP,
            layerCount: 1,
            usesMultipleComponentTransform: false,
            encodeSignedPixelValuesAsUnsigned: false);
        var tileData = ExtractTileData(encoded);
        var coefficients = DecodeTileCoefficients(tileData, width: 6, height: 5, levels: 5, precision: 8);
        var expectedCoefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            shifted,
            6,
            5,
            5,
            0,
            0);

        Assert.Equal(expectedCoefficients, coefficients);

        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Inverse53",
            coefficients,
            6,
            5,
            5,
            0,
            0);

        Assert.Equal(shifted, coefficients);
    }

    private static int CalculateBitCount(int[] data)
    {
        var max = 0;
        foreach (var value in data)
        {
            var abs = value < 0 ? -value : value;
            if (abs > max)
            {
                max = abs;
            }
        }

        var bitCount = 0;
        while (max > 0)
        {
            max >>= 1;
            bitCount++;
        }

        return bitCount;
    }

    private static System.Reflection.Assembly Jpeg2000Assembly => typeof(DicomJpeg2000LosslessCodec).Assembly;

    private static object Create(string typeName, params object[] args)
    {
        var type = Jpeg2000Assembly.GetType(typeName, throwOnError: true)!;
        return Create(type, args);
    }

    private static object Create(Type type, params object[] args)
    {
        return Activator.CreateInstance(type, args)!;
    }

    private static object Invoke(object instance, string method, params object[] args)
    {
        var type = instance.GetType();
        while (type != null)
        {
            var candidate = type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SingleOrDefault(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length);
            if (candidate != null)
            {
                return candidate.Invoke(instance, args)!;
            }

            type = type.BaseType;
        }

        throw new MissingMethodException(instance.GetType().FullName, method);
    }

    private static object InvokeStatic(string typeName, string method, params object[] args)
    {
        var type = Jpeg2000Assembly.GetType(typeName, throwOnError: true)!;
        return type.GetMethod(
            method,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, args)!;
    }

    private static T Property<T>(object instance, string property)
    {
        return (T)instance.GetType().GetProperty(property)!.GetValue(instance)!;
    }

    private static Array ToArray(string elementTypeName, object value)
    {
        var elementType = Jpeg2000Assembly.GetType(elementTypeName, throwOnError: true)!;
        return ToArray(elementType, value);
    }

    private static Array ToArray(Type elementType, object value)
    {
        var array = Array.CreateInstance(elementType, 1);
        array.SetValue(value, 0);
        return array;
    }

    private static Array CreateComponents(Jpeg2000SizeSegment size, Jpeg2000CodingStyleDefault coding)
    {
        var elementTypeName = "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent";
        var elementType = Jpeg2000Assembly.GetType(elementTypeName, throwOnError: true)!;
        var array = Array.CreateInstance(elementType, size.Components.Count);
        var tileX0 = (int)Math.Max(size.ImageOffsetX, size.TileOffsetX);
        var tileY0 = (int)Math.Max(size.ImageOffsetY, size.TileOffsetY);
        var tileX1 = (int)Math.Min(size.ReferenceGridWidth, size.TileOffsetX + size.TileWidth);
        var tileY1 = (int)Math.Min(size.ReferenceGridHeight, size.TileOffsetY + size.TileHeight);
        for (var component = 0; component < size.Components.Count; component++)
        {
            var source = size.Components[component];
            array.SetValue(
                Create(
                    elementType,
                    component,
                    tileX0,
                    tileY0,
                    tileX1 - tileX0,
                    tileY1 - tileY0,
                    coding.DecompositionLevels,
                    source.Precision,
                    source.IsSigned),
                component);
        }

        return array;
    }

    private static int[] DecodeTileCoefficients(byte[] tileData, int width, int height, int levels, int precision, int layerCount = 1)
    {
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            width,
            height,
            levels,
            precision,
            true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            1,
            layerCount,
            levels + 1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);
        Invoke(decoder, "Decode");

        var coefficients = new int[width * height];
        foreach (var block in ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>())
        {
            var data = Property<byte[]>(block, "Data");
            var totalPasses = Property<int>(block, "TotalPasses");
            if (data.Length == 0 || totalPasses == 0)
            {
                continue;
            }

            var orientation = Property<int>(block, "Orientation");
            var bitPlaneCount = precision + GainBits(orientation) + 1 - Property<int>(block, "ZeroBitPlanes");
            var tier1 = Create(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder",
                Property<int>(block, "Width"),
                Property<int>(block, "Height"),
                orientation,
                (byte)0);
            var blockCoefficients = (int[])Invoke(tier1, "Decode", data, totalPasses, bitPlaneCount);
            for (var y = 0; y < Property<int>(block, "Height"); y++)
            {
                for (var x = 0; x < Property<int>(block, "Width"); x++)
                {
                    coefficients[((Property<int>(block, "Y0") + y) * width) + Property<int>(block, "X0") + x] =
                        blockCoefficients[(y * Property<int>(block, "Width")) + x];
                }
            }
        }

        return coefficients;
    }

    private static object[] DecodeTileBlocks(byte[] codestream, int width, int height, int precision)
    {
        var coding = ReadCoding(codestream);
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            width,
            height,
            coding.DecompositionLevels,
            precision,
            true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            ExtractTileData(codestream),
            1,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            coding.CodeBlockStyle);
        Invoke(decoder, "Decode");
        return ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>().ToArray();
    }

    private static string DescribeDecodedBlockForCoefficient(byte[] codestream, int width, int height, int precision, int coefficientIndex)
    {
        var coding = ReadCoding(codestream);
        var tileData = ExtractTileData(codestream);
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            width,
            height,
            coding.DecompositionLevels,
            precision,
            true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            1,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            coding.CodeBlockStyle);
        var packets = ((IEnumerable)Invoke(decoder, "Decode")).Cast<object>().ToArray();
        var x = coefficientIndex % width;
        var y = coefficientIndex / width;
        var text = new System.Text.StringBuilder();
        text.AppendLine($"cod layers={coding.LayerCount} levels={coding.DecompositionLevels} style={coding.CodeBlockStyle}");
        foreach (var block in ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>())
        {
            var x0 = Property<int>(block, "X0");
            var y0 = Property<int>(block, "Y0");
            var blockWidth = Property<int>(block, "Width");
            var blockHeight = Property<int>(block, "Height");
            if (x < x0 || x >= x0 + blockWidth || y < y0 || y >= y0 + blockHeight)
            {
                continue;
            }

            var orientation = Property<int>(block, "Orientation");
            var localIndex = ((y - y0) * blockWidth) + x - x0;
            var totalPasses = Property<int>(block, "TotalPasses");
            var data = Property<byte[]>(block, "Data");
            var bitPlaneCount = precision + GainBits(orientation) + 1 - Property<int>(block, "ZeroBitPlanes");
            var tier1Concat = Create(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder",
                blockWidth,
                blockHeight,
                orientation,
                coding.CodeBlockStyle);
            var concatCoefficients = (int[])Invoke(tier1Concat, "Decode", data, totalPasses, bitPlaneCount);
            var tier1Segments = Create(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder",
                blockWidth,
                blockHeight,
                orientation,
                coding.CodeBlockStyle);
            var segmentCoefficients = (int[])Invoke(tier1Segments, "DecodeSegments", Property<object>(block, "Segments"), bitPlaneCount);

            text.AppendLine($"block index={Property<int>(block, "Index")} orientation={orientation} x0={x0} y0={y0} w={blockWidth} h={blockHeight}");
            text.AppendLine($"zeroBitPlanes={Property<int>(block, "ZeroBitPlanes")} bitPlaneCount={bitPlaneCount} totalPasses={totalPasses} dataBytes={data.Length}");
            text.AppendLine($"localIndex={localIndex} concat={concatCoefficients[localIndex]} segments={segmentCoefficients[localIndex]}");
            var segments = ((IEnumerable)Property<object>(block, "Segments")).Cast<object>().ToArray();
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentData = Property<byte[]>(segments[i], "Data");
                text.AppendLine($"segment[{i}] passes={Property<int>(segments[i], "PassCount")} bytes={segmentData.Length}");
            }

            foreach (var packet in packets)
            {
                foreach (var contribution in ((IEnumerable)Property<object>(packet, "Contributions")).Cast<object>())
                {
                    if (!ReferenceEquals(Property<object>(contribution, "Block"), block))
                    {
                        continue;
                    }

                    text.AppendLine(
                        $"packet layer={Property<int>(packet, "Layer")} resolution={Property<int>(packet, "Resolution")} included={Property<bool>(contribution, "Included")} passes={Property<int>(contribution, "PassCount")} bytes={Property<int>(contribution, "ByteLength")}");
                }
            }

            break;
        }

        return text.ToString();
    }

    private static int[] DecodeIrreversibleTileCoefficients(byte[] tileData, int width, int height, int levels, int precision, ushort[] encodedSteps)
    {
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            width,
            height,
            levels,
            precision,
            true);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            1,
            1,
            levels + 1,
            Jpeg2000ProgressionOrder.LRCP,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            (byte)0);
        Invoke(decoder, "Decode");

        var coefficients = new int[width * height];
        foreach (var block in ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>())
        {
            var data = Property<byte[]>(block, "Data");
            var totalPasses = Property<int>(block, "TotalPasses");
            if (data.Length == 0 || totalPasses == 0)
            {
                continue;
            }

            var orientation = Property<int>(block, "Orientation");
            var resolution = ResolutionForBlock(levels, orientation, Property<int>(block, "X0"), Property<int>(block, "Y0"), width, height);
            var stepIndex = (int)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "SubbandIndex",
                resolution,
                orientation);
            var bitPlaneDepth = (int)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "BitPlaneDepth",
                encodedSteps[stepIndex],
                2);
            var bitPlaneCount = bitPlaneDepth - Property<int>(block, "ZeroBitPlanes");
            var tier1 = Create(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder",
                Property<int>(block, "Width"),
                Property<int>(block, "Height"),
                orientation,
                (byte)0);
            var segments = ((IEnumerable)Property<object>(block, "Segments")).Cast<object>().ToArray();
            var blockCoefficients = segments.Length > 0
                ? (int[])Invoke(tier1, "DecodeSegmentsScaled", Property<object>(block, "Segments"), bitPlaneCount)
                : (int[])Invoke(tier1, "DecodeScaled", data, totalPasses, bitPlaneCount);
            for (var y = 0; y < Property<int>(block, "Height"); y++)
            {
                for (var x = 0; x < Property<int>(block, "Width"); x++)
                {
                    coefficients[((Property<int>(block, "Y0") + y) * width) + Property<int>(block, "X0") + x] =
                        blockCoefficients[(y * Property<int>(block, "Width")) + x];
                }
            }
        }

        return coefficients;
    }

    private static int GainBits(int orientation)
    {
        return orientation == 3 ? 2 : orientation == 0 ? 0 : 1;
    }

    private static byte[] ExtractTileData(byte[] codestream)
    {
        for (var i = 0; i + 1 < codestream.Length; i++)
        {
            if (codestream[i] != 0xFF || codestream[i + 1] != 0x93)
            {
                continue;
            }

            var start = i + 2;
            var end = codestream.Length - 2;
            while (end > start && codestream[end] == 0xFF && codestream[end + 1] == 0xD9)
            {
                break;
            }

            var bytes = new byte[end - start];
            Buffer.BlockCopy(codestream, start, bytes, 0, bytes.Length);
            return bytes;
        }

        throw new InvalidOperationException("SOD marker was not found.");
    }

    private static string[] DescribePacketLayers(byte[] codestream)
    {
        var size = ReadSize(codestream);
        var coding = ReadCoding(codestream);
        var tileData = ExtractTileData(codestream);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            size.Components.Count,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            CreateComponents(size, coding),
            64,
            64,
            coding.CodeBlockStyle);
        var packets = ((IEnumerable)Invoke(decoder, "Decode")).Cast<object>();
        return packets
            .GroupBy(packet => Property<int>(packet, "Layer"))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var passCount = 0;
                var byteCount = 0;
                var included = 0;
                foreach (var contribution in group.SelectMany(packet => ((IEnumerable)Property<object>(packet, "Contributions")).Cast<object>()))
                {
                    if (Property<bool>(contribution, "Included"))
                    {
                        included++;
                    }

                    passCount += Property<int>(contribution, "PassCount");
                    byteCount += Property<int>(contribution, "ByteLength");
                }

                return $"layer={group.Key} packets={group.Count()} included={included} passes={passCount} bytes={byteCount}";
            })
            .ToArray();
    }

    private static string DescribeFirstPacketContributionDifference(byte[] referenceFrame, byte[] pureFrame)
    {
        var reference = DescribePacketContributions(referenceFrame);
        var pure = DescribePacketContributions(pureFrame);
        var missing = reference.Except(pure).Take(20).ToArray();
        var extra = pure.Except(reference).Take(20).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
        {
            return "contribution set differences:" + Environment.NewLine
                + string.Join(Environment.NewLine, missing.Select(item => "reference only: " + item))
                + Environment.NewLine
                + string.Join(Environment.NewLine, extra.Select(item => "pure only: " + item))
                + Environment.NewLine
                + "reference layers:" + Environment.NewLine
                + string.Join(Environment.NewLine, DescribePacketLayers(referenceFrame))
                + Environment.NewLine
                + "pure layers:" + Environment.NewLine
                + string.Join(Environment.NewLine, DescribePacketLayers(pureFrame));
        }

        var count = Math.Min(reference.Length, pure.Length);
        var differences = new System.Text.StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (reference[i] != pure[i])
            {
                differences.AppendLine("reference: " + reference[i]);
                differences.AppendLine("pure: " + pure[i]);
                if (differences.Length > 2000)
                {
                    break;
                }
            }
        }

        if (differences.Length > 0)
        {
            return "contribution diffs:" + Environment.NewLine + differences;
        }

        if (reference.Length != pure.Length)
        {
            return $"contribution count differs reference={reference.Length} pure={pure.Length}";
        }

        return "no contribution detail difference";
    }

    private static string[] DescribePacketContributions(byte[] codestream)
    {
        var size = ReadSize(codestream);
        var coding = ReadCoding(codestream);
        var tileData = ExtractTileData(codestream);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            size.Components.Count,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            CreateComponents(size, coding),
            64,
            64,
            coding.CodeBlockStyle);
        var packets = ((IEnumerable)Invoke(decoder, "Decode")).Cast<object>();
        return packets
            .SelectMany(packet => ((IEnumerable)Property<object>(packet, "Contributions")).Cast<object>()
                .Where(contribution => Property<bool>(contribution, "Included"))
                .Select(contribution =>
                {
                    var block = Property<object>(contribution, "Block");
                    return $"layer={Property<int>(packet, "Layer")} comp={Property<int>(packet, "Component")} res={Property<int>(packet, "Resolution")} orient={Property<int>(block, "Orientation")} x={Property<int>(block, "LocalX")} y={Property<int>(block, "LocalY")} zero={Property<int>(block, "ZeroBitPlanes")} passes={Property<int>(contribution, "PassCount")} bytes={Property<int>(contribution, "ByteLength")}";
                }))
            .ToArray();
    }

    private static string DescribeManagedTargetBlockPasses(DicomPixelData pixelData, byte[] referenceFrame, byte[] actualFrame)
    {
        var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            pixelData.GetFrame(0).Data,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            isSigned,
            false);
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ApplyForwardLevelShift",
            new[] { samples },
            pixelData.BitsStored,
            isSigned);
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            samples,
            pixelData.Width,
            pixelData.Height,
            5,
            0,
            0);
        var blocks = ((IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocks",
            coefficients,
            pixelData.Width,
            pixelData.Height,
            5,
            pixelData.BitsStored,
            true,
            null!)).Cast<object>();
        var block = blocks.Single(item => Property<int>(item, "Orientation") == 3
            && Property<int>(item, "X") == 2
            && Property<int>(item, "Y") == 3);
        var lengths = Property<int[]>(block, "PassLengths");
        var distortions = Property<double[]>(block, "PassDistortions");
        var blocksByResolution = InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocksByResolution",
            coefficients,
            pixelData.Width,
            pixelData.Height,
            pixelData.BitsStored,
            true,
            null!,
            0,
            null!);
        var selectedBlock = ((IEnumerable)blocksByResolution)
            .Cast<IEnumerable>()
            .ElementAt(5)
            .Cast<object>()
            .Single(item => Property<int>(item, "Orientation") == 3
                && Property<int>(item, "X") == 2
                && Property<int>(item, "Y") == 3);
        var layerRates = new double[] { 1280, 640, 320, 160, 80, 40, 20, 0 };
        var mainHeaderBytesBeforeSot = (int)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "EstimateMainHeaderBytesBeforeSot",
            1,
            false);
        var targets = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "CreateLayerByteTargets",
            layerRates,
            layerRates.Length,
            (int)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
                "CalculateRateControlSourceByteLength",
                pixelData.GetFrame(0).Data.Length,
                pixelData.BitsStored,
                pixelData.BitsAllocated),
            mainHeaderBytesBeforeSot);
        var selections = ((IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "AllocateLayerSelections",
            blocksByResolution,
            targets,
            true)).Cast<object>().ToArray();
        var targetSelections = selections
            .Select(selection => (int)selection.GetType().GetMethod("get_Item")!.Invoke(selection, new[] { selectedBlock })!)
            .ToArray();
        var referenceFirstContribution = ExtractDecodedBlockData(referenceFrame, resolution: 5, orientation: 3, x: 2, y: 3);
        var actualFirstContribution = ExtractDecodedBlockData(actualFrame, resolution: 5, orientation: 3, x: 2, y: 3);
        return "managed target block: resolution=" + Property<int>(selectedBlock, "Resolution")
            + " orientation=" + Property<int>(selectedBlock, "Orientation")
            + " x=" + Property<int>(selectedBlock, "X")
            + " y=" + Property<int>(selectedBlock, "Y")
            + " zero=" + Property<int>(selectedBlock, "ZeroBitPlanes")
            + " lengths=" + string.Join(",", lengths)
            + " distortions=" + string.Join(",", distortions.Select(value => value.ToString("R")))
            + " targets=" + string.Join(",", targets)
            + " selections=" + string.Join(",", targetSelections)
            + " nativeFirst=" + string.Join("", referenceFirstContribution.Select(value => value.ToString("X2")))
            + " pureFirst=" + string.Join("", actualFirstContribution.Select(value => value.ToString("X2")))
            + " pureTier1=" + string.Join("", Property<byte[]>(selectedBlock, "Data").Select(value => value.ToString("X2")));
    }

    private static string DescribeNativePassBoundaryDifferences(DicomPixelData pixelData, byte[] referenceFrame)
    {
        var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
        var samples = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ReadSamples",
            pixelData.GetFrame(0).Data,
            pixelData.BitsAllocated,
            pixelData.BitsStored,
            isSigned,
            false);
        InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "ApplyForwardLevelShift",
            new[] { samples },
            pixelData.BitsStored,
            isSigned);
        var coefficients = (int[])InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardWavelet",
            "Forward53",
            samples,
            pixelData.Width,
            pixelData.Height,
            5,
            0,
            0);
        var blocksByResolution = (IEnumerable)InvokeStatic(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardFrameEncoder",
            "BuildCodeBlocksByResolution",
            coefficients,
            pixelData.Width,
            pixelData.Height,
            pixelData.BitsStored,
            true,
            null!,
            0,
            null!);
        var blocks = blocksByResolution.Cast<IEnumerable>()
            .SelectMany(items => items.Cast<object>())
            .ToDictionary(BlockKey, item => item);
        var size = ReadSize(referenceFrame);
        var coding = ReadCoding(referenceFrame);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            ExtractTileData(referenceFrame),
            size.Components.Count,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            CreateComponents(size, coding),
            64,
            64,
            coding.CodeBlockStyle);
        var totals = new System.Collections.Generic.Dictionary<string, (int Passes, int Bytes)>();
        var differences = new System.Collections.Generic.List<string>();
        foreach (var packet in ((IEnumerable)Invoke(decoder, "Decode")).Cast<object>())
        {
            foreach (var contribution in ((IEnumerable)Property<object>(packet, "Contributions")).Cast<object>())
            {
                if (!Property<bool>(contribution, "Included"))
                {
                    continue;
                }

                var decodedBlock = Property<object>(contribution, "Block");
                var key = DecodedBlockKey(Property<int>(packet, "Resolution"), decodedBlock);
                var previous = totals.TryGetValue(key, out var value) ? value : (Passes: 0, Bytes: 0);
                var current = (Passes: previous.Passes + Property<int>(contribution, "PassCount"), Bytes: previous.Bytes + Property<int>(contribution, "ByteLength"));
                totals[key] = current;
                if (!blocks.TryGetValue(key, out var encodedBlock))
                {
                    differences.Add("missing managed block " + key);
                    continue;
                }

                var lengths = Property<int[]>(encodedBlock, "PassLengths");
                var managedBytes = current.Passes > 0 && current.Passes <= lengths.Length ? lengths[current.Passes - 1] : -1;
                if (managedBytes != current.Bytes)
                {
                    differences.Add("boundary " + key + " passes=" + current.Passes + " native=" + current.Bytes + " managed=" + managedBytes);
                }
            }
        }

        return differences.Count == 0
            ? "all Native pass boundaries match managed pass lengths"
            : string.Join(Environment.NewLine, differences.Take(50));
    }

    private static string BlockKey(object block)
    {
        return BlockKey(Property<int>(block, "Resolution"), block);
    }

    private static string BlockKey(int resolution, object block)
    {
        return resolution + ":" + Property<int>(block, "Orientation") + ":" + Property<int>(block, "X") + ":" + Property<int>(block, "Y");
    }

    private static string DecodedBlockKey(int resolution, object block)
    {
        return resolution + ":" + Property<int>(block, "Orientation") + ":" + Property<int>(block, "LocalX") + ":" + Property<int>(block, "LocalY");
    }

    private static byte[] ExtractDecodedBlockData(byte[] codestream, int resolution, int orientation, int x, int y)
    {
        var size = ReadSize(codestream);
        var coding = ReadCoding(codestream);
        var tileData = ExtractTileData(codestream);
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            (int)(size.ReferenceGridWidth - size.ImageOffsetX),
            (int)(size.ReferenceGridHeight - size.ImageOffsetY),
            coding.DecompositionLevels,
            size.Components[0].Precision,
            size.Components[0].IsSigned);
        var decoder = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardPacketDecoder",
            tileData,
            size.Components.Count,
            coding.LayerCount,
            coding.DecompositionLevels + 1,
            coding.ProgressionOrder,
            ToArray("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent", component),
            64,
            64,
            coding.CodeBlockStyle);
        var packets = ((IEnumerable)Invoke(decoder, "Decode")).Cast<object>();
        foreach (var packet in packets)
        {
            if (Property<int>(packet, "Resolution") != resolution)
            {
                continue;
            }

            foreach (var contribution in ((IEnumerable)Property<object>(packet, "Contributions")).Cast<object>())
            {
                var block = Property<object>(contribution, "Block");
                if (Property<int>(block, "Orientation") == orientation
                    && Property<int>(block, "LocalX") == x
                    && Property<int>(block, "LocalY") == y)
                {
                    return Property<byte[]>(block, "Data");
                }
            }
        }

        return Array.Empty<byte>();
    }

    private static string DescribeByteDifference(string leftName, byte[] left, string rightName, byte[] right)
    {
        var count = Math.Min(left.Length, right.Length);
        for (var i = 0; i < count; i++)
        {
            if (left[i] != right[i])
            {
                return $"{leftName}.Length={left.Length} {rightName}.Length={right.Length} firstDiff={i} {leftName}=0x{left[i]:X2} {rightName}=0x{right[i]:X2}";
            }
        }

        return $"{leftName}.Length={left.Length} {rightName}.Length={right.Length} firstDiff=none";
    }

    private static Jpeg2000SizeSegment ReadSize(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.SIZ)
            {
                return Jpeg2000SizeSegment.Parse(segment);
            }
        }

        throw new InvalidOperationException("SIZ marker was not found.");
    }

    private static Jpeg2000CodingStyleDefault ReadCoding(byte[] codestream)
    {
        var reader = new Jpeg2000CodestreamReader(codestream);
        while (!reader.EndOfData)
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.COD)
            {
                return Jpeg2000CodingStyleDefault.Parse(segment);
            }
        }

        throw new InvalidOperationException("COD marker was not found.");
    }

    private static double[] DequantizeLikeManagedDecoder(int[] quantized, int width, int height, int precision, ushort[] encodedSteps)
    {
        var restored = new double[quantized.Length];
        var component = Create(
            "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardComponent",
            0,
            0,
            0,
            width,
            height,
            5,
            precision,
            true);
        Invoke(component, "BuildCodeBlocks", 64, 64);
        foreach (var block in ((IEnumerable)Invoke(component, "AllCodeBlocks")).Cast<object>())
        {
            var orientation = Property<int>(block, "Orientation");
            var resolution = ResolutionForBlock(5, orientation, Property<int>(block, "X0"), Property<int>(block, "Y0"), width, height);
            var index = (int)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "SubbandIndex",
                resolution,
                orientation);
            var step = (double)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000QuantizationTable",
                "DecodeStepSize",
                encodedSteps[index],
                precision);
            for (var y = Property<int>(block, "Y0"); y < Property<int>(block, "Y0") + Property<int>(block, "Height"); y++)
            {
                for (var x = Property<int>(block, "X0"); x < Property<int>(block, "X0") + Property<int>(block, "Width"); x++)
                {
                    var offset = (y * width) + x;
                    restored[offset] = quantized[offset] * step / (1 << Tier1FractionalBits);
                }
            }
        }

        return restored;
    }

    private static int ResolutionForBlock(int levels, int orientation, int x0, int y0, int width, int height)
    {
        if (orientation == 0)
        {
            return 0;
        }

        for (var resolution = 1; resolution <= levels; resolution++)
        {
            var bands = (IEnumerable)InvokeStatic(
                "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardGeometry",
                "GetSubbands",
                width,
                height,
                0,
                0,
                levels,
                resolution);
            if (bands.Cast<object>().Any(band => Property<int>(band, "Orientation") == orientation
                && x0 >= Property<int>(band, "OffsetX")
                && y0 >= Property<int>(band, "OffsetY")
                && x0 < Property<int>(band, "OffsetX") + Property<int>(band, "Width")
                && y0 < Property<int>(band, "OffsetY") + Property<int>(band, "Height")))
            {
                return resolution;
            }
        }

        return -1;
    }

    private static int Round(double value)
    {
        return value >= 0 ? (int)(value + 0.5) : (int)(value - 0.5);
    }
}
