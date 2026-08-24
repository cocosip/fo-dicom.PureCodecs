using System.Diagnostics;
using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Htj2kReference;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kReferenceAlignmentTests
{
    [Theory]
    [InlineData("201", false)]
    [InlineData("202", false)]
    [InlineData("203", false)]
    [InlineData("203", true)]
    public async Task Htj2k_default_codestream_matches_fo_dicom_codecs_reference(string syntax, bool monochrome)
    {
        var directory = Path.Combine(Path.GetTempPath(), "purecodecs-htj2k-alignment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var sourceDataset = monochrome
                // The reference codec API exposes its compressed buffer directly. For
                // high-entropy lossy frames it can fill that buffer before EOC.
                ? DicomPixelDataFixtures.CreateMonochrome8(
                    rows: 32,
                    columns: 32,
                    frame: Enumerable.Range(0, 32 * 32)
                        .Select(index => index == (16 * 32) + 16 ? (byte)129 : (byte)128)
                        .ToArray())
                : DicomPixelDataFixtures.CreateRgbInterleaved(rows: 32, columns: 32);
            var sourcePath = Path.Combine(directory, "source.dcm");
            var manifestPath = Path.Combine(directory, "reference.json");
            new DicomFile(sourceDataset).Save(sourcePath);

            var worker = await RunReferenceWorkerAsync(sourcePath, syntax, manifestPath);
            Assert.True(worker.ExitCode == 0, worker.Output);
            var expected = JsonSerializer.Deserialize<Htj2kReferenceManifest>(File.ReadAllText(manifestPath));
            Assert.NotNull(expected);
            var expectedBytes = new[] { File.ReadAllBytes(Path.Combine(directory, "0.j2c")) };

            var source = DicomPixelData.Create(sourceDataset);
            var codec = CreatePureCodec(syntax);
            var pureDataset = CloneForTransferSyntax(sourceDataset, codec.TransferSyntax);
            var pure = DicomPixelData.Create(pureDataset, true);
            codec.Encode(source, pure, codec.GetDefaultParameters());
            var actualBytes = Htj2kReferenceManifestBuilder.ExtractLogicalCodestream(pure.GetFrame(0).Data);
            var actual = expected with
            {
                Frames = new[]
                {
                    expected.Frames[0] with
                    {
                        CodestreamSha256 = Htj2kReferenceManifestBuilder.ComputeSha256(actualBytes),
                        LogicalCodestreamLength = actualBytes.Length
                    }
                }
            };

            Assert.Equal(HeaderThroughSod(expectedBytes[0]), HeaderThroughSod(actualBytes));
            var diff = Htj2kReferenceDiffComparer.Compare(expected, expectedBytes, actual, new[] { actualBytes });

            Assert.True(diff.IsMatch, diff.Summary + " " + Describe(diff.FirstDifference));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunReferenceWorkerAsync(string sourcePath, string syntax, string manifestPath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var workerAssembly = Path.Combine(root, "tools", "fo-dicom.PureCodecs.Htj2kReference", "bin", "Debug", "net10.0", "fo-dicom.PureCodecs.Htj2kReference.dll");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(workerAssembly);
        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("--syntax");
        startInfo.ArgumentList.Add(syntax);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(manifestPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the HTJ2K reference worker.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("HTJ2K reference worker exceeded 120 seconds.");
        }

        return (process.ExitCode, (await outputTask) + (await errorTask));
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

    private static IDicomCodec CreatePureCodec(string syntax)
    {
        return syntax switch
        {
            "201" => new DicomHtJpeg2000LosslessCodec(),
            "202" => new DicomHtJpeg2000LosslessRpclCodec(),
            "203" => new DicomHtJpeg2000LossyCodec(),
            _ => throw new ArgumentOutOfRangeException(nameof(syntax))
        };
    }

    private static string Describe(Htj2kReferenceByteDifference? difference)
    {
        return difference is null
            ? string.Empty
            : $"frame={difference.FrameIndex}, offset={difference.ByteOffset}, expected={difference.ExpectedByte}, actual={difference.ActualByte}";
    }

    private static byte[] HeaderThroughSod(byte[] codestream)
    {
        for (var index = 0; index < codestream.Length - 1; index++)
        {
            if (codestream[index] == 0xFF && codestream[index + 1] == 0x93)
            {
                return codestream[..(index + 2)];
            }
        }

        throw new Xunit.Sdk.XunitException("HTJ2K SOD marker not found.");
    }
}
