using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
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
            Console.WriteLine("HTJ2K_REFERENCE|ok");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"HTJ2K_REFERENCE|fail|{exception.GetType().Name}: {exception.Message}");
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
        var sourceFile = DicomFile.Open(options.InputPath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(sourceFile.Dataset);
        if (source.Syntax.IsEncapsulated)
        {
            throw new InvalidDataException("HTJ2K reference input must have uncompressed PixelData.");
        }

        var codec = CreateCodec(options.TransferSyntax);
        var compressed = DicomPixelData.Create(CloneForTransferSyntax(source.Dataset, codec.TransferSyntax), true);
        codec.Encode(source, compressed, codec.GetDefaultParameters());
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
        var encodedFrames = new List<EncodedFrame>(source.NumberOfFrames);
        for (var frameIndex = 0; frameIndex < source.NumberOfFrames; frameIndex++)
        {
            var rawFrame = source.GetFrame(frameIndex).Data;
            var compressedFrame = compressed.GetFrame(frameIndex);
            var logicalCodestream = ExtractLogicalCodestream(compressedFrame, frameIndex);
            File.WriteAllBytes(Path.Combine(outputDirectory, frameIndex + ".j2c"), logicalCodestream);
            encodedFrames.Add(new EncodedFrame(
                rawFrame,
                logicalCodestream,
                Htj2kReferenceManifestBuilder.ReadMarkerSummary(logicalCodestream)));
        }

        var decoded = DicomPixelData.Create(CloneForTransferSyntax(source.Dataset, DicomTransferSyntax.ExplicitVRLittleEndian), true);
        codec.Decode(compressed, decoded, codec.GetDefaultParameters());
        if (source.NumberOfFrames != decoded.NumberOfFrames)
        {
            throw new InvalidDataException("HTJ2K reference codec did not preserve decoded frame count.");
        }

        var frames = new List<Htj2kReferenceFrame>(source.NumberOfFrames);
        for (var frameIndex = 0; frameIndex < source.NumberOfFrames; frameIndex++)
        {
            var encodedFrame = encodedFrames[frameIndex];
            var decodedFrame = decoded.GetFrame(frameIndex).Data;
            frames.Add(new Htj2kReferenceFrame(
                frameIndex,
                Htj2kReferenceManifestBuilder.ComputeSha256(encodedFrame.RawFrame),
                Htj2kReferenceManifestBuilder.ComputeSha256(encodedFrame.LogicalCodestream),
                Htj2kReferenceManifestBuilder.ComputeSha256(decodedFrame),
                encodedFrame.LogicalCodestream.Length,
                encodedFrame.MarkerSummary));
        }

        var dependencyManifestPath = Path.ChangeExtension(
            typeof(Htj2kReferenceWorkerProgram).Assembly.Location,
            ".deps.json");
        var resolvedPackageVersion = Htj2kReferencePackageVersionReader.ReadResolvedVersion(
            dependencyManifestPath,
            "fo-dicom.Codecs");
        var provenance = Htj2kReferenceProvenanceReader.ReadAndValidate(
            codec.GetType().Assembly,
            resolvedPackageVersion);
        var codestreamReportedVersion = Htj2kReferenceManifestBuilder.ReadCodestreamReportedOpenJphVersion(
            encodedFrames[0].LogicalCodestream);
        var effectiveParameters = Htj2kReferenceManifestBuilder.ReadEffectiveParameters(encodedFrames[0].LogicalCodestream);
        foreach (var encodedFrame in encodedFrames)
        {
            if (Htj2kReferenceManifestBuilder.ReadCodestreamReportedOpenJphVersion(encodedFrame.LogicalCodestream) != codestreamReportedVersion
                || Htj2kReferenceManifestBuilder.ReadEffectiveParameters(encodedFrame.LogicalCodestream) != effectiveParameters)
            {
                throw new InvalidDataException("HTJ2K reference frames report inconsistent provenance or effective parameters.");
            }
        }

        var manifest = new Htj2kReferenceManifest(
            provenance.PackageVersion,
            provenance.ReleaseCommit,
            codestreamReportedVersion,
            codec.TransferSyntax.UID.UID,
            source.NumberOfFrames,
            effectiveParameters,
            frames);
        File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] ExtractLogicalCodestream(FellowOakDicom.IO.Buffer.IByteBuffer frame, int frameIndex)
    {
        try
        {
            return Htj2kReferenceManifestBuilder.ExtractLogicalCodestream(frame.Data);
        }
        catch (InvalidDataException exception)
        {
            var data = frame.Data;
            throw new InvalidDataException(
                $"HTJ2K reference frame {frameIndex} does not expose EOC. BufferSize={frame.Size}, DataLength={data.Length}.",
                exception);
        }
    }

    private static IDicomCodec CreateCodec(DicomTransferSyntax transferSyntax)
    {
        if (transferSyntax == DicomTransferSyntax.HTJ2KLossless)
        {
            return new DicomHtJpeg2000LosslessCodec();
        }

        if (transferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL)
        {
            return new DicomHtJpeg2000LosslessRPCLCodec();
        }

        if (transferSyntax == DicomTransferSyntax.HTJ2K)
        {
            return new DicomHtJpeg2000LossyCodec();
        }

        throw new ArgumentOutOfRangeException(nameof(transferSyntax), "Unsupported HTJ2K transfer syntax.");
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

            if (!worker || string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(syntax) || string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Usage: --worker --input <raw-dicom> --syntax <201|202|203> --output <manifest.json>.");
            }

            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("HTJ2K reference input file was not found.");
            }

            return new Options(Path.GetFullPath(inputPath), ParseTransferSyntax(syntax), Path.GetFullPath(outputPath));
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
                _ => throw new ArgumentOutOfRangeException(nameof(value), "HTJ2K syntax must be 201, 202, or 203.")
            };
        }
    }

    private sealed record EncodedFrame(byte[] RawFrame, byte[] LogicalCodestream, Htj2kMarkerSummary MarkerSummary);
}
