using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FellowOakDicom.PureCodecs.Htj2kReference;

public sealed record Htj2kReferenceManifest(
    string ReferencePackageVersion,
    string ReferenceReleaseCommit,
    string CodestreamReportedOpenJphVersion,
    string TransferSyntaxUid,
    int FrameCount,
    Htj2kReferenceParameters EffectiveParameters,
    IReadOnlyList<Htj2kReferenceFrame> Frames);

public sealed record Htj2kReferenceParameters(
    string ProgressionOrder,
    bool IsReversible,
    bool UsesColorTransform,
    int BitsPerSample);

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

    public static Htj2kReferenceParameters ReadEffectiveParameters(byte[] codestream)
    {
        var sizOffset = FindMarker(codestream, 0x51);
        var codOffset = FindMarker(codestream, 0x52);
        var precision = (codestream[sizOffset + 40] & 0x7F) + 1;
        var progressionOrder = codestream[codOffset + 5] switch
        {
            0 => "LRCP",
            1 => "RLCP",
            2 => "RPCL",
            3 => "PCRL",
            4 => "CPRL",
            _ => throw new InvalidDataException("HTJ2K COD progression order is invalid.")
        };

        return new Htj2kReferenceParameters(
            progressionOrder,
            IsReversible: codestream[codOffset + 13] == 1,
            UsesColorTransform: codestream[codOffset + 8] != 0,
            BitsPerSample: precision);
    }

    public static string ReadCodestreamReportedOpenJphVersion(byte[] codestream)
    {
        const string prefix = "OpenJPH Ver ";
        var offset = 0;
        while (offset < codestream.Length)
        {
            RequireMarker(codestream, offset);
            var marker = codestream[offset + 1];
            if (marker == 0x90 || marker == 0xD9)
            {
                break;
            }

            var markerLength = MarkerLength(codestream, offset, marker);
            if (marker == 0x64)
            {
                var segmentLength = ReadUInt16(codestream, offset + 2);
                if (segmentLength >= 4)
                {
                    var text = Encoding.ASCII.GetString(codestream, offset + 6, segmentLength - 4);
                    if (text.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return text.Substring(prefix.Length).TrimEnd('.');
                    }
                }
            }

            offset += markerLength;
        }

        throw new InvalidDataException("HTJ2K codestream does not report an OpenJPH version in COM.");
    }

    public static Htj2kMarkerSummary ReadMarkerSummary(byte[] codestream)
    {
        var markerCodes = new List<string>();
        var tilePartCount = 0;
        var offset = 0;
        while (offset < codestream.Length)
        {
            RequireMarker(codestream, offset);
            var marker = codestream[offset + 1];
            markerCodes.Add("FF" + marker.ToString("X2"));
            if (marker == 0xD9)
            {
                break;
            }

            if (marker == 0x90)
            {
                tilePartCount++;
                offset = SkipTilePart(codestream, offset, markerCodes);
                continue;
            }

            offset += MarkerLength(codestream, offset, marker);
        }

        if (markerCodes.Count == 0 || markerCodes[^1] != "FFD9")
        {
            throw new InvalidDataException("HTJ2K codestream marker sequence does not end with EOC.");
        }

        return new Htj2kMarkerSummary(markerCodes, tilePartCount);
    }

    private static int FindMarker(byte[] codestream, byte marker)
    {
        for (var offset = 0; offset + 1 < codestream.Length; offset++)
        {
            if (codestream[offset] == 0xFF && codestream[offset + 1] == marker)
            {
                return offset;
            }
        }

        throw new InvalidDataException($"HTJ2K marker FF{marker:X2} was not found.");
    }

    private static int SkipTilePart(byte[] codestream, int offset, List<string> markerCodes)
    {
        if (offset + 12 > codestream.Length || ReadUInt16(codestream, offset + 2) != 10)
        {
            throw new InvalidDataException("HTJ2K SOT marker is invalid.");
        }

        var tilePartLength = ReadUInt32(codestream, offset + 6);
        if (tilePartLength < 14 || tilePartLength > codestream.Length - offset)
        {
            throw new InvalidDataException("HTJ2K SOT length is invalid.");
        }

        var tilePartEnd = checked(offset + (int)tilePartLength);
        offset += 12;
        while (offset < tilePartEnd)
        {
            RequireMarker(codestream, offset);
            var marker = codestream[offset + 1];
            markerCodes.Add("FF" + marker.ToString("X2"));
            if (marker == 0x93)
            {
                return tilePartEnd;
            }

            offset += MarkerLength(codestream, offset, marker);
        }

        throw new InvalidDataException("HTJ2K tile part does not contain SOD.");
    }

    private static int MarkerLength(byte[] codestream, int offset, byte marker)
    {
        if (marker is 0x4F or 0x92 or 0x93 or 0xD9)
        {
            return 2;
        }

        if (offset + 4 > codestream.Length)
        {
            throw new InvalidDataException("HTJ2K marker length is outside the codestream.");
        }

        var length = ReadUInt16(codestream, offset + 2);
        if (length < 2 || length > codestream.Length - offset - 2)
        {
            throw new InvalidDataException("HTJ2K marker length is invalid.");
        }

        return 2 + length;
    }

    private static void RequireMarker(byte[] codestream, int offset)
    {
        if (offset + 1 >= codestream.Length || codestream[offset] != 0xFF)
        {
            throw new InvalidDataException("HTJ2K marker prefix is invalid.");
        }
    }

    private static int ReadUInt16(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
}

public sealed record Htj2kReferenceProvenance(string PackageVersion, string ReleaseCommit);

public static class Htj2kReferenceProvenanceReader
{
    public const string ExpectedPackageVersion = "5.16.7";
    public const string ExpectedReleaseCommit = "1d05c6cca14883d06b835f8dadca5dae7d97577c";

    public static Htj2kReferenceProvenance ReadAndValidate(
        Assembly assembly,
        string expectedPackageVersion = ExpectedPackageVersion,
        string expectedReleaseCommit = ExpectedReleaseCommit)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            throw new InvalidDataException("HTJ2K reference assembly does not expose an informational version.");
        }

        var parts = informationalVersion.Split(new[] { '+' }, 2);
        var packageVersion = parts[0];
        var releaseCommit = parts.Length == 2 ? parts[1] : string.Empty;
        if (!string.Equals(packageVersion, expectedPackageVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"HTJ2K reference package version is {packageVersion}; expected {expectedPackageVersion}.");
        }

        if (!string.Equals(releaseCommit, expectedReleaseCommit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("HTJ2K reference release commit does not match the expected baseline.");
        }

        return new Htj2kReferenceProvenance(packageVersion, releaseCommit);
    }
}
