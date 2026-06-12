using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLosslessPredictorTests
{
    [Fact]
    public void Predictor_1_uses_left_sample()
    {
        Assert.Equal(120, JpegLosslessPredictor.Predict(selectionValue: 1, left: 120, above: 80, upperLeft: 40));
    }

    [Theory]
    [InlineData(2, 80)]
    [InlineData(3, 40)]
    [InlineData(4, 160)]
    [InlineData(5, 140)]
    [InlineData(6, 120)]
    [InlineData(7, 100)]
    public void Predictors_2_through_7_use_jpeg_lossless_formulas(int selectionValue, int expected)
    {
        var actual = JpegLosslessPredictor.Predict(selectionValue, left: 120, above: 80, upperLeft: 40);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Predictor_uses_half_range_for_first_sample(int selectionValue)
    {
        var actual = JpegLosslessPredictor.PredictSample(
            selectionValue,
            samplePrecision: 8,
            x: 0,
            y: 0,
            left: 0,
            above: 0,
            upperLeft: 0);

        Assert.Equal(128, actual);
    }

    [Fact]
    public void Predictor_uses_left_sample_for_first_row_after_first_sample()
    {
        var actual = JpegLosslessPredictor.PredictSample(
            selectionValue: 7,
            samplePrecision: 16,
            x: 1,
            y: 0,
            left: 1000,
            above: 0,
            upperLeft: 0);

        Assert.Equal(1000, actual);
    }

    [Fact]
    public void Predictor_uses_above_sample_for_first_column_after_first_row()
    {
        var actual = JpegLosslessPredictor.PredictSample(
            selectionValue: 7,
            samplePrecision: 12,
            x: 0,
            y: 1,
            left: 0,
            above: 2000,
            upperLeft: 0);

        Assert.Equal(2000, actual);
    }

    [Fact]
    public void Predictor_rejects_unsupported_selection_value()
    {
        var exception = Assert.Throws<DicomCodecException>(
            () => JpegLosslessPredictor.Predict(selectionValue: 8, left: 1, above: 2, upperLeft: 3));

        Assert.Contains("JPEG", exception.Message);
        Assert.Contains("predictor", exception.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
