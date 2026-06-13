using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000ClassicCodingTests
{
    [Fact]
    public void Mq_state_tables_expose_decoder_and_encoder_transitions()
    {
        var decoder = Jpeg2000MqDecoderStateTable.Default;
        var encoder = Jpeg2000MqEncoderStateTable.Default;

        Assert.True(decoder.Count >= 47);
        Assert.True(encoder.Count >= 47);
        Assert.Equal(0x5601, decoder[0].ProbabilityEstimate);
        Assert.Equal(decoder[0].MostProbableSymbolNextIndex, encoder[0].MostProbableSymbolNextIndex);
        Assert.Equal(decoder[0].LeastProbableSymbolNextIndex, encoder[0].LeastProbableSymbolNextIndex);
    }

    [Fact]
    public void Mq_coder_round_trips_context_symbols_and_uses_marker_safe_byte_stuffing()
    {
        var symbols = new[]
        {
            true, true, true, true, true, true, true, true,
            false, true, false, false, true, true, false
        };
        var encoder = new Jpeg2000MqEncoder();

        foreach (var symbol in symbols)
        {
            encoder.Encode(contextIndex: 0, symbol);
        }

        var bytes = encoder.ToArray();
        var decoder = new Jpeg2000MqDecoder(bytes);

        Assert.Contains(new byte[] { 0xFF, 0x00 }, bytes);
        foreach (var expected in symbols)
        {
            Assert.Equal(expected, decoder.Decode(contextIndex: 0));
        }
    }

    [Fact]
    public void Tier1_passes_report_lengths_and_reconstruct_code_block_coefficients()
    {
        var coefficients = new[]
        {
            0, 5, -2, 0,
            7, 0, 0, -1,
            0, 0, 3, 0,
            -4, 0, 0, 2
        };
        var block = new Jpeg2000ClassicCodeBlock(4, 4, coefficients);

        var encoded = Jpeg2000Tier1Encoder.Encode(block, maxBitPlane: 3);
        var decoded = Jpeg2000Tier1Decoder.Decode(encoded);

        Assert.Contains(encoded.Passes, pass => pass.Type == Jpeg2000Tier1PassType.SignificancePropagation);
        Assert.Contains(encoded.Passes, pass => pass.Type == Jpeg2000Tier1PassType.MagnitudeRefinement);
        Assert.Contains(encoded.Passes, pass => pass.Type == Jpeg2000Tier1PassType.Cleanup);
        Assert.All(encoded.Passes, pass => Assert.True(pass.ByteLength > 0));
        Assert.Equal(coefficients, decoded.Coefficients);
    }

    [Fact]
    public void Classic_code_block_encoder_and_decoder_round_trip_coefficients()
    {
        var block = new Jpeg2000ClassicCodeBlock(
            width: 3,
            height: 2,
            coefficients: new[] { 9, -4, 0, 1, 0, -8 });

        var encoded = Jpeg2000ClassicCodeBlockEncoder.Encode(block);
        var decoded = Jpeg2000ClassicCodeBlockDecoder.Decode(encoded);

        Assert.Equal(block.Width, decoded.Width);
        Assert.Equal(block.Height, decoded.Height);
        Assert.Equal(block.Coefficients, decoded.Coefficients);
        Assert.True(encoded.CodingPasses.Count >= 3);
    }

    [Fact]
    public void Tag_tree_encoder_and_decoder_round_trip_values()
    {
        var tree = new Jpeg2000TagTree(width: 3, height: 2, new[] { 0, 2, 1, 5, 3, 4 });

        var encoded = Jpeg2000TagTreeEncoder.Encode(tree);
        var decoded = Jpeg2000TagTreeDecoder.Decode(encoded);

        Assert.Equal(tree.Width, decoded.Width);
        Assert.Equal(tree.Height, decoded.Height);
        Assert.Equal(tree.Values, decoded.Values);
        Assert.Equal(0, decoded.GetValue(0, 0));
        Assert.Equal(5, decoded.GetValue(0, 1));
    }

    [Fact]
    public void Packet_encoder_and_decoder_handle_empty_packets()
    {
        var packet = Jpeg2000ClassicPacket.Empty(layerIndex: 0, resolutionLevel: 0, componentIndex: 0, precinctIndex: 0);

        var bytes = Jpeg2000ClassicPacketEncoder.Encode(packet);
        var decoded = Jpeg2000ClassicPacketDecoder.Decode(bytes);

        Assert.True(decoded.IsEmpty);
        Assert.Empty(decoded.Contributions);
    }

    [Fact]
    public void Packet_encoder_and_decoder_preserve_multi_layer_contributions()
    {
        var packet = new Jpeg2000ClassicPacket(
            layerIndex: 2,
            resolutionLevel: 1,
            componentIndex: 0,
            precinctIndex: 3,
            new[]
            {
                new Jpeg2000PacketContribution(codeBlockIndex: 0, codingPassCount: 2, byteLength: 9),
                new Jpeg2000PacketContribution(codeBlockIndex: 1, codingPassCount: 1, byteLength: 4)
            });

        var bytes = Jpeg2000ClassicPacketEncoder.Encode(packet);
        var decoded = Jpeg2000ClassicPacketDecoder.Decode(bytes);

        Assert.False(decoded.IsEmpty);
        Assert.Equal(2, decoded.LayerIndex);
        Assert.Equal(2, decoded.Contributions.Count);
        Assert.Equal(9, decoded.Contributions[0].ByteLength);
        Assert.Equal(1, decoded.Contributions[1].CodingPassCount);
    }

    [Fact]
    public void Pcrd_allocator_honors_rate_target_and_keeps_optional_final_lossless_layer()
    {
        var passes = new[]
        {
            new Jpeg2000RateDistortionPass(0, byteLength: 30, distortionReduction: 12.0),
            new Jpeg2000RateDistortionPass(1, byteLength: 20, distortionReduction: 6.0),
            new Jpeg2000RateDistortionPass(2, byteLength: 50, distortionReduction: 8.0)
        };

        var layers = Jpeg2000PcrdLayerAllocator.Allocate(
            passes,
            new Jpeg2000RateControlOptions(rate: 1.0, rateLevels: 2, targetRatio: 2.0, numLayers: 2, includeFinalLosslessLayer: true),
            totalUncompressedBytes: 200);

        Assert.Equal(3, layers.Count);
        Assert.True(layers[0].TotalBytes <= 100);
        Assert.True(layers[1].TotalBytes <= 100);
        Assert.True(layers[2].IsFinalLosslessLayer);
        Assert.Equal(3, layers[2].Passes.Count);
    }
}
