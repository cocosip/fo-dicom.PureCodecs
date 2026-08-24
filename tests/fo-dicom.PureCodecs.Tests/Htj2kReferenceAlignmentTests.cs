using System.Diagnostics;
using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Htj2kReference;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
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

            var expectedHeader = HeaderThroughSod(expectedBytes[0]);
            var actualHeader = HeaderThroughSod(actualBytes);
            Assert.True(
                expectedHeader.SequenceEqual(actualHeader),
                $"HTJ2K headers differ. ExpectedLength={expectedHeader.Length}, ActualLength={actualHeader.Length}, " +
                DescribeFirstDifference(expectedHeader, actualHeader) + ", " +
                $"expectedMarkers={DescribeMarkers(expectedHeader)}, actualMarkers={DescribeMarkers(actualHeader)}");
            var diff = Htj2kReferenceDiffComparer.Compare(expected, expectedBytes, actual, new[] { actualBytes });
            var expectedDecoded = DecodeFrame(sourceDataset, expectedBytes[0]);
            var actualDecoded = DecodeFrame(sourceDataset, actualBytes);

            Assert.True(
                diff.IsMatch,
                diff.Summary + " " + Describe(diff.FirstDifference) + ", " +
                $"ExpectedLength={expectedBytes[0].Length}, ActualLength={actualBytes.Length}, " +
                DescribeFirstDifference(expectedBytes[0], actualBytes) + ", " +
                DescribeDifferenceCount(expectedBytes[0], actualBytes) + ", " +
                DescribeTilePart(expectedBytes[0], diff.FirstDifference?.ByteOffset ?? -1) + ", " +
                "Decoded" + DescribeDifferenceCount(expectedDecoded, actualDecoded));
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

    private static byte[] DecodeFrame(DicomDataset sourceDataset, byte[] codestream)
    {
        var targetDataset = CloneForTransferSyntax(sourceDataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var target = DicomPixelData.Create(targetDataset, true);
        return new Jpeg2000HtFrameCodec().DecodeFrame(target, codestream);
    }

    private static string Describe(Htj2kReferenceByteDifference? difference)
    {
        return difference is null
            ? string.Empty
            : $"frame={difference.FrameIndex}, offset={difference.ByteOffset}, expected={difference.ExpectedByte}, actual={difference.ActualByte}";
    }

    private static string DescribeFirstDifference(byte[] expected, byte[] actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < length; index++)
        {
            if (expected[index] != actual[index])
            {
                var start = Math.Max(0, index - 8);
                var count = Math.Min(length - start, 17);
                return $"offset={index}, expected={expected[index]}, actual={actual[index]}, " +
                    $"expectedContext={Convert.ToHexString(expected, start, count)}, " +
                    $"actualContext={Convert.ToHexString(actual, start, count)}";
            }
        }

        return expected.Length == actual.Length
            ? "no byte difference"
            : $"length differs after offset {length}";
    }

    private static string DescribeMarkers(byte[] codestream)
    {
        var markers = new List<string>();
        for (var offset = 0; offset < codestream.Length - 1;)
        {
            if (codestream[offset] != 0xFF)
            {
                return string.Join(";", markers) + $";data@{offset}";
            }

            var code = codestream[offset + 1];
            if (code is 0x4F or 0x93)
            {
                markers.Add($"FF{code:X2}@{offset}:2");
                offset += 2;
                continue;
            }

            var length = (codestream[offset + 2] << 8) | codestream[offset + 3];
            markers.Add($"FF{code:X2}@{offset}:{length + 2}");
            offset += length + 2;
        }

        return string.Join(";", markers);
    }

    private static string DescribeDifferenceCount(byte[] expected, byte[] actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        var count = Math.Abs(expected.Length - actual.Length);
        var last = -1;
        for (var index = 0; index < length; index++)
        {
            if (expected[index] != actual[index])
            {
                count++;
                last = index;
            }
        }

        return $"DifferentBytes={count}, LastDifference={last}";
    }

    private static string DescribeTilePart(byte[] codestream, int byteOffset)
    {
        var tilePart = 0;
        for (var offset = 0; offset < codestream.Length - 13; offset++)
        {
            if (codestream[offset] != 0xFF || codestream[offset + 1] != 0x90)
            {
                continue;
            }

            var length = (int)(((uint)codestream[offset + 6] << 24)
                | ((uint)codestream[offset + 7] << 16)
                | ((uint)codestream[offset + 8] << 8)
                | codestream[offset + 9]);
            if (byteOffset >= offset && byteOffset < offset + length)
            {
                return $"TilePart={tilePart}, TileStart={offset}, TileLength={length}, TileRelativeOffset={byteOffset - offset}";
            }

            tilePart++;
            offset += Math.Max(1, length) - 1;
        }

        return "TilePart=not-found";
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
