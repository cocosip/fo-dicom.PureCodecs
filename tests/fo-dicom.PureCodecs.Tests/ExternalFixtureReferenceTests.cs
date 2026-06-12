using FellowOakDicom;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class ExternalFixtureReferenceTests
{
    [Fact]
    public void Efferent_unit_fixture_root_is_referenced_when_available()
    {
        var catalog = ExternalFixtureCatalog.Resolve();

        Assert.False(string.IsNullOrWhiteSpace(catalog.CodecsRoot));
        Assert.All(catalog.UnitFixtures, fixture => Assert.True(File.Exists(fixture.Path), fixture.Path));
    }

    [Fact]
    public void Efferent_acceptance_fixture_root_is_referenced_when_available()
    {
        var catalog = ExternalFixtureCatalog.Resolve();

        Assert.False(string.IsNullOrWhiteSpace(catalog.CodecsRoot));
        Assert.All(catalog.AcceptanceFixtures, fixture => Assert.True(File.Exists(fixture.Path), fixture.Path));
    }

    [Fact]
    public void Referenced_efferent_unit_fixtures_can_be_opened()
    {
        var catalog = ExternalFixtureCatalog.Resolve();

        Assert.NotEmpty(catalog.UnitFixtures);
        foreach (var fixture in catalog.UnitFixtures)
        {
            var file = DicomFile.Open(fixture.Path);

            Assert.True(file.Dataset.Contains(DicomTag.PixelData), fixture.Name);
            Assert.Same(fixture.ExpectedTransferSyntax, file.Dataset.InternalTransferSyntax);
        }
    }

    [Fact]
    public void Referenced_acceptance_fixtures_can_be_opened()
    {
        var catalog = ExternalFixtureCatalog.Resolve();

        Assert.NotEmpty(catalog.AcceptanceFixtures);
        foreach (var fixture in catalog.AcceptanceFixtures)
        {
            var file = DicomFile.Open(fixture.Path);

            Assert.True(file.Dataset.Contains(DicomTag.PixelData), fixture.Name);
            Assert.Same(fixture.ExpectedTransferSyntax, file.Dataset.InternalTransferSyntax);
        }
    }
}
