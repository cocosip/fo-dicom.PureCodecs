using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class CompressedRenderMatrixSkeletonTests
{
    [Fact]
    public void Render_matrix_references_compressed_fixtures_where_rendering_dependencies_are_available()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var compressedFixtures = catalog.RenderFixtures
            .Where(fixture => fixture.ExpectedTransferSyntax != DicomTransferSyntax.ExplicitVRLittleEndian)
            .ToArray();

        Assert.NotEmpty(compressedFixtures);
        Assert.All(compressedFixtures, fixture => Assert.True(File.Exists(fixture.Path), fixture.Path));
    }

    [Fact]
    public void Render_matrix_currently_fails_at_stub_codec_boundary_for_compressed_jpeg_fixtures()
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var jpegFixture = catalog.RenderFixtures.First(fixture =>
            fixture.ExpectedTransferSyntax == DicomTransferSyntax.JPEGProcess1 ||
            fixture.ExpectedTransferSyntax == DicomTransferSyntax.JPEGProcess14SV1);

        var exception = Record.Exception(() => new DicomImage(jpegFixture.Path).RenderImage());

        Assert.NotNull(exception);
    }
}
