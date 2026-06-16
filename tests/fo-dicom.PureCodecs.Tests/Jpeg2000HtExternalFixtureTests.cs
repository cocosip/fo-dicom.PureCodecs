using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class Jpeg2000HtExternalFixtureTests
{
    [Fact]
    public void Openjph_irreversible_htj2k_fixture_parses_header_and_is_rejected_with_managed_error()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestSupport", "Fixtures", "OpenJPH", "test.j2c");
        var codestream = File.ReadAllBytes(path);

        Assert.True(Jpeg2000CodestreamReader.IsRawCodestream(codestream));

        var reader = new Jpeg2000CodestreamReader(codestream);
        Jpeg2000SizeSegment? siz = null;
        Jpeg2000CodingStyleDefault? cod = null;
        while (!reader.EndOfData && (siz == null || cod == null))
        {
            var segment = reader.ReadNext();
            if (segment.Code == Jpeg2000Marker.SIZ)
            {
                siz = Jpeg2000SizeSegment.Parse(segment);
            }
            else if (segment.Code == Jpeg2000Marker.COD)
            {
                cod = Jpeg2000CodingStyleDefault.Parse(segment);
            }
        }

        Assert.NotNull(siz);
        Assert.NotNull(cod);
        Assert.Equal(512u, siz.ReferenceGridWidth);
        Assert.Equal(512u, siz.ReferenceGridHeight);
        Assert.Equal(3, siz.Components.Count);
        Assert.Equal(0x40, cod.CodeBlockStyle & 0x40);

        var dataset = DicomPixelDataFixtures.CreateRgbInterleaved(rows: 512, columns: 512);
        var compressedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.HTJ2KLossless);
        var compressed = DicomPixelData.Create(compressedDataset, true);
        compressed.AddFrame(new FellowOakDicom.IO.Buffer.MemoryByteBuffer(codestream));
        var decodedDataset = CloneForTransferSyntax(dataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decoded = DicomPixelData.Create(decodedDataset, true);

        var exception = Assert.Throws<DicomCodecException>(() =>
            new Jpeg2000HtFrameCodec().DecodeFrame(decoded, codestream));

        Assert.Contains("reversible lossless", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DicomDataset CloneForTransferSyntax(DicomDataset source, DicomTransferSyntax transferSyntax)
    {
        var clone = new DicomDataset(transferSyntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }
}
