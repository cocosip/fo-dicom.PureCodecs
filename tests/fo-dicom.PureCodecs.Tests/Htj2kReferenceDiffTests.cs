using FellowOakDicom.PureCodecs.Htj2kReference;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kReferenceDiffTests
{
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
            "5.16.7",
            "1d05c6cca14883d06b835f8dadca5dae7d97577c",
            "0.21.2",
            "1.2.840.10008.1.2.4.201",
            new[]
            {
                new Htj2kReferenceFrame(0, "RAW", codestreamHash, "DECODED", 4, new Htj2kMarkerSummary(new[] { "FF4F", "FFD9" }, 0))
            });
    }
}
