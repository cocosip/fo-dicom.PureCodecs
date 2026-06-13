using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs;
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
    public void Render_matrix_renders_jpeg2000_acceptance_samples_where_dependencies_are_available()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();
        var catalog = ExternalFixtureCatalog.Resolve();
        var jpeg2000Fixtures = catalog.RenderFixtures
            .Where(fixture =>
                fixture.ExpectedTransferSyntax == DicomTransferSyntax.JPEG2000Lossless ||
                fixture.ExpectedTransferSyntax == DicomTransferSyntax.JPEG2000Lossy)
            .ToArray();

        Assert.NotEmpty(jpeg2000Fixtures);
        Assert.All(jpeg2000Fixtures, fixture =>
        {
            var rendered = new DicomImage(fixture.Path).RenderImage();
            Assert.NotNull(rendered);
        });
    }
}
