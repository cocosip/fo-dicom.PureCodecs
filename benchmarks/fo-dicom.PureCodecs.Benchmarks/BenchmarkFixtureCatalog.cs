using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;

namespace FellowOakDicom.PureCodecs.Benchmarks;

public sealed record BenchmarkFixture(string Name, string Path, DicomTransferSyntax TransferSyntax, int? JpegLsAllowedError = null)
{
    public override string ToString()
    {
        return Name;
    }
}

public static class BenchmarkFixtureCatalog
{
    public static IReadOnlyList<BenchmarkFixture> Create(string acceptanceRoot)
    {
        if (string.IsNullOrWhiteSpace(acceptanceRoot))
        {
            throw new ArgumentException("Fixture root is required.", nameof(acceptanceRoot));
        }

        GeneratedBenchmarkFixtures.Ensure(acceptanceRoot);

        return new[]
        {
            CreateFixture("RLE lossless", "PM5644-960x540_RLE-Lossless.dcm", DicomTransferSyntax.RLELossless),
            CreateFixture("JPEG baseline", "PM5644-960x540_JPEG-Baseline_YBR422.dcm", DicomTransferSyntax.JPEGProcess1),
            CreateFixture("JPEG Extended 8-bit", "JPEG-Extended-8bit.dcm", DicomTransferSyntax.JPEGProcess2_4),
            CreateFixture("JPEG Extended 12-bit", "JPEG-Extended-12bit.dcm", DicomTransferSyntax.JPEGProcess2_4),
            CreateFixture("JPEG lossless Process 14", "JPEG-Lossless-Process14_RGB.dcm", DicomTransferSyntax.JPEGProcess14),
            CreateFixture("JPEG lossless SV1", "PM5644-960x540_JPEG-Lossless_RGB.dcm", DicomTransferSyntax.JPEGProcess14SV1),
            CreateFixture("JPEG-LS lossless", "PM5644-960x540_JPEG-LS_Lossless.dcm", DicomTransferSyntax.JPEGLSLossless),
            CreateFixture("JPEG-LS near-lossless (NEAR=2)", "PM5644-960x540_JPEG-LS_NearLossless.dcm", DicomTransferSyntax.JPEGLSNearLossless, jpegLsAllowedError: 2),
            CreateFixture("JPEG 2000 lossless", "PM5644-960x540_JPEG2000-Lossless.dcm", DicomTransferSyntax.JPEG2000Lossless),
            CreateFixture("JPEG 2000 lossy", "PM5644-960x540_JPEG2000-Lossy.dcm", DicomTransferSyntax.JPEG2000Lossy),
        };

        BenchmarkFixture CreateFixture(string name, string fileName, DicomTransferSyntax transferSyntax, int? jpegLsAllowedError = null)
        {
            return new BenchmarkFixture(name, Path.Combine(acceptanceRoot, fileName), transferSyntax, jpegLsAllowedError);
        }
    }
}

internal static class GeneratedBenchmarkFixtures
{
    private const string BaselineFixtureFileName = "PM5644-960x540_JPEG-Baseline_YBR422.dcm";
    private const string Extended8BitFixtureFileName = "JPEG-Extended-8bit.dcm";
    private const string Extended12BitFixtureFileName = "JPEG-Extended-12bit.dcm";
    private const string JpegLosslessSv1FixtureFileName = "PM5644-960x540_JPEG-Lossless_RGB.dcm";
    private const string JpegLosslessProcess14FixtureFileName = "JPEG-Lossless-Process14_RGB.dcm";

    public static void Ensure(string fixtureRoot)
    {
        Directory.CreateDirectory(fixtureRoot);
        EnsureExtended8BitFixture(fixtureRoot);
        EnsureExtended12BitFixture(fixtureRoot);
        EnsureJpegLosslessProcess14Fixture(fixtureRoot);
    }

    private static void EnsureExtended8BitFixture(string fixtureRoot)
    {
        var targetPath = Path.Combine(fixtureRoot, Extended8BitFixtureFileName);
        if (File.Exists(targetPath))
        {
            return;
        }

        var sourcePath = Path.Combine(fixtureRoot, BaselineFixtureFileName);
        var source = DicomPixelData.Create(DicomFile.Open(sourcePath, FileReadOption.ReadAll).Dataset);
        var rawDataset = CreateRawDataset(source);
        var raw = DicomPixelData.Create(rawDataset, true);
        var baselineCodec = new DicomJpegProcess1Codec();
        baselineCodec.Decode(source, raw, baselineCodec.GetDefaultParameters());

        SaveExtendedFixture(raw, targetPath);
    }

    private static void EnsureExtended12BitFixture(string fixtureRoot)
    {
        var targetPath = Path.Combine(fixtureRoot, Extended12BitFixtureFileName);
        if (File.Exists(targetPath))
        {
            return;
        }

        const ushort rows = 288;
        const ushort columns = 288;
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, rows },
            { DicomTag.Columns, columns },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, (ushort)12 },
            { DicomTag.HighBit, (ushort)11 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };
        var raw = DicomPixelData.Create(dataset, true);
        var frame = new byte[rows * columns * 2];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var sample = (ushort)((row * 73 + column * 193 + (row * column >> 3)) & 0x0fff);
                var offset = (row * columns + column) * 2;
                frame[offset] = (byte)sample;
                frame[offset + 1] = (byte)(sample >> 8);
            }
        }

        raw.AddFrame(new MemoryByteBuffer(frame));
        SaveExtendedFixture(raw, targetPath);
    }

    private static void EnsureJpegLosslessProcess14Fixture(string fixtureRoot)
    {
        var targetPath = Path.Combine(fixtureRoot, JpegLosslessProcess14FixtureFileName);
        if (File.Exists(targetPath))
        {
            return;
        }

        var sourcePath = Path.Combine(fixtureRoot, JpegLosslessSv1FixtureFileName);
        var source = DicomPixelData.Create(DicomFile.Open(sourcePath, FileReadOption.ReadAll).Dataset);
        var rawDataset = CreateRawDataset(source);
        var raw = DicomPixelData.Create(rawDataset, true);
        var sv1Codec = new DicomJpegLossless14SV1Codec();
        sv1Codec.Decode(source, raw, sv1Codec.GetDefaultParameters());

        var dataset = CloneWithoutPixelData(raw.Dataset, DicomTransferSyntax.JPEGProcess14);
        EnsureFileMetaIdentifiers(dataset);
        var target = DicomPixelData.Create(dataset, true);
        var process14Codec = new DicomJpegLossless14Codec();
        process14Codec.Encode(raw, target, process14Codec.GetDefaultParameters());
        new DicomFile(dataset).Save(targetPath);
    }

    private static void SaveExtendedFixture(DicomPixelData raw, string targetPath)
    {
        var dataset = CloneWithoutPixelData(raw.Dataset, DicomTransferSyntax.JPEGProcess2_4);
        EnsureFileMetaIdentifiers(dataset);
        var target = DicomPixelData.Create(dataset, true);
        var codec = new DicomJpegProcess2_4Codec();
        codec.Encode(raw, target, codec.GetDefaultParameters());
        new DicomFile(dataset).Save(targetPath);
    }

    private static void EnsureFileMetaIdentifiers(DicomDataset dataset)
    {
        dataset.AddOrUpdate(DicomTag.SOPClassUID, dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage));
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, DicomUID.Generate()));
        dataset.AddOrUpdate(DicomTag.StudyInstanceUID, dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, DicomUID.Generate()));
        dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, DicomUID.Generate()));
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

        if (source.SamplesPerPixel > 1)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
        }

        return dataset;
    }
}
