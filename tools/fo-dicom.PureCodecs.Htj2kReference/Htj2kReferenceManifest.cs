using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

public static class Htj2kReferencePackageVersionReader
{
    public static string ReadResolvedVersion(string dependencyManifestPath, string packageId)
    {
        if (string.IsNullOrWhiteSpace(dependencyManifestPath))
        {
            throw new ArgumentException("Dependency manifest path is required.", nameof(dependencyManifestPath));
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id is required.", nameof(packageId));
        }

        using var document = JsonDocument.Parse(File.ReadAllText(dependencyManifestPath));
        if (!document.RootElement.TryGetProperty("libraries", out var libraries)
            || libraries.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Dependency manifest does not contain a libraries object.");
        }

        var prefix = packageId + "/";
        string? resolvedVersion = null;
        foreach (var library in libraries.EnumerateObject())
        {
            if (!library.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = library.Name.Substring(prefix.Length);
            if (candidate.Length == 0 || resolvedVersion is not null)
            {
                throw new InvalidDataException($"Dependency manifest does not identify exactly one {packageId} package version.");
            }

            resolvedVersion = candidate;
        }

        return resolvedVersion
            ?? throw new InvalidDataException($"Dependency manifest does not contain package {packageId}.");
    }
}

public static class Htj2kReferenceProvenanceReader
{
    public const string MinimumPackageVersion = "6.0.0-beta1";

    public static Htj2kReferenceProvenance ReadAndValidate(
        Assembly assembly,
        string? resolvedPackageVersion = null,
        string minimumPackageVersion = MinimumPackageVersion)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            throw new InvalidDataException("HTJ2K reference assembly does not expose an informational version.");
        }

        var parts = informationalVersion.Split(new[] { '+' }, 2);
        var packageVersion = resolvedPackageVersion ?? parts[0];
        var releaseCommit = parts.Length == 2 ? parts[1] : string.Empty;
        var comparablePackageVersion = ParseComparableVersion(packageVersion, "reference package");
        var comparableMinimumVersion = ParseComparableVersion(minimumPackageVersion, "minimum package");
        if (CompareVersions(comparablePackageVersion, comparableMinimumVersion) < 0)
        {
            throw new InvalidDataException(
                $"HTJ2K reference package version is {packageVersion}; minimum supported version is {minimumPackageVersion}.");
        }

        return new Htj2kReferenceProvenance(packageVersion, releaseCommit);
    }

    private static ComparablePackageVersion ParseComparableVersion(string packageVersion, string description)
    {
        var versionWithoutBuildMetadata = packageVersion.Split(new[] { '+' }, 2)[0];
        var parts = versionWithoutBuildMetadata.Split(new[] { '-' }, 2);
        var numericVersion = parts[0];
        if (!Version.TryParse(numericVersion, out var version))
        {
            throw new InvalidDataException($"HTJ2K {description} version '{packageVersion}' is invalid.");
        }

        return new ComparablePackageVersion(version, parts.Length == 2 ? parts[1] : string.Empty);
    }

    private static int CompareVersions(ComparablePackageVersion left, ComparablePackageVersion right)
    {
        var coreComparison = left.Core.CompareTo(right.Core);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (left.Prerelease.Length == 0)
        {
            return right.Prerelease.Length == 0 ? 0 : 1;
        }

        if (right.Prerelease.Length == 0)
        {
            return -1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left.Prerelease, right.Prerelease);
    }

    private sealed record ComparablePackageVersion(Version Core, string Prerelease);
}
