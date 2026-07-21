using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Benchmarks;

internal static class BenchmarkFixtureVerifier
{
    public static void Verify()
    {
        ConfigureDicomServices();
        var acceptanceRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var manager = new PureTranscoderManager();
        var fixtures = BenchmarkFixtureCatalog.Create(acceptanceRoot);

        foreach (var fixture in fixtures)
        {
            var file = DicomFile.Open(fixture.Path, FileReadOption.ReadAll);
            var compressed = DicomPixelData.Create(file.Dataset);
            if (!ReferenceEquals(fixture.TransferSyntax, compressed.Syntax))
            {
                throw new InvalidDataException(
                    $"{fixture.Name} declared {fixture.TransferSyntax.UID.Name} but contains {compressed.Syntax.UID.Name}.");
            }

            var codec = manager.GetCodec(fixture.TransferSyntax);
            var rawDataset = CreateRawDataset(compressed);
            var decoded = DicomPixelData.Create(rawDataset, true);
            codec.Decode(compressed, decoded, codec.GetDefaultParameters());
            if (decoded.NumberOfFrames != compressed.NumberOfFrames || decoded.GetFrame(0).Size == 0)
            {
                throw new InvalidDataException($"{fixture.Name} did not produce decoded pixel data.");
            }

            var encoded = DicomPixelData.Create(CloneWithoutPixelData(rawDataset, fixture.TransferSyntax), true);
            codec.Encode(decoded, encoded, codec.GetDefaultParameters());
            if (encoded.NumberOfFrames != decoded.NumberOfFrames || encoded.GetFrame(0).Size == 0)
            {
                throw new InvalidDataException($"{fixture.Name} did not re-encode decoded pixel data.");
            }
        }

        Console.WriteLine($"Verified {fixtures.Count} benchmark fixtures with PureCodecs.");
    }

    private static DicomDataset CloneWithoutPixelData(DicomDataset source, DicomTransferSyntax transferSyntax)
    {
        var clone = new DicomDataset(transferSyntax);
        foreach (var item in source)
        {
            clone.Add(item);
        }

        clone.Remove(DicomTag.PixelData);
        return clone;
    }

    private static DicomDataset CreateRawDataset(DicomPixelData source)
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

        return dataset;
    }

    private static void ConfigureDicomServices()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();
    }
}
