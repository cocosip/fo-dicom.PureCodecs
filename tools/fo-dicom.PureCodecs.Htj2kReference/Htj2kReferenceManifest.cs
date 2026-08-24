using System.Security.Cryptography;

namespace FellowOakDicom.PureCodecs.Htj2kReference;

public sealed record Htj2kReferenceManifest(
    string ReferencePackageVersion,
    string ReferenceReleaseCommit,
    string CodestreamReportedOpenJphVersion,
    string TransferSyntaxUid,
    IReadOnlyList<Htj2kReferenceFrame> Frames);

public sealed record Htj2kReferenceFrame(
    int FrameIndex,
    string RawFrameSha256,
    string CodestreamSha256,
    string DecodedFrameSha256,
    int LogicalCodestreamLength,
    Htj2kMarkerSummary MarkerSummary);

public sealed record Htj2kMarkerSummary(
    IReadOnlyList<string> MarkerCodes,
    int TilePartCount);

public static class Htj2kReferenceManifestBuilder
{
    public static byte[] ExtractLogicalCodestream(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        for (var index = 0; index < frame.Length - 1; index++)
        {
            if (frame[index] == 0xFF && frame[index + 1] == 0xD9)
            {
                var logicalCodestream = new byte[index + 2];
                Buffer.BlockCopy(frame, 0, logicalCodestream, 0, logicalCodestream.Length);
                return logicalCodestream;
            }
        }

        throw new InvalidDataException("HTJ2K codestream does not contain EOC.");
    }

    public static string ComputeSha256(byte[] bytes)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
