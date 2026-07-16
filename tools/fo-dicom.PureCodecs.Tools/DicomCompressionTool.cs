using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using PureHtJpeg2000Params = FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000Params;

namespace FellowOakDicom.PureCodecs.Tools;

public sealed class DicomCompressionTool
{
    public IReadOnlyList<CompressionResult> CompressAll(string inputPath, string? outputDirectory)
    {
        return Compress(inputPath, outputDirectory, targetFormat: null);
    }

    public IReadOnlyList<CompressionResult> Compress(
        string inputPath,
        string? outputDirectory,
        CompressionTargetFormat? targetFormat)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input DICOM file was not found.", inputPath);
        }

        ConfigureDicomServices();

        var file = DicomFile.Open(inputPath, FileReadOption.ReadAll);
        var pixelData = DicomPixelData.Create(file.Dataset);
        var plan = CompressionPlan.Create(inputPath, outputDirectory, file.Dataset);
        Directory.CreateDirectory(plan.OutputDirectory);

        var inputSize = new FileInfo(inputPath).Length;
        var items = targetFormat is null
            ? plan.Items
            : plan.Items.Where(item => item.Format.TransferSyntax == targetFormat.TransferSyntax).ToArray();
        var results = new List<CompressionResult>(items.Count);
        foreach (var item in items)
        {
            if (item.IsUnsupported)
            {
                results.Add(new CompressionResult(item, CompressionResultStatus.Unsupported, outputSize: null, item.SkipReason));
                continue;
            }

            try
            {
                var outputDataset = Transcode(file.Dataset, pixelData.Syntax, item.Format.TransferSyntax);
                new DicomFile(outputDataset).Save(item.OutputPath);
                var outputSize = new FileInfo(item.OutputPath).Length;
                var ratio = outputSize > 0 ? inputSize / (double)outputSize : 0.0;
                results.Add(new CompressionResult(item, CompressionResultStatus.Success, outputSize, $"{ratio:0.00}x compression"));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new CompressionResult(item, CompressionResultStatus.Failed, outputSize: null, exception.Message));
            }
        }

        return results;
    }

    private static DicomDataset Transcode(
        DicomDataset sourceDataset,
        DicomTransferSyntax sourceSyntax,
        DicomTransferSyntax targetSyntax)
    {
        if (sourceSyntax == targetSyntax)
        {
            return sourceDataset.Clone();
        }

        var transcoder = new DicomTranscoder(sourceSyntax, targetSyntax, outputCodecParams: CreateOutputCodecParams(targetSyntax));
        return transcoder.Transcode(sourceDataset);
    }

    private static DicomCodecParams? CreateOutputCodecParams(DicomTransferSyntax targetSyntax)
    {
        if (targetSyntax == DicomTransferSyntax.HTJ2K)
        {
            return new PureHtJpeg2000Params
            {
                Irreversible = true,
                TargetRatio = 16
            };
        }

        return null;
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
