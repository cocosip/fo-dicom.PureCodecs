using FellowOakDicom.PureCodecs.Htj2kReference;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Htj2kReferenceManifestTests
{
    [Fact]
    public void Logical_codestream_extraction_excludes_dicom_item_padding()
    {
        var logical = Htj2kReferenceManifestBuilder.ExtractLogicalCodestream(
            new byte[] { 0xFF, 0x4F, 0x00, 0x01, 0xFF, 0xD9, 0x00 });

        Assert.Equal(new byte[] { 0xFF, 0x4F, 0x00, 0x01, 0xFF, 0xD9 }, logical);
    }

    [Fact]
    public void Logical_codestream_extraction_rejects_frames_without_eoc()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Htj2kReferenceManifestBuilder.ExtractLogicalCodestream(new byte[] { 0xFF, 0x4F, 0x00 }));

        Assert.Equal("HTJ2K codestream does not contain EOC.", exception.Message);
    }
}
