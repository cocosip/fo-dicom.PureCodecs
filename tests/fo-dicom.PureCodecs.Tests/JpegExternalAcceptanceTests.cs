using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegExternalAcceptanceTests
{
    [Theory]
    [InlineData("JPEG baseline YBR full acceptance sample")]
    [InlineData("JPEG baseline YBR 4:2:2 acceptance sample")]
    public void Process1_decodes_efferent_acceptance_jpeg_samples(string fixtureName)
    {
        var fixture = ExternalFixtureCatalog.Resolve().AcceptanceFixtures.Single(fixture => fixture.Name == fixtureName);
        var file = DicomFile.Open(fixture.Path);
        var compressedPixelData = DicomPixelData.Create(file.Dataset);
        var rawPixelData = CreateRawTarget(compressedPixelData);
        var codec = new DicomJpegProcess1Codec();

        codec.Decode(compressedPixelData, rawPixelData, codec.GetDefaultParameters());

        Assert.Equal(compressedPixelData.Width, rawPixelData.Width);
        Assert.Equal(compressedPixelData.Height, rawPixelData.Height);
        Assert.Equal(compressedPixelData.NumberOfFrames, rawPixelData.NumberOfFrames);
        Assert.Equal(compressedPixelData.Width * compressedPixelData.Height * compressedPixelData.SamplesPerPixel, rawPixelData.GetFrame(0).Size);
    }

    private static DicomPixelData CreateRawTarget(DicomPixelData source)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
            { DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved },
        };

        if (source.NumberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, source.NumberOfFrames.ToString());
        }

        return DicomPixelData.Create(dataset, true);
    }
}
