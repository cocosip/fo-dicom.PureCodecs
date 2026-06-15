namespace FellowOakDicom.PureCodecs.Tools;

public sealed class CompressionPlanItem
{
    private CompressionPlanItem(
        CompressionTargetFormat format,
        string outputPath,
        bool isUnsupported,
        string? skipReason)
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

    public static CompressionPlanItem Skip(CompressionTargetFormat format, string outputPath, string skipReason)
    {
        return Unsupported(format, outputPath, skipReason);
    }

    public static CompressionPlanItem Unsupported(CompressionTargetFormat format, string outputPath, string reason)
    {
        return new CompressionPlanItem(format, outputPath, isUnsupported: true, skipReason: reason);
    }
}
