using System.Diagnostics;
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
    public void Validation_tool_uses_complete_dataset_decode_and_writes_the_decoded_dicom()
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
            Assert.Contains("VALIDATION|passed|maxDiff=0", result.Output);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "decoded.dcm")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "source.bmp")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "decoded.bmp")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validation_tool_rejects_lossless_source_with_different_dimensions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-htj2k-validation-shape-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.dcm");
            var compressedPath = Path.Combine(directory, "compressed.dcm");
            var outputDirectory = Path.Combine(directory, "validation");
            var fullDataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 64, columns: 64);
            var fullFrame = DicomPixelData.Create(fullDataset).GetFrame(0).Data;
            var shortDataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 16, columns: 64, frame: fullFrame[..(16 * 64)]);
            new DicomFile(shortDataset).Save(sourcePath);
            var compressedDataset = CloneForTransferSyntax(fullDataset, DicomTransferSyntax.HTJ2KLossless);
            new DicomHtJpeg2000LosslessCodec().Encode(
                DicomPixelData.Create(fullDataset),
                DicomPixelData.Create(compressedDataset, true),
                new DicomHtJpeg2000Params());
            new DicomFile(compressedDataset).Save(compressedPath);

            var result = RunValidationTool(sourcePath, compressedPath, outputDirectory);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("dimensions", result.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validation_tool_requires_explicit_tolerance_for_lossy_syntax()
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-htj2k-validation-lossy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "source.dcm");
            var compressedPath = Path.Combine(directory, "compressed.dcm");
            var outputDirectory = Path.Combine(directory, "validation");
            var sourceDataset = DicomPixelDataFixtures.CreateMonochrome8(rows: 32, columns: 32);
            new DicomFile(sourceDataset).Save(sourcePath);
            var compressedDataset = CloneForTransferSyntax(sourceDataset, DicomTransferSyntax.HTJ2K);
            new DicomHtJpeg2000LossyCodec().Encode(
                DicomPixelData.Create(sourceDataset),
                DicomPixelData.Create(compressedDataset, true),
                new DicomHtJpeg2000Params { TargetRatio = 3 });
            new DicomFile(compressedDataset).Save(compressedPath);

            var result = RunValidationTool(sourcePath, compressedPath, outputDirectory);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("lossy-tolerance", result.Output, StringComparison.OrdinalIgnoreCase);
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
