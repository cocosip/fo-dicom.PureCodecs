using System.Security.Cryptography;

namespace FellowOakDicom.PureCodecs.Htj2kReference;

public sealed record Htj2kReferenceManifest(
    string TransferSyntaxUid,
    int FrameCount,
    IReadOnlyList<Htj2kReferenceFrame> Frames);

public sealed record Htj2kReferenceFrame(
    int FrameIndex,
    string RawFrameSha256,
    string EncodedFrameSha256,
    string DecodedFrameSha256,
    int EncodedFrameLength);

public static class Htj2kReferenceManifestBuilder
{
    public static string ComputeSha256(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
