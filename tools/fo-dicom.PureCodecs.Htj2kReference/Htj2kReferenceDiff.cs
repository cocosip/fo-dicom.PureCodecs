namespace FellowOakDicom.PureCodecs.Htj2kReference;

public sealed record Htj2kReferenceDiff(
    bool IsMatch,
    string? Summary,
    Htj2kReferenceByteDifference? FirstDifference)
{
    public static Htj2kReferenceDiff Match() => new(true, null, null);

    public static Htj2kReferenceDiff Mismatch(string summary) => new(false, summary, null);

    public static Htj2kReferenceDiff Mismatch(string summary, Htj2kReferenceByteDifference difference) => new(false, summary, difference);
}

public sealed record Htj2kReferenceByteDifference(
    int FrameIndex,
    int ByteOffset,
    string ExpectedByte,
    string ActualByte);

public static class Htj2kReferenceDiffComparer
{
    public static Htj2kReferenceDiff Compare(
        Htj2kReferenceManifest expected,
        IReadOnlyList<byte[]> expectedFrames,
        Htj2kReferenceManifest actual,
        IReadOnlyList<byte[]> actualFrames)
    {
        if (expected is null)
        {
            throw new ArgumentNullException(nameof(expected));
        }

        if (expectedFrames is null)
        {
            throw new ArgumentNullException(nameof(expectedFrames));
        }

        if (actual is null)
        {
            throw new ArgumentNullException(nameof(actual));
        }

        if (actualFrames is null)
        {
            throw new ArgumentNullException(nameof(actualFrames));
        }

        var provenanceMismatch = CompareProvenance(expected, actual);
        if (provenanceMismatch is not null)
        {
            return Htj2kReferenceDiff.Mismatch(provenanceMismatch);
        }

        if (expected.FrameCount != actual.FrameCount
            || expected.FrameCount != expected.Frames.Count
            || actual.FrameCount != actual.Frames.Count
            || expectedFrames.Count != actualFrames.Count
            || expected.Frames.Count != expectedFrames.Count)
        {
            return Htj2kReferenceDiff.Mismatch("HTJ2K reference frame count differs.");
        }

        if (expected.EffectiveParameters != actual.EffectiveParameters)
        {
            return Htj2kReferenceDiff.Mismatch("HTJ2K effective parameters differ.");
        }

        for (var frameIndex = 0; frameIndex < expected.Frames.Count; frameIndex++)
        {
            var expectedFrame = expected.Frames[frameIndex];
            var actualFrame = actual.Frames[frameIndex];
            if (expectedFrame.FrameIndex != actualFrame.FrameIndex)
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K reference frame indexes differ.");
            }


            if (!string.Equals(expectedFrame.RawFrameSha256, actualFrame.RawFrameSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K raw-frame hashes differ.");
            }

            var byteDifference = FindFirstByteDifference(expectedFrames[frameIndex], actualFrames[frameIndex], frameIndex);
            if (byteDifference is not null)
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K logical codestream bytes differ.", byteDifference);
            }

            if (!string.Equals(expectedFrame.CodestreamSha256, actualFrame.CodestreamSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K logical codestream hashes differ.");
            }


            if (!string.Equals(expectedFrame.DecodedFrameSha256, actualFrame.DecodedFrameSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K decoded-frame hashes differ.");
            }

            if (expectedFrame.LogicalCodestreamLength != actualFrame.LogicalCodestreamLength)
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K logical codestream lengths differ.");
            }

            if (!expectedFrame.MarkerSummary.MarkerCodes.SequenceEqual(actualFrame.MarkerSummary.MarkerCodes, StringComparer.Ordinal)
                || expectedFrame.MarkerSummary.TilePartCount != actualFrame.MarkerSummary.TilePartCount)
            {
                return Htj2kReferenceDiff.Mismatch("HTJ2K marker summary differs.");
            }
        }

        return Htj2kReferenceDiff.Match();
    }

    private static string? CompareProvenance(Htj2kReferenceManifest expected, Htj2kReferenceManifest actual)
    {
        if (!string.Equals(expected.ReferencePackageVersion, actual.ReferencePackageVersion, StringComparison.Ordinal))
        {
            return "HTJ2K reference package versions differ.";
        }

        if (!string.Equals(expected.ReferenceReleaseCommit, actual.ReferenceReleaseCommit, StringComparison.Ordinal))
        {
            return "HTJ2K reference release commits differ.";
        }

        if (!string.Equals(expected.CodestreamReportedOpenJphVersion, actual.CodestreamReportedOpenJphVersion, StringComparison.Ordinal))
        {
            return "HTJ2K codestream-reported versions differ.";
        }

        return string.Equals(expected.TransferSyntaxUid, actual.TransferSyntaxUid, StringComparison.Ordinal)
            ? null
            : "HTJ2K transfer syntaxes differ.";
    }

    private static Htj2kReferenceByteDifference? FindFirstByteDifference(byte[] expected, byte[] actual, int frameIndex)
    {
        var sharedLength = Math.Min(expected.Length, actual.Length);
        for (var offset = 0; offset < sharedLength; offset++)
        {
            if (expected[offset] != actual[offset])
            {
                return new Htj2kReferenceByteDifference(
                    frameIndex,
                    offset,
                    expected[offset].ToString("X2"),
                    actual[offset].ToString("X2"));
            }
        }

        if (expected.Length == actual.Length)
        {
            return null;
        }

        return new Htj2kReferenceByteDifference(
            frameIndex,
            sharedLength,
            sharedLength < expected.Length ? expected[sharedLength].ToString("X2") : "<end>",
            sharedLength < actual.Length ? actual[sharedLength].ToString("X2") : "<end>");
    }
}
