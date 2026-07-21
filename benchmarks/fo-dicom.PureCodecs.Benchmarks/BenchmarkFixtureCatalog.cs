using FellowOakDicom;

namespace FellowOakDicom.PureCodecs.Benchmarks;

public sealed record BenchmarkFixture(string Name, string Path, DicomTransferSyntax TransferSyntax)
{
    public override string ToString()
    {
        return Name;
    }
}

public static class BenchmarkFixtureCatalog
{
    public static IReadOnlyList<BenchmarkFixture> Create(string acceptanceRoot)
    {
        if (string.IsNullOrWhiteSpace(acceptanceRoot))
        {
            throw new ArgumentException("Fixture root is required.", nameof(acceptanceRoot));
        }

        return new[]
        {
            CreateFixture("RLE lossless", "PM5644-960x540_RLE-Lossless.dcm", DicomTransferSyntax.RLELossless),
            CreateFixture("JPEG baseline", "PM5644-960x540_JPEG-Baseline_YBR422.dcm", DicomTransferSyntax.JPEGProcess1),
            CreateFixture("JPEG lossless SV1", "PM5644-960x540_JPEG-Lossless_RGB.dcm", DicomTransferSyntax.JPEGProcess14SV1),
            CreateFixture("JPEG-LS lossless", "PM5644-960x540_JPEG-LS_Lossless.dcm", DicomTransferSyntax.JPEGLSLossless),
            CreateFixture("JPEG 2000 lossless", "PM5644-960x540_JPEG2000-Lossless.dcm", DicomTransferSyntax.JPEG2000Lossless),
            CreateFixture("JPEG 2000 lossy", "PM5644-960x540_JPEG2000-Lossy.dcm", DicomTransferSyntax.JPEG2000Lossy),
        };

        BenchmarkFixture CreateFixture(string name, string fileName, DicomTransferSyntax transferSyntax)
        {
            return new BenchmarkFixture(name, Path.Combine(acceptanceRoot, fileName), transferSyntax);
        }
    }
}
