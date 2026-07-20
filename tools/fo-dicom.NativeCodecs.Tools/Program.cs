using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.NativeCodecs.Tools;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var options = ToolOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            PrintUsage();
            return 1;
        }

        var inputPath = TrimShellQuotes(options.InputPath);
        Console.WriteLine("DICOM automatic compression tool");
        Console.WriteLine("Input: " + inputPath);

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        PrintImageInfo(file.Dataset);

        var targetFormat = ResolveTargetFormat(options.Format);
        var results = new DicomCompressionTool().Compress(inputPath, options.OutputDirectory, targetFormat);
        var outputDirectory = results.Count > 0
            ? Path.GetDirectoryName(results[0].Item.OutputPath)
            : options.OutputDirectory;
        Console.WriteLine("Output directory: " + outputDirectory);
        Console.WriteLine();

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            Console.WriteLine($"[{index + 1}/{results.Count}] {result.Item.Format.Name}");
            Console.WriteLine("    Transfer Syntax: " + result.Item.Format.TransferSyntax.UID.UID);
            switch (result.Status)
            {
                case CompressionResultStatus.Success:
                    Console.WriteLine("    Success: " + Path.GetFileName(result.Item.OutputPath));
                    Console.WriteLine("    Size: " + FormatBytes(result.OutputSize.GetValueOrDefault()) + " (" + result.Message + ")");
                    break;
                case CompressionResultStatus.Unsupported:
                    Console.WriteLine("    Unsupported: " + result.Message);
                    break;
                case CompressionResultStatus.Skipped:
                    Console.WriteLine("    Skipped: " + result.Message);
                    break;
                case CompressionResultStatus.Failed:
                    Console.WriteLine("    Failed: " + result.Message);
                    break;
            }
        }

        var successCount = results.Count(result => result.Status == CompressionResultStatus.Success);
        var unsupportedCount = results.Count(result => result.Status == CompressionResultStatus.Unsupported);
        var skippedCount = results.Count(result => result.Status == CompressionResultStatus.Skipped);
        var failedCount = results.Count(result => result.Status == CompressionResultStatus.Failed);
        Console.WriteLine();
        Console.WriteLine($"Summary: {successCount} succeeded, {unsupportedCount} unsupported, {skippedCount} skipped, {failedCount} failed.");

        return failedCount == results.Count ? 1 : 0;
    }

    private static void PrintImageInfo(DicomDataset dataset)
    {
        var pixelData = DicomPixelData.Create(dataset);
        Console.WriteLine("Image:");
        Console.WriteLine($"    Rows: {pixelData.Height}");
        Console.WriteLine($"    Columns: {pixelData.Width}");
        Console.WriteLine($"    Bits Stored: {pixelData.BitsStored}");
        Console.WriteLine($"    Samples Per Pixel: {pixelData.SamplesPerPixel}");
        Console.WriteLine($"    Photometric Interpretation: {dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, string.Empty)}");
        Console.WriteLine($"    Source Transfer Syntax: {pixelData.Syntax.UID.UID}");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{bytes} B" : $"{value:0.0} {units[unitIndex]}";
    }

    private static string TrimShellQuotes(string value)
    {
        return value.Trim().Trim('"', '\'');
    }

    private static CompressionTargetFormat? ResolveTargetFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        var targetFormat = CompressionTargetFormats.All.SingleOrDefault(item =>
            string.Equals(item.Suffix, format, StringComparison.OrdinalIgnoreCase));
        return targetFormat ?? throw new ArgumentException($"Unknown format {format}.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run <input-file> [--output-dir <directory>] [--format <format>]");
        Console.WriteLine("  fo-dicom.NativeCodecs.Tools.exe <input-file> [--output-dir <directory>] [--format <format>]");
    }
}
