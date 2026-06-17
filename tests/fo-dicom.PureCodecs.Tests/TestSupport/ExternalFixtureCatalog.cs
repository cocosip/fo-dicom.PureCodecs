using FellowOakDicom;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal sealed class ExternalFixtureCatalog
{
    private ExternalFixtureCatalog(
        string codecsRoot,
        string? foDicomRoot,
        IReadOnlyList<ExternalFixture> unitFixtures,
        IReadOnlyList<ExternalFixture> acceptanceFixtures,
        IReadOnlyList<ExternalFixture> renderFixtures)
    {
        CodecsRoot = codecsRoot;
        FoDicomRoot = foDicomRoot;
        UnitFixtures = unitFixtures;
        AcceptanceFixtures = acceptanceFixtures;
        RenderFixtures = renderFixtures;
    }

    public string CodecsRoot { get; }

    public string? FoDicomRoot { get; }

    public IReadOnlyList<ExternalFixture> UnitFixtures { get; }

    public IReadOnlyList<ExternalFixture> AcceptanceFixtures { get; }

    public IReadOnlyList<ExternalFixture> RenderFixtures { get; }

    public static ExternalFixtureCatalog Resolve()
    {
        var codecsRoot = ResolveRoot(
            "FO_DICOM_CODECS_SOURCE_ROOT",
            Path.Combine(AppContext.BaseDirectory, "TestSupport", "Fixtures", "fo-dicom.Codecs"));
        var foDicomRoot = ResolveRoot(
            "FO_DICOM_SOURCE_ROOT",
            Path.Combine(AppContext.BaseDirectory, "TestSupport", "Fixtures", "fo-dicom"));

        if (codecsRoot is null)
        {
            return new ExternalFixtureCatalog(
                codecsRoot: string.Empty,
                foDicomRoot: foDicomRoot,
                unitFixtures: Array.Empty<ExternalFixture>(),
                acceptanceFixtures: Array.Empty<ExternalFixture>(),
                renderFixtures: Array.Empty<ExternalFixture>());
        }

        var unitDirectory = Path.Combine(codecsRoot, "Tests", "Unit");
        var acceptanceDirectory = Path.Combine(codecsRoot, "Tests", "Acceptance");

        var unitFixtures = new[]
        {
            new ExternalFixture("Efferent unit 8-bit sample", Path.Combine(unitDirectory, "test8bits.dcm"), DicomTransferSyntax.ExplicitVRLittleEndian),
            new ExternalFixture("Efferent unit 16-bit sample", Path.Combine(unitDirectory, "test16bits.dcm"), DicomTransferSyntax.ExplicitVRLittleEndian),
        };

        var acceptanceFixtures = new[]
        {
            new ExternalFixture("RGB raw acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_RGB.dcm"), DicomTransferSyntax.ExplicitVRLittleEndian),
            new ExternalFixture("JPEG baseline YBR 4:2:2 acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG-Baseline_YBR422.dcm"), DicomTransferSyntax.JPEGProcess1),
            new ExternalFixture("JPEG baseline YBR full acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG-Baseline_YBRFull.dcm"), DicomTransferSyntax.JPEGProcess1),
            new ExternalFixture("JPEG lossless RGB acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG-Lossless_RGB.dcm"), DicomTransferSyntax.JPEGProcess14SV1),
            new ExternalFixture("RLE lossless acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_RLE-Lossless.dcm"), DicomTransferSyntax.RLELossless),
            new ExternalFixture("JPEG-LS lossless acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG-LS_Lossless.dcm"), DicomTransferSyntax.JPEGLSLossless),
            new ExternalFixture("JPEG-LS near-lossless acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG-LS_NearLossless.dcm"), DicomTransferSyntax.JPEGLSNearLossless),
            new ExternalFixture("JPEG 2000 lossless acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG2000-Lossless.dcm"), DicomTransferSyntax.JPEG2000Lossless),
            new ExternalFixture("JPEG 2000 lossy acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG2000-Lossy.dcm"), DicomTransferSyntax.JPEG2000Lossy),
            new ExternalFixture("JPEG 2000 lossy quality-50 acceptance sample", Path.Combine(acceptanceDirectory, "PM5644-960x540_JPEG2000-Lossy50.dcm"), DicomTransferSyntax.JPEG2000Lossy),
        };

        var renderFixtures = acceptanceFixtures
            .Concat(foDicomRenderFixtures(foDicomRoot))
            .ToArray();

        return new ExternalFixtureCatalog(codecsRoot, foDicomRoot, unitFixtures, acceptanceFixtures, renderFixtures);
    }

    private static IEnumerable<ExternalFixture> foDicomRenderFixtures(string? foDicomRoot)
    {
        if (foDicomRoot is null)
        {
            yield break;
        }

        var testData = Path.Combine(foDicomRoot, "Tests", "FO-DICOM.Tests", "Test Data");
        yield return new ExternalFixture("fo-dicom JPEG Process 1 regression sample", Path.Combine(testData, "GH538-jpeg1.dcm"), DicomTransferSyntax.JPEGProcess1);
        yield return new ExternalFixture("fo-dicom JPEG Process 14 SV1 regression sample", Path.Combine(testData, "GH538-jpeg14sv1.dcm"), DicomTransferSyntax.JPEGProcess14SV1);
        yield return new ExternalFixture("fo-dicom JPEG with icon sample", Path.Combine(testData, "JPEGwithIcon.dcm"), DicomTransferSyntax.JPEGProcess1);
    }

    private static string? ResolveRoot(string environmentVariable, params string[] fallbacks)
    {
        var configured = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        foreach (var fallback in fallbacks)
        {
            if (Directory.Exists(fallback))
            {
                return Path.GetFullPath(fallback);
            }
        }

        return null;
    }
}
