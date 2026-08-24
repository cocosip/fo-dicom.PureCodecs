using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.PureCodecs.Htj2kReference;

return Htj2kReferenceWorkerProgram.Run(args);

internal static class Htj2kReferenceWorkerProgram
{
    private const string ReferencePackageVersion = "5.16.7";
    private const string ReferenceReleaseCommit = "1d05c6cca14883d06b835f8dadca5dae7d97577c";
    private const string CodestreamReportedOpenJphVersion = "0.21.2";

    public static int Run(string[] args)
    {
        try
        {
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
            encodedFrames.Add(new EncodedFrame(rawFrame, logicalCodestream, ReadMarkerSummary(logicalCodestream)));
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

        var manifest = new Htj2kReferenceManifest(
            ReferencePackageVersion,
            ReferenceReleaseCommit,
            CodestreamReportedOpenJphVersion,
            codec.TransferSyntax.UID.UID,
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

    private static Htj2kMarkerSummary ReadMarkerSummary(byte[] codestream)
    {
        var markerCodes = new List<string>();
        var tilePartCount = 0;
        var offset = 0;
        while (offset < codestream.Length)
        {
            RequireMarker(codestream, offset);
            var marker = codestream[offset + 1];
            markerCodes.Add("FF" + marker.ToString("X2"));
            if (marker == 0xD9)
            {
                break;
            }

            if (marker == 0x90)
            {
                tilePartCount++;
                offset = SkipTilePart(codestream, offset, markerCodes);
                continue;
            }

            offset += MarkerLength(codestream, offset, marker);
        }

        if (markerCodes.Count == 0 || markerCodes[^1] != "FFD9")
        {
            throw new InvalidDataException("HTJ2K codestream marker sequence does not end with EOC.");
        }

        return new Htj2kMarkerSummary(markerCodes, tilePartCount);
    }

    private static int SkipTilePart(byte[] codestream, int offset, List<string> markerCodes)
    {
        if (offset + 12 > codestream.Length || ReadUInt16(codestream, offset + 2) != 10)
        {
            throw new InvalidDataException("HTJ2K SOT marker is invalid.");
        }

        var tilePartLength = ReadUInt32(codestream, offset + 6);
        if (tilePartLength < 14 || tilePartLength > codestream.Length - offset)
        {
            throw new InvalidDataException("HTJ2K SOT length is invalid.");
        }

        var tilePartEnd = checked(offset + (int)tilePartLength);
        offset += 12;
        while (offset < tilePartEnd)
        {
            RequireMarker(codestream, offset);
            var marker = codestream[offset + 1];
            markerCodes.Add("FF" + marker.ToString("X2"));
            if (marker == 0x93)
            {
                return tilePartEnd;
            }

            offset += MarkerLength(codestream, offset, marker);
        }

        throw new InvalidDataException("HTJ2K tile part does not contain SOD.");
    }

    private static int MarkerLength(byte[] codestream, int offset, byte marker)
    {
        if (marker is 0x4F or 0x92 or 0x93 or 0xD9)
        {
            return 2;
        }

        if (offset + 4 > codestream.Length)
        {
            throw new InvalidDataException("HTJ2K marker length is outside the codestream.");
        }

        var length = ReadUInt16(codestream, offset + 2);
        if (length < 2 || length > codestream.Length - offset - 2)
        {
            throw new InvalidDataException("HTJ2K marker length is invalid.");
        }

        return 2 + length;
    }

    private static void RequireMarker(byte[] codestream, int offset)
    {
        if (offset + 1 >= codestream.Length || codestream[offset] != 0xFF)
        {
            throw new InvalidDataException("HTJ2K marker prefix is invalid.");
        }
    }

    private static int ReadUInt16(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

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
