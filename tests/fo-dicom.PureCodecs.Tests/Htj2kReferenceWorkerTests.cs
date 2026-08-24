using System.Diagnostics;
using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.PureCodecs.Htj2kReference;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kReferenceWorkerTests
{
    [Fact]
    public async Task Reference_worker_writes_versioned_lossless_manifest_for_each_frame()
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-htj2k-reference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.dcm");
            var manifestPath = Path.Combine(directory, "reference.json");
            new DicomFile(DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32)).Save(sourcePath);

            var exitCode = RunWorker(sourcePath, "201", manifestPath);

            Assert.True(exitCode == 0, "Reference worker returned a non-zero exit code.");
            var manifest = JsonSerializer.Deserialize<Htj2kReferenceManifest>(File.ReadAllText(manifestPath));
            Assert.NotNull(manifest);
            Assert.Equal("5.16.7", manifest.ReferencePackageVersion);
            Assert.Equal("1d05c6cca14883d06b835f8dadca5dae7d97577c", manifest.ReferenceReleaseCommit);
            Assert.Equal("0.21.2", manifest.CodestreamReportedOpenJphVersion);
            Assert.Equal(DicomTransferSyntax.HTJ2KLossless.UID.UID, manifest.TransferSyntaxUid);
            Assert.Single(manifest.Frames);
            Assert.All(manifest.Frames, frame =>
            {
                Assert.Equal(64, frame.RawFrameSha256.Length);
                Assert.Equal(64, frame.CodestreamSha256.Length);
                Assert.Equal(64, frame.DecodedFrameSha256.Length);
                Assert.True(frame.LogicalCodestreamLength > 2);
                Assert.Contains("FFD9", frame.MarkerSummary.MarkerCodes);
                Assert.True(File.Exists(Path.Combine(directory, frame.FrameIndex + ".j2c")));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static int RunWorker(string sourcePath, string syntax, string manifestPath)
    {
        return Htj2kReferenceWorkerProgram.Run(new[]
        {
            "--worker",
            "--input", sourcePath,
            "--syntax", syntax,
            "--output", manifestPath
        });
    }
}
