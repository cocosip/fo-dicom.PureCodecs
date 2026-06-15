using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000HtCodingTests
{
    [Fact]
    public void Ht_vlc_tables_match_annex_c_prefix_entries()
    {
        var initial = Jpeg2000HtVlcTable.InitialQuadRow.Lookup(context: 0, codeBits: 0x06);
        var nonInitial = Jpeg2000HtVlcTable.NonInitialQuadRow.Lookup(context: 0, codeBits: 0x00);

        Assert.Equal(0x1, initial.Rho);
        Assert.False(initial.HasMagnitudeResidual);
        Assert.Equal(4, initial.CodewordLength);
        Assert.Equal(0x1, nonInitial.Rho);
        Assert.False(nonInitial.HasMagnitudeResidual);
        Assert.Equal(3, nonInitial.CodewordLength);
    }

    [Fact]
    public void Ht_standard_decode_tables_match_openjph_vector_entries()
    {
        Assert.Equal(0x111F, Jpeg2000HtStandardTables.VlcTable0[(0 << 7) | 0x3F]);
        Assert.Equal(0x0014, Jpeg2000HtStandardTables.VlcTable0[(1 << 7) | 0x1E]);
    }

    [Fact]
    public void Ht_vlc_encoder_and_decoder_round_trip_quad_symbols()
    {
        var symbols = new[]
        {
            new Jpeg2000HtVlcSymbol(context: 0, rho: 0x1, hasMagnitudeResidual: false, embK: 0, emb1: 0),
            new Jpeg2000HtVlcSymbol(context: 0, rho: 0x3, hasMagnitudeResidual: true, embK: 0x2, emb1: 0x2),
            new Jpeg2000HtVlcSymbol(context: 0, rho: 0x8, hasMagnitudeResidual: false, embK: 0, emb1: 0),
        };

        var bytes = Jpeg2000HtVlcEncoder.Encode(symbols, initialQuadRow: true);
        var decoded = Jpeg2000HtVlcDecoder.Decode(bytes, symbols.Length, initialQuadRow: true);

        Assert.Equal(symbols, decoded);
    }

    [Fact]
    public void MagSgn_encoder_and_decoder_round_trip_coefficient_magnitudes_and_signs()
    {
        var coefficients = new[] { 0, 5, -2, 19, -31, 1, 0, -7 };

        var bytes = Jpeg2000HtMagSgnEncoder.Encode(coefficients);
        var decoded = Jpeg2000HtMagSgnDecoder.Decode(bytes, coefficients.Length);

        Assert.Equal(coefficients, decoded);
    }

    [Fact]
    public void Ht_cleanup_pass_scans_code_block_as_quad_pairs()
    {
        var block = new Jpeg2000ClassicCodeBlock(
            width: 4,
            height: 4,
            coefficients: new[]
            {
                1, 0, 2, 0,
                0, 3, 0, 4,
                5, 0, 6, 0,
                0, 7, 0, 8
            });

        var cleanup = Jpeg2000HtCleanupPass.Encode(block);

        Assert.Equal(new[]
        {
            new Jpeg2000HtQuad(0, 0, 0x9),
            new Jpeg2000HtQuad(2, 0, 0x9),
            new Jpeg2000HtQuad(0, 2, 0x9),
            new Jpeg2000HtQuad(2, 2, 0x9),
        }, cleanup.Quads);
    }

    [Fact]
    public void Ht_block_encoder_assembles_three_segments_and_round_trips_coefficients()
    {
        var block = new Jpeg2000ClassicCodeBlock(
            width: 3,
            height: 2,
            coefficients: new[] { 9, -4, 0, 1, 0, -8 });

        var encoded = Jpeg2000HtCodeBlockEncoder.Encode(block);
        var decoded = Jpeg2000HtCodeBlockDecoder.Decode(encoded);

        Assert.True(encoded.MagSgnLength > 0);
        Assert.True(encoded.MelLength > 0);
        Assert.True(encoded.VlcLength > 0);
        Assert.Equal(block.Width, decoded.Width);
        Assert.Equal(block.Height, decoded.Height);
        Assert.Equal(block.Coefficients, decoded.Coefficients);
    }

    [Fact]
    public void Ht_cleanup_decoder_matches_openjph_cup_only_vector()
    {
        // Generated with OpenJPH ojph_encode_codeblock32 from coefficients:
        // 8,0,4,0 / 0,2,0,1, with missing_msbs=28 and one cleanup pass.
        var cleanupPass = new byte[] { 0xFC, 0x01, 0xE7, 0x74, 0x00 };

        var decoded = Jpeg2000HtCodeBlockDecoder.DecodeStandardCleanupPass(
            cleanupPass,
            width: 4,
            height: 2,
            missingMostSignificantBits: 28);

        Assert.Equal(new[] { 10, 0, 6, 0, 0, 0, 0, 0 }, decoded.Coefficients);
    }

    [Theory]
    [MemberData(nameof(MelStateMachineVectors))]
    public void Mel_state_machine_matches_fixed_vectors(bool[] events, byte[] bytes)
    {
        Assert.Equal(bytes, Jpeg2000HtMelEncoder.EncodeEvents(events));
        Assert.Equal(events, Jpeg2000HtMelDecoder.DecodeEvents(bytes, events.Length));
    }

    [Fact]
    public void Mel_encoder_and_decoder_round_trip_event_state_machine()
    {
        var events = new[]
        {
            false, false, false, true,
            false, true,
            false, false, false, false, true,
            true,
            false, false, false, false, false, false, false, false,
            false, true
        };

        var encoded = Jpeg2000HtMelEncoder.EncodeEvents(events);
        var decoded = Jpeg2000HtMelDecoder.DecodeEvents(encoded, events.Length);

        Assert.Equal(events, decoded);
    }

    public static TheoryData<bool[], byte[]> MelStateMachineVectors
    {
        get
        {
            return new TheoryData<bool[], byte[]>
            {
                { new[] { true }, new byte[] { 0x00 } },
                { new[] { false, true }, new byte[] { 0x80 } },
                {
                    new[]
                    {
                        false, false, false, true,
                        false, true,
                        false, false, false, false, true,
                        true,
                        false, false, false, false, false, false, false, false,
                        false, true
                    },
                    new byte[] { 0xE4, 0xD3, 0xD0 }
                },
                { FortyFalseEvents, new byte[] { 0xFF, 0x78 } }
            };
        }
    }

    private static bool[] FortyFalseEvents
    {
        get
        {
            var events = new bool[40];
            return events;
        }
    }
}
