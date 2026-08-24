using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;

namespace FellowOakDicom.NativeCodecs.Tools;

public static class Htj2kNativeWorkerProgram
{
    public static int Run(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            Transcode(options);
            Console.WriteLine("NATIVE_HTJ2K|ok");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"NATIVE_HTJ2K|fail|{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static void Transcode(Options options)
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            .SkipValidation()
            .Build();

        var sourceFile = DicomFile.Open(options.InputPath, FileReadOption.ReadAll);
        var sourceSyntax = sourceFile.Dataset.InternalTransferSyntax;
        var outputDataset = sourceSyntax == options.TargetSyntax
            ? sourceFile.Dataset.Clone()
            : new DicomTranscoder(sourceSyntax, options.TargetSyntax).Transcode(sourceFile.Dataset);
        DicomCompressionTool.TrimHtj2kFrames(outputDataset, options.TargetSyntax);

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Native HTJ2K output path must include a directory.");
        }

        Directory.CreateDirectory(outputDirectory);
        new DicomFile(outputDataset).Save(options.OutputPath);
    }

    private sealed record Options(string InputPath, string OutputPath, DicomTransferSyntax TargetSyntax)
    {
        public static Options Parse(string[] args)
        {
            string? inputPath = null;
            string? outputPath = null;
            string? syntax = null;
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
                    case "--output":
                        outputPath = NextValue(args, ref index, "--output");
                        break;
                    case "--syntax":
                        syntax = NextValue(args, ref index, "--syntax");
                        break;
                    default:
                        throw new ArgumentException("Unknown native HTJ2K worker option.");
                }
            }

            if (!worker || string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(syntax))
            {
                throw new ArgumentException("Usage: --worker --input <dicom> --output <dicom> --syntax <raw|201|202|203>.");
            }

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Native HTJ2K worker input file was not found.");
            }

            return new Options(
                Path.GetFullPath(inputPath),
                Path.GetFullPath(outputPath),
                ParseTransferSyntax(syntax));
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
                "raw" => DicomTransferSyntax.ExplicitVRLittleEndian,
                "201" => DicomTransferSyntax.HTJ2KLossless,
                "202" => DicomTransferSyntax.HTJ2KLosslessRPCL,
                "203" => DicomTransferSyntax.HTJ2K,
                _ => throw new ArgumentOutOfRangeException(nameof(value), "Native HTJ2K syntax must be raw, 201, 202, or 203.")
            };
        }
    }
}
