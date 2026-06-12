using FellowOakDicom.PureCodecs.JpegLs.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsCodingPrimitiveTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 2)]
    [InlineData(17, 4)]
    public void Golomb_writer_and_reader_round_trip_mapped_error_values(int value, int parameter)
    {
        var writer = new JpegLsGolombCodeWriter();

        writer.Write(value, parameter);
        var reader = new JpegLsGolombCodeReader(writer.ToArray());

        Assert.Equal(value, reader.Read(parameter));
    }

    [Fact]
    public void Context_model_initializes_default_regular_context_state()
    {
        var context = new JpegLsContextModel(maximumSampleValue: 255, nearLossless: 0, resetThreshold: 64);

        var state = context.GetRegularContext(0);

        Assert.Equal(4, state.A);
        Assert.Equal(1, state.N);
        Assert.Equal(0, state.B);
        Assert.Equal(0, state.C);
    }

    [Fact]
    public void Context_model_updates_regular_context_state_after_prediction_error()
    {
        var context = new JpegLsContextModel(maximumSampleValue: 255, nearLossless: 0, resetThreshold: 64);
        var state = context.GetRegularContext(0);

        state.Update(errorValue: -3, nearLossless: 0);

        Assert.Equal(7, state.A);
        Assert.Equal(2, state.N);
        Assert.Equal(-1, state.B);
        Assert.Equal(-1, state.C);
    }

    [Fact]
    public void Traits_compute_default_coding_parameters_for_8_bit_lossless()
    {
        var traits = JpegLsTraits.CreateDefault(maximumSampleValue: 255, nearLossless: 0, resetThreshold: 64);

        Assert.Equal(256, traits.Range);
        Assert.Equal(8, traits.QuantizedBitsPerPixel);
        Assert.Equal(32, traits.Limit);
        Assert.Equal(3, traits.Threshold1);
        Assert.Equal(7, traits.Threshold2);
        Assert.Equal(21, traits.Threshold3);
        Assert.Equal(64, traits.ResetThreshold);
    }

    [Fact]
    public void Predictor_uses_median_edge_detection()
    {
        Assert.Equal(10, JpegLsPredictor.Predict(10, 20, 25));
        Assert.Equal(20, JpegLsPredictor.Predict(10, 20, 5));
        Assert.Equal(18, JpegLsPredictor.Predict(10, 20, 12));
    }

    [Fact]
    public void Context_model_resolves_gradient_context_with_sign_symmetry()
    {
        var context = new JpegLsContextModel(maximumSampleValue: 255, nearLossless: 0, resetThreshold: 64);

        var positive = context.GetContext(1, 0, -1, out var positiveSign);
        var negative = context.GetContext(-1, 0, 1, out var negativeSign);

        Assert.Same(positive, negative);
        Assert.Equal(0, positiveSign);
        Assert.Equal(-1, negativeSign);
    }

    [Fact]
    public void Run_mode_writer_and_reader_round_trip_run_length_and_interruption()
    {
        var traits = JpegLsTraits.CreateDefault(maximumSampleValue: 255, nearLossless: 0, resetThreshold: 64);
        var writerScanner = new JpegLsRunModeScanner(traits);
        var writer = new JpegLsGolombCodeWriter();

        writerScanner.EncodeRunLength(writer, runLength: 5, endOfLine: false);
        writerScanner.EncodeRunInterruption(writer, writerScanner.RunInterruptionContexts[1], errorValue: -2);

        var readerScanner = new JpegLsRunModeScanner(traits);
        var reader = new JpegLsGolombCodeReader(writer.ToArray());

        Assert.Equal(5, readerScanner.DecodeRunLength(reader, remainingInLine: 10));
        Assert.Equal(-2, readerScanner.DecodeRunInterruption(reader, readerScanner.RunInterruptionContexts[1]));
    }

    [Theory]
    [InlineData(260, 8, 255)]
    [InlineData(-2, 8, 0)]
    [InlineData(42, 8, 42)]
    [InlineData(4100, 12, 4095)]
    public void Near_lossless_clamp_limits_samples_to_bit_depth(int sample, int bitsStored, int expected)
    {
        Assert.Equal(expected, JpegLsNearLossless.ClampSample(sample, bitsStored));
    }

    [Fact]
    public void Near_lossless_tolerance_checks_absolute_sample_error()
    {
        Assert.True(JpegLsNearLossless.IsWithinTolerance(100, 103, allowedError: 3));
        Assert.False(JpegLsNearLossless.IsWithinTolerance(100, 104, allowedError: 3));
    }
}
