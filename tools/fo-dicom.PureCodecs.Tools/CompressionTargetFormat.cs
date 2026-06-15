using FellowOakDicom;

namespace FellowOakDicom.PureCodecs.Tools;

public sealed class CompressionTargetFormat
{
    public CompressionTargetFormat(string name, DicomTransferSyntax transferSyntax, string suffix, bool isLossless)
    {
        Name = name;
        TransferSyntax = transferSyntax;
        Suffix = suffix;
        IsLossless = isLossless;
    }

    public string Name { get; }

    public DicomTransferSyntax TransferSyntax { get; }

    public string Suffix { get; }

    public bool IsLossless { get; }
}
