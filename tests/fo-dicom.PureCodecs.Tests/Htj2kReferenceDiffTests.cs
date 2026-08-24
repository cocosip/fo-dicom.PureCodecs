using FellowOakDicom.PureCodecs.Htj2kReference;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kReferenceDiffTests
{
    [Fact]
    public void Diff_reports_each_required_manifest_field_mismatch()
    {
        var codestream = new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 };
        var expected = CreateManifest("CODESTREAM");
        var expectedFrame = expected.Frames[0];
        var cases = new (string Name, string ExpectedSummary, Htj2kReferenceManifest Actual)[]
        {
            ("package version", "package version", expected with { ReferencePackageVersion = "5.16.8" }),
            ("release commit", "release commit", expected with { ReferenceReleaseCommit = "other" }),
            ("codestream version", "codestream-reported version", expected with { CodestreamReportedOpenJphVersion = "0.22.0" }),
            ("transfer syntax", "transfer syntax", expected with { TransferSyntaxUid = "1.2.840.10008.1.2.4.202" }),
            ("frame count", "frame count", expected with { FrameCount = 2 }),
            ("effective parameters", "effective parameters", expected with
            {
                EffectiveParameters = expected.EffectiveParameters with { ProgressionOrder = "LRCP" }
            }),
            ("frame index", "frame index", expected with
            {
                Frames = new[] { expectedFrame with { FrameIndex = 1 } }
            }),
            ("raw frame hash", "raw-frame hash", expected with
            {
                Frames = new[] { expectedFrame with { RawFrameSha256 = "OTHER" } }
            }),
            ("codestream hash", "codestream hash", expected with
            {
                Frames = new[] { expectedFrame with { CodestreamSha256 = "OTHER" } }
            }),
            ("decoded frame hash", "decoded-frame hash", expected with
            {
                Frames = new[] { expectedFrame with { DecodedFrameSha256 = "OTHER" } }
            }),
            ("logical length", "logical codestream length", expected with
            {
                Frames = new[] { expectedFrame with { LogicalCodestreamLength = 5 } }
            }),
            ("marker codes", "marker summary", expected with
            {
                Frames = new[]
                {
                    expectedFrame with
                    {
                        MarkerSummary = expectedFrame.MarkerSummary with { MarkerCodes = new[] { "FF4F", "FF90", "FFD9" } }
                    }
                }
            }),
            ("tile-part count", "marker summary", expected with
            {
                Frames = new[]
                {
                    expectedFrame with
                    {
                        MarkerSummary = expectedFrame.MarkerSummary with { TilePartCount = 1 }
                    }
                }
            })
        };

        foreach (var testCase in cases)
        {
            var diff = Htj2kReferenceDiffComparer.Compare(
                expected,
                new[] { codestream },
                testCase.Actual,
                new[] { codestream });

            Assert.False(diff.IsMatch, testCase.Name);
            Assert.Contains(testCase.ExpectedSummary, diff.Summary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Diff_reports_the_first_mismatched_codestream_byte()
    {
        var expectedBytes = new byte[] { 0xFF, 0x4F, 0xFF, 0xD9 };
        var actualBytes = new byte[] { 0xFF, 0x4F, 0x00, 0xD9 };

        var diff = Htj2kReferenceDiffComparer.Compare(
            CreateManifest("AAAA"),
            new[] { expectedBytes },
            CreateManifest("BBBB"),
            new[] { actualBytes });

        Assert.False(diff.IsMatch);
        Assert.NotNull(diff.FirstDifference);
        Assert.Equal(0, diff.FirstDifference.FrameIndex);
        Assert.Equal(2, diff.FirstDifference.ByteOffset);
        Assert.Equal("FF", diff.FirstDifference.ExpectedByte);
        Assert.Equal("00", diff.FirstDifference.ActualByte);
    }

    private static Htj2kReferenceManifest CreateManifest(string codestreamHash)
    {
        return new Htj2kReferenceManifest(
            "6.0.0-beta1",
            "fc2df0efaa9acdee7b3640f821665107630933e8",
            "0.30.1",
            "1.2.840.10008.1.2.4.201",
            1,
            new Htj2kReferenceParameters("RPCL", true, true, 8),
            new[]
            {
                new Htj2kReferenceFrame(0, "RAW", codestreamHash, "DECODED", 4, new Htj2kMarkerSummary(new[] { "FF4F", "FFD9" }, 0))
            });
    }
}
