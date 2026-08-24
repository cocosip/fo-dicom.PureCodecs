using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
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

            var result = RunWorker(sourcePath, "201", manifestPath);

            Assert.False(result.TimedOut);
            Assert.True(result.ExitCode == 0, result.StandardError);
            var manifest = JsonSerializer.Deserialize<Htj2kReferenceManifest>(File.ReadAllText(manifestPath));
            Assert.NotNull(manifest);
            Assert.Equal("5.16.7", manifest.ReferencePackageVersion);
            Assert.Equal("1d05c6cca14883d06b835f8dadca5dae7d97577c", manifest.ReferenceReleaseCommit);
            Assert.Equal("0.21.2", manifest.CodestreamReportedOpenJphVersion);
            Assert.Equal(DicomTransferSyntax.HTJ2KLossless.UID.UID, manifest.TransferSyntaxUid);
            Assert.Equal(1, manifest.FrameCount);
            Assert.Equal(new Htj2kReferenceParameters("RPCL", true, true, 8), manifest.EffectiveParameters);
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

    [Fact]
    public void Reference_provenance_rejects_an_unexpected_loaded_package_version()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Htj2kReferenceProvenanceReader.ReadAndValidate(
                CreateAssemblyWithInformationalVersion(
                    "1.0.0+1d05c6cca14883d06b835f8dadca5dae7d97577c"),
                expectedPackageVersion: "0.0.0",
                expectedReleaseCommit: "1d05c6cca14883d06b835f8dadca5dae7d97577c"));

        Assert.Contains("package version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reference_worker_reports_invalid_arguments_from_the_child_process()
    {
        var result = BoundedWorkerProcess.Run(
            ReferenceWorkerAssemblyPath,
            new[] { "--worker", "--syntax", "201" });

        Assert.False(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("HTJ2K_REFERENCE|fail", result.StandardError);
    }

    [Fact]
    public void Reference_worker_timeout_terminates_the_child_process_tree()
    {
        var result = BoundedWorkerProcess.Run(
            ReferenceWorkerAssemblyPath,
            new[] { "--worker", "--delay-ms", "5000" },
            TimeSpan.FromMilliseconds(100));

        Assert.True(result.TimedOut);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public void Reference_worker_does_not_change_native_assembly_load_state_in_the_test_process()
    {
        var loadedBefore = IsNativeCodecsAssemblyLoaded();

        var result = BoundedWorkerProcess.Run(
            ReferenceWorkerAssemblyPath,
            new[] { "--worker", "--syntax", "201" });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(loadedBefore, IsNativeCodecsAssemblyLoaded());
    }

    [Fact]
    public void Native_worker_transcodes_htj2k_lossless_in_bounded_child_processes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-native-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.dcm");
            var compressedPath = Path.Combine(directory, "compressed.dcm");
            var decodedPath = Path.Combine(directory, "decoded.dcm");
            var sourceDataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32);
            new DicomFile(sourceDataset).Save(sourcePath);

            var encode = RunNativeWorker(sourcePath, compressedPath, "201");
            Assert.False(encode.TimedOut);
            Assert.True(encode.ExitCode == 0, encode.StandardError);
            var compressed = DicomFile.Open(compressedPath, FileReadOption.ReadAll);
            Assert.Equal(DicomTransferSyntax.HTJ2KLossless, compressed.Dataset.InternalTransferSyntax);
            Assert.Equal(1, DicomPixelData.Create(compressed.Dataset).NumberOfFrames);

            var decode = RunNativeWorker(compressedPath, decodedPath, "raw");
            Assert.False(decode.TimedOut);
            Assert.True(decode.ExitCode == 0, decode.StandardError);
            var decoded = DicomFile.Open(decodedPath, FileReadOption.ReadAll);
            PixelDataAssertions.FramesMatchExactly(
                DicomPixelData.Create(sourceDataset),
                DicomPixelData.Create(decoded.Dataset));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Native_worker_rejects_an_invalid_target_syntax()
    {
        var result = BoundedWorkerProcess.Run(
            NativeWorkerAssemblyPath,
            new[] { "--worker", "--input", "missing.dcm", "--output", "output.dcm", "--syntax", "invalid" });

        Assert.False(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NATIVE_HTJ2K|fail", result.StandardError);
    }

    private static BoundedWorkerResult RunWorker(string sourcePath, string syntax, string manifestPath)
    {
        return BoundedWorkerProcess.Run(ReferenceWorkerAssemblyPath, new[]
        {
            "--worker",
            "--input", sourcePath,
            "--syntax", syntax,
            "--output", manifestPath
        });
    }

    private static string ReferenceWorkerAssemblyPath => typeof(Htj2kReferenceWorkerProgram).Assembly.Location;

    private static string NativeWorkerAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "fo-dicom.NativeCodecs.Tools.dll");

    private static BoundedWorkerResult RunNativeWorker(string inputPath, string outputPath, string syntax)
    {
        return BoundedWorkerProcess.Run(NativeWorkerAssemblyPath, new[]
        {
            "--worker",
            "--input", inputPath,
            "--output", outputPath,
            "--syntax", syntax
        });
    }

    private static Assembly CreateAssemblyWithInformationalVersion(string informationalVersion)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("HtJ2kReferenceProvenanceTest" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, new object[] { informationalVersion }));
        return assembly;
    }

    private static bool IsNativeCodecsAssemblyLoaded()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(
            assembly => string.Equals(assembly.GetName().Name, "fo-dicom.Codecs", StringComparison.Ordinal));
    }
}
