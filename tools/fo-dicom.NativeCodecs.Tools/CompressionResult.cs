namespace FellowOakDicom.NativeCodecs.Tools;

public sealed class CompressionResult
{
    public CompressionResult(CompressionPlanItem item, CompressionResultStatus status, long? outputSize, string? message)
    {
        Item = item;
        Status = status;
        OutputSize = outputSize;
        Message = message;
    }

    public CompressionPlanItem Item { get; }

    public CompressionResultStatus Status { get; }

    public long? OutputSize { get; }

    public string? Message { get; }
}
