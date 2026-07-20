using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.NativeCodecs.Tools;

public sealed class CompressionPlan
{
    private CompressionPlan(string inputPath, string outputDirectory, IReadOnlyList<CompressionPlanItem> items)
    {
        InputPath = inputPath;
        OutputDirectory = outputDirectory;
        Items = items;
    }

    public string InputPath { get; }

    public string OutputDirectory { get; }

    public IReadOnlyList<CompressionPlanItem> Items { get; }

    public static CompressionPlan Create(string inputPath, string? outputDirectory, DicomDataset dataset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentNullException.ThrowIfNull(dataset);

        var resolvedOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? GetDefaultOutputDirectory(inputPath)
            : outputDirectory;
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var items = CompressionTargetFormats.All
            .Select(format => CompressionPlanItem.Transcode(format, Path.Combine(resolvedOutputDirectory, $"{baseName}_{format.Suffix}.dcm")))
            .ToArray();

        return new CompressionPlan(inputPath, resolvedOutputDirectory, items);
    }

    private static string GetDefaultOutputDirectory(string inputPath)
    {
        var inputDirectory = Path.GetDirectoryName(inputPath);
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        return string.IsNullOrEmpty(inputDirectory)
            ? $"{baseName}_compressed"
            : Path.Combine(inputDirectory, $"{baseName}_compressed");
    }
}
