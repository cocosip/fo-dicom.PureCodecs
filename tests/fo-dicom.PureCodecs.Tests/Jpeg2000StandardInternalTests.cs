using System;
using System.Collections;
using System.Linq;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000StandardInternalTests
{
    private const int Tier1FractionalBits = 6;

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

        Assert.Equal(coefficients, decoded);
    }

    [Fact]
    public void Tier1_pass_snapshot_decodes_as_valid_truncated_code_block()
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
        Invoke(encoder, "Encode", args);
        var passSnapshots = (byte[][])args[3];
        var decoder = Create("FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard.Jpeg2000StandardTier1Decoder", 4, 4, 0, (byte)0);
        var decoded = (int[])Invoke(decoder, "Decode", passSnapshots[truncatedPassCount - 1], truncatedPassCount, bitCount);

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

        Assert.Equal(coefficients, decoded);
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
        return instance.GetType().GetMethods()
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
            .Invoke(instance, args)!;
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
        var array = Array.CreateInstance(elementType, 1);
        array.SetValue(value, 0);
        return array;
    }

    private static int[] DecodeTileCoefficients(byte[] tileData, int width, int height, int levels, int precision)
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
}
