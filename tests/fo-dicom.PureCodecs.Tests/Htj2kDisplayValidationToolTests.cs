using System.Diagnostics;
using System.Text.RegularExpressions;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using FellowOakDicom.IO.Buffer;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kDisplayValidationToolTests
{
    [Fact]
    public void Validation_tool_reports_codestream_native_decode_and_render_fingerprint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-htj2k-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.dcm");
            var compressedPath = Path.Combine(directory, "source_htj2k_lossless.dcm");
            var outputDirectory = Path.Combine(directory, "validation");
            var sourceDataset = DicomPixelDataFixtures.CreateMonochrome16(rows: 64, columns: 64);
            new DicomFile(sourceDataset).Save(sourcePath);
            var compressedDataset = CloneForTransferSyntax(sourceDataset, DicomTransferSyntax.HTJ2KLossless);
            var source = DicomPixelData.Create(sourceDataset);
            var compressed = DicomPixelData.Create(compressedDataset, true);
            new DicomHtJpeg2000LosslessCodec().Encode(source, compressed, new DicomHtJpeg2000Params());
            new DicomFile(compressedDataset).Save(compressedPath);

            var result = RunValidationTool(sourcePath, compressedPath, outputDirectory);

            Assert.True(result.ExitCode == 0, result.Output);
            Assert.Contains("STRUCTURE|ok", result.Output);
            Assert.Contains("NATIVE-DECODE|ok|maxDiff=0", result.Output);
            Assert.Contains("RENDER|ok", result.Output);
            Assert.Matches(new Regex("sourceHash=[0-9A-F]{64}"), result.Output);
            Assert.Matches(new Regex("decodedHash=[0-9A-F]{64}"), result.Output);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "source.bmp")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "decoded.bmp")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (int ExitCode, string Output) RunValidationTool(string sourcePath, string compressedPath, string outputDirectory)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "fo-dicom.PureCodecs.Htj2kValidation", "fo-dicom.PureCodecs.Htj2kValidation.csproj"));
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(compressedPath);
        startInfo.ArgumentList.Add(outputDirectory);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start validation tool.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);
        return (process.ExitCode, output);
    }

    private static DicomDataset CloneForTransferSyntax(DicomDataset source, DicomTransferSyntax transferSyntax)
    {
        var clone = new DicomDataset(transferSyntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }
}
