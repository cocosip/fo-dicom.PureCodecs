using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class JpegLsExternalAcceptanceTests
{
    [Theory]
    [InlineData("JPEG-LS lossless acceptance sample")]
    [InlineData("JPEG-LS near-lossless acceptance sample")]
    public void Decode_efferent_acceptance_jpeg_ls_samples_when_available(string fixtureName)
    {
        var catalog = ExternalFixtureCatalog.Resolve();
        var fixture = catalog.AcceptanceFixtures.Single(fixture => fixture.Name == fixtureName);
        Assert.True(File.Exists(fixture.Path), fixture.Path);
        var file = DicomFile.Open(fixture.Path);
        var compressedPixelData = DicomPixelData.Create(file.Dataset);
        var rawPixelData = CreateRawTarget(compressedPixelData);
        IDicomCodec codec = fixture.ExpectedTransferSyntax == DicomTransferSyntax.JPEGLSLossless
            ? new DicomJpegLsLosslessCodec()
            : new DicomJpegLsNearLosslessCodec();

        codec.Decode(compressedPixelData, rawPixelData, codec.GetDefaultParameters());

        Assert.Equal(compressedPixelData.Width, rawPixelData.Width);
        Assert.Equal(compressedPixelData.Height, rawPixelData.Height);
        Assert.Equal(compressedPixelData.NumberOfFrames, rawPixelData.NumberOfFrames);
        Assert.True(rawPixelData.GetFrame(0).Size > 0);
    }

    private static DicomPixelData CreateRawTarget(DicomPixelData source)
    {
        var photometric = source.SamplesPerPixel == 3
            ? PhotometricInterpretation.Rgb.Value
            : source.PhotometricInterpretation.Value;
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, photometric },
            { DicomTag.Rows, source.Height },
            { DicomTag.Columns, source.Width },
            { DicomTag.BitsAllocated, source.BitsAllocated },
            { DicomTag.BitsStored, source.BitsStored },
            { DicomTag.HighBit, source.HighBit },
            { DicomTag.PixelRepresentation, (ushort)source.PixelRepresentation },
            { DicomTag.SamplesPerPixel, source.SamplesPerPixel },
        };

        if (source.NumberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, source.NumberOfFrames.ToString());
        }

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        }

        return DicomPixelData.Create(dataset, true);
    }
}
