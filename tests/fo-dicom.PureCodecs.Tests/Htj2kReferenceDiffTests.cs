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
            ("transfer syntax", "transfer syntax", expected with { TransferSyntaxUid = "1.2.840.10008.1.2.4.202" }),
            ("frame count", "frame count", expected with { FrameCount = 2 }),
            ("frame index", "frame index", expected with
            {
                Frames = new[] { expectedFrame with { FrameIndex = 1 } }
            }),
            ("raw frame hash", "raw-frame hash", expected with
            {
                Frames = new[] { expectedFrame with { RawFrameSha256 = "OTHER" } }
            }),
            ("encoded frame hash", "encoded frame hash", expected with
            {
                Frames = new[] { expectedFrame with { EncodedFrameSha256 = "OTHER" } }
            }),
            ("decoded frame hash", "decoded-frame hash", expected with
            {
                Frames = new[] { expectedFrame with { DecodedFrameSha256 = "OTHER" } }
            }),
            ("encoded length", "encoded frame length", expected with
            {
                Frames = new[] { expectedFrame with { EncodedFrameLength = 5 } }
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
            "1.2.840.10008.1.2.4.201",
            1,
            new[]
            {
                new Htj2kReferenceFrame(0, "RAW", codestreamHash, "DECODED", 4)
            });
    }
}
