using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Htj2kReference;

return Htj2kReferenceWorkerProgram.Run(args);

public static class Htj2kReferenceWorkerProgram
{
    public static int Run(string[] args)
    {
        try
        {
            DelayForWorkerContractTest(args);
            var options = Options.Parse(args);
            WriteReference(options);
            Console.WriteLine("HTJ2K_REFERENCE|passed");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"HTJ2K_REFERENCE|failed|{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static void DelayForWorkerContractTest(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] != "--delay-ms")
            {
                continue;
            }

            if (++index >= args.Length
                || !int.TryParse(args[index], out var delayMilliseconds)
                || delayMilliseconds < 0
                || delayMilliseconds > 60000)
            {
                throw new ArgumentException("--delay-ms requires a value from 0 through 60000.");
            }

            Thread.Sleep(delayMilliseconds);
            return;
        }
    }

    private static void WriteReference(Options options)
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var sourceFile = DicomFile.Open(options.InputPath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(sourceFile.Dataset);
        if (source.Syntax.IsEncapsulated)
        {
            throw new InvalidDataException("HTJ2K reference input must have uncompressed PixelData.");
        }

        var compressedDataset = new DicomTranscoder(source.Syntax, options.TransferSyntax)
            .Transcode(sourceFile.Dataset);
        var compressed = DicomPixelData.Create(compressedDataset);
        if (source.NumberOfFrames != compressed.NumberOfFrames)
        {
            throw new InvalidDataException("HTJ2K reference codec did not preserve frame count.");
        }

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Reference manifest output path must include a directory.", nameof(options));
        }

        Directory.CreateDirectory(outputDirectory);
        new DicomFile(compressedDataset).Save(Path.Combine(outputDirectory, "reference.dcm"));

        var encodedFrames = new byte[source.NumberOfFrames][];
        for (var frameIndex = 0; frameIndex < source.NumberOfFrames; frameIndex++)
        {
            encodedFrames[frameIndex] = ReadFrameBytes(compressed.GetFrame(frameIndex));
            File.WriteAllBytes(Path.Combine(outputDirectory, frameIndex + ".j2c"), encodedFrames[frameIndex]);
        }

        var decodedDataset = new DicomTranscoder(
                options.TransferSyntax,
                DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(compressedDataset);
        var decoded = DicomPixelData.Create(decodedDataset);
        if (source.NumberOfFrames != decoded.NumberOfFrames)
        {
            throw new InvalidDataException("HTJ2K reference codec did not preserve decoded frame count.");
        }

        var frames = new Htj2kReferenceFrame[source.NumberOfFrames];
        for (var frameIndex = 0; frameIndex < source.NumberOfFrames; frameIndex++)
        {
            var rawFrame = ReadFrameBytes(source.GetFrame(frameIndex));
            var decodedFrame = ReadFrameBytes(decoded.GetFrame(frameIndex));
            frames[frameIndex] = new Htj2kReferenceFrame(
                frameIndex,
                Htj2kReferenceManifestBuilder.ComputeSha256(rawFrame),
                Htj2kReferenceManifestBuilder.ComputeSha256(encodedFrames[frameIndex]),
                Htj2kReferenceManifestBuilder.ComputeSha256(decodedFrame),
                encodedFrames[frameIndex].Length);
        }

        var manifest = new Htj2kReferenceManifest(
            options.TransferSyntax.UID.UID,
            source.NumberOfFrames,
            frames);
        File.WriteAllText(
            options.OutputPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] ReadFrameBytes(IByteBuffer frame)
    {
        var bytes = new byte[frame.Size];
        Buffer.BlockCopy(frame.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private sealed record Options(string InputPath, DicomTransferSyntax TransferSyntax, string OutputPath)
    {
        public static Options Parse(string[] args)
        {
            string? inputPath = null;
            string? syntax = null;
            string? outputPath = null;
            var worker = false;
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--worker":
                        worker = true;
                        break;
                    case "--input":
                        inputPath = NextValue(args, ref index, "--input");
                        break;
                    case "--syntax":
                        syntax = NextValue(args, ref index, "--syntax");
                        break;
                    case "--output":
                        outputPath = NextValue(args, ref index, "--output");
                        break;
                    case "--delay-ms":
                        NextValue(args, ref index, "--delay-ms");
                        break;
                    default:
                        throw new ArgumentException("Unknown HTJ2K reference worker option.");
                }
            }

            if (!worker
                || string.IsNullOrWhiteSpace(inputPath)
                || string.IsNullOrWhiteSpace(syntax)
                || string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "Usage: --worker --input <raw-dicom> --syntax <201|202|203> --output <manifest.json>.");
            }

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("HTJ2K reference input file was not found.");
            }

            return new Options(
                Path.GetFullPath(inputPath),
                ParseTransferSyntax(syntax),
                Path.GetFullPath(outputPath));
        }

        private static string NextValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length)
            {
                throw new ArgumentException(option + " requires a value.");
            }

            return args[index];
        }

        private static DicomTransferSyntax ParseTransferSyntax(string value)
        {
            return value switch
            {
                "201" => DicomTransferSyntax.HTJ2KLossless,
                "202" => DicomTransferSyntax.HTJ2KLosslessRPCL,
                "203" => DicomTransferSyntax.HTJ2K,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "HTJ2K syntax must be 201, 202, or 203.")
            };
        }
    }
}
