using FellowOakDicom;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal sealed record ExternalFixture(
    string Name,
    string Path,
    DicomTransferSyntax ExpectedTransferSyntax);
