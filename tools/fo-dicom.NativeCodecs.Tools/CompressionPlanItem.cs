namespace FellowOakDicom.NativeCodecs.Tools;

public sealed class CompressionPlanItem
{
    private CompressionPlanItem(CompressionTargetFormat format, string outputPath, bool isUnsupported, string? skipReason)
    {
        Format = format;
        OutputPath = outputPath;
        IsUnsupported = isUnsupported;
        SkipReason = skipReason;
    }

    public CompressionTargetFormat Format { get; }

    public string OutputPath { get; }

    public bool IsUnsupported { get; }

    public bool ShouldSkip => IsUnsupported;

    public string? SkipReason { get; }

    public static CompressionPlanItem Transcode(CompressionTargetFormat format, string outputPath)
    {
        return new CompressionPlanItem(format, outputPath, isUnsupported: false, skipReason: null);
    }
}
