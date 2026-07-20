using FellowOakDicom;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class RegressionFixturePrivacyTests
{
    [Fact]
    public void Local_real_regression_fixture_is_deidentified()
    {
        var file = DicomFile.Open(RegressionFixturePaths.LocalReal1, FileReadOption.ReadAll);

        Assert.Equal("PURECODECS^REG", file.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty));
        Assert.Equal("PURECODECS-001", file.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty));
        Assert.Equal("PURECODECS", file.Dataset.GetSingleValueOrDefault(DicomTag.InstitutionName, string.Empty));
        Assert.Equal("20000101", file.Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty));
        Assert.DoesNotContain(file.Dataset, item => item.Tag.IsPrivate);
        Assert.True(file.Dataset.Contains(DicomTag.PixelData));
    }

    [Theory]
    [InlineData("1_jpeg_lossless.dcm")]
    [InlineData("1_jpeg_lossless_sv1.dcm")]
    [InlineData("1_jpegls_lossless.dcm")]
    [InlineData("1_jpegls_near_lossless.dcm")]
    [InlineData("1_j2k_lossy.dcm")]
    public void Transcoded_regression_fixtures_are_derived_from_deidentified_source(string fileName)
    {
        var file = DicomFile.Open(RegressionFixturePaths.Transcoded(fileName), FileReadOption.ReadAll);

        Assert.Equal("PURECODECS^REG", file.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty));
        Assert.Equal("PURECODECS-001", file.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty));
        Assert.DoesNotContain(file.Dataset, item => item.Tag.IsPrivate);
        Assert.True(file.Dataset.Contains(DicomTag.PixelData));
    }

    private static void AssertNoPrivateTags(DicomDataset dataset)
    {
        Assert.DoesNotContain(dataset, item => item.Tag.IsPrivate);

        foreach (var sequence in dataset.OfType<DicomSequence>())
        {
            foreach (var item in sequence)
            {
                AssertNoPrivateTags(item);
            }
        }
    }
}
