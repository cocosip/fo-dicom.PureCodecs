using System.Threading;
using BenchmarkDotNet.Attributes;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using PureJpeg2000Params = FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000Params;
using PureJpegLsParams = FellowOakDicom.PureCodecs.JpegLs.DicomJpegLsParams;

namespace FellowOakDicom.PureCodecs.Benchmarks;

[MemoryDiagnoser]
[MarkdownExporter]
[JsonExporter]
public class CodecBenchmarks
{
    private static int _servicesConfigured;
    private DicomDataset _compressedDataset = null!;
    private IDicomCodec _codec = null!;
    private DicomCodecParams _codecParameters = null!;
    private DicomDataset _rawDataset = null!;

    [ParamsSource(nameof(Fixtures))]
    public BenchmarkFixture Fixture { get; set; } = null!;

    public IEnumerable<BenchmarkFixture> Fixtures()
    {
        return BenchmarkFixtureCatalog.Create(Path.Combine(AppContext.BaseDirectory, "Fixtures"));
    }

    [GlobalSetup]
    public void Setup()
    {
        ConfigureDicomServices();

        _compressedDataset = DicomFile.Open(Fixture.Path, FileReadOption.ReadAll).Dataset;
        _codec = new PureTranscoderManager().GetCodec(Fixture.TransferSyntax);
        _codecParameters = CreateOutputCodecParams(Fixture) ?? _codec.GetDefaultParameters();
        var compressed = DicomPixelData.Create(_compressedDataset);
        _rawDataset = CreateRawDataset(compressed);
        _codec.Decode(compressed, DicomPixelData.Create(_rawDataset, true), _codec.GetDefaultParameters());
    }

    [Benchmark]
    public long CodecEncode()
    {
        var sourceDataset = _rawDataset.Clone();
        var source = DicomPixelData.Create(sourceDataset);
        var target = DicomPixelData.Create(CloneWithoutPixelData(sourceDataset, Fixture.TransferSyntax), true);

        _codec.Encode(source, target, _codecParameters);

        return target.GetFrame(0).Size;
    }

    [Benchmark]
    public long CodecDecode()
    {
        var sourceDataset = _compressedDataset.Clone();
        var source = DicomPixelData.Create(sourceDataset);
        var target = DicomPixelData.Create(CreateRawDataset(source), true);

        _codec.Decode(source, target, _codec.GetDefaultParameters());

        return target.GetFrame(0).Size;
    }

    [Benchmark]
    public long TranscoderEncode()
    {
        var output = new DicomTranscoder(
            DicomTransferSyntax.ExplicitVRLittleEndian,
            Fixture.TransferSyntax,
            outputCodecParams: _codecParameters).Transcode(_rawDataset);

        return DicomPixelData.Create(output).GetFrame(0).Size;
    }

    [Benchmark]
    public long TranscoderDecode()
    {
        var output = new DicomTranscoder(
            Fixture.TransferSyntax,
            DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(_compressedDataset);

        return DicomPixelData.Create(output).GetFrame(0).Size;
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

    private static DicomCodecParams? CreateOutputCodecParams(BenchmarkFixture fixture)
    {
        if (fixture.TransferSyntax == DicomTransferSyntax.JPEGLSNearLossless)
        {
            return new PureJpegLsParams { AllowedError = fixture.JpegLsAllowedError ?? 0 };
        }

        if (fixture.TransferSyntax == DicomTransferSyntax.JPEG2000Lossy)
        {
            return new PureJpeg2000Params
            {
                Irreversible = true,
                Rate = 16
            };
        }

        return null;
    }

    private static void ConfigureDicomServices()
    {
        if (Interlocked.Exchange(ref _servicesConfigured, 1) != 0)
        {
            return;
        }

        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();
    }
}
