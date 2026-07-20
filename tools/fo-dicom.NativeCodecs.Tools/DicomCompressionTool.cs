using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.IO.Buffer;
using NativeJpeg2000Params = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000Params;

namespace FellowOakDicom.NativeCodecs.Tools;

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
            try
            {
                var outputDataset = Transcode(file.Dataset, pixelData.Syntax, item.Format.TransferSyntax);
                TrimJpegLsFrames(outputDataset, item.Format.TransferSyntax);
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
        return targetSyntax == DicomTransferSyntax.JPEG2000Lossy
            ? new NativeJpeg2000Params { Irreversible = true, Rate = 16 }
            : null;
    }

    public static byte[] TrimJpegLsFramePadding(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        for (var index = 0; index + 1 < frame.Length; index++)
        {
            if (frame[index] != 0xff || frame[index + 1] != 0xd9)
            {
                continue;
            }

            var length = index + 2;
            var trimmed = new byte[length + (length & 1)];
            Buffer.BlockCopy(frame, 0, trimmed, 0, length);
            return trimmed;
        }

        return frame;
    }

    private static void TrimJpegLsFrames(DicomDataset dataset, DicomTransferSyntax targetSyntax)
    {
        if (targetSyntax != DicomTransferSyntax.JPEGLSLossless &&
            targetSyntax != DicomTransferSyntax.JPEGLSNearLossless)
        {
            return;
        }

        var pixelData = DicomPixelData.Create(dataset);
        var frames = new byte[pixelData.NumberOfFrames][];
        var changed = false;
        for (var frame = 0; frame < frames.Length; frame++)
        {
            var original = pixelData.GetFrame(frame).Data;
            var trimmed = TrimJpegLsFramePadding(original);
            frames[frame] = trimmed;
            changed |= trimmed.Length != original.Length;
        }

        if (!changed)
        {
            return;
        }

        dataset.Remove(DicomTag.PixelData);
        var trimmedPixelData = DicomPixelData.Create(dataset, true);
        foreach (var frame in frames)
        {
            trimmedPixelData.AddFrame(new MemoryByteBuffer(frame));
        }
    }

    private static void ConfigureDicomServices()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .Build();
    }
}
