using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Tools;

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
        var pixelData = DicomPixelData.Create(dataset);
        var items = CompressionTargetFormats.All
            .Select(format => CreateItem(format, pixelData, resolvedOutputDirectory, baseName))
            .ToArray();

        return new CompressionPlan(inputPath, resolvedOutputDirectory, items);
    }

    private static CompressionPlanItem CreateItem(
        CompressionTargetFormat format,
        DicomPixelData pixelData,
        string outputDirectory,
        string baseName)
    {
        var outputPath = Path.Combine(outputDirectory, $"{baseName}_{format.Suffix}.dcm");
        if (format.TransferSyntax == DicomTransferSyntax.JPEGProcess1 && pixelData.BitsStored > 8)
        {
            return CompressionPlanItem.Unsupported(
                format,
                outputPath,
                $"JPEG Baseline Process 1 supports only 8-bit input; this image has BitsStored={pixelData.BitsStored}.");
        }

        if (format.TransferSyntax == DicomTransferSyntax.JPEGProcess2_4 && pixelData.BitsStored > 8)
        {
            return CompressionPlanItem.Unsupported(
                format,
                outputPath,
                "JPEG Extended Process 2/4 12-bit sequential DCT is not implemented by the current managed JPEG path.");
        }

        if (IsJpegSequentialDct(format.TransferSyntax))
        {
            var unsupportedReason = GetJpegSequentialDctUnsupportedReason(pixelData);
            if (unsupportedReason is not null)
            {
                return CompressionPlanItem.Unsupported(format, outputPath, unsupportedReason);
            }
        }

        return CompressionPlanItem.Transcode(format, outputPath);
    }

    private static bool IsJpegSequentialDct(DicomTransferSyntax transferSyntax)
    {
        return transferSyntax == DicomTransferSyntax.JPEGProcess1 ||
               transferSyntax == DicomTransferSyntax.JPEGProcess2_4;
    }

    private static string? GetJpegSequentialDctUnsupportedReason(DicomPixelData pixelData)
    {
        if (pixelData.BitsAllocated != 8 || pixelData.BitsStored != 8)
        {
            return $"JPEG sequential DCT supports only BitsAllocated 8 and BitsStored 8; this image has BitsAllocated={pixelData.BitsAllocated}, BitsStored={pixelData.BitsStored}.";
        }

        if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
        {
            return "JPEG sequential DCT supports only SamplesPerPixel 1 or 3.";
        }

        var photometric = pixelData.Dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, string.Empty);
        if (photometric != PhotometricInterpretation.Monochrome1.Value &&
            photometric != PhotometricInterpretation.Monochrome2.Value &&
            photometric != PhotometricInterpretation.Rgb.Value &&
            photometric != "YBR_FULL" &&
            photometric != "YBR_FULL_422")
        {
            return $"JPEG sequential DCT does not support photometric interpretation {photometric ?? "<missing>"}.";
        }

        return null;
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
