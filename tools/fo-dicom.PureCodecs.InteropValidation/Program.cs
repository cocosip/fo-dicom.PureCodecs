using System.Diagnostics;
using System.Reflection;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using NativeJpegCodecParams = FellowOakDicom.Imaging.NativeCodec.DicomJpegParams;
using NativeJpeg2000Params = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000Params;
using NativeJpeg2000LosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000LosslessCodec;
using NativeJpeg2000LossyCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpeg2000LossyCodec;
using NativeJpegLsLosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLsLosslessCodec;
using NativeJpegLsNearLosslessCodec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLsNearLosslessCodec;
using NativeJpegLsParams = FellowOakDicom.Imaging.NativeCodec.DicomJpegLsCodec.DicomJpegLsParams;
using NativeJpegLossless14Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLossless14Codec;
using NativeJpegLossless14Sv1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegLossless14SV1Codec;
using NativeJpegProcess1Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess1Codec;
using NativeJpegProcess4Codec = FellowOakDicom.Imaging.NativeCodec.DicomJpegProcess4Codec;
using NativeRleCodec = FellowOakDicom.Imaging.NativeCodec.DicomRleNativeCodec;
using PureJpeg2000Params = FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000Params;

return await InteropValidationProgram.RunAsync(args);

internal static class InteropValidationProgram
{
    private const int DefaultParallelFormats = 4;

    private static readonly CodecDefinition[] CodecDefinitions =
    {
        new("rle", DicomTransferSyntax.RLELossless, () => new DicomRleLosslessCodec(), () => new NativeRleCodec(), null, SupportsAny),
        new("jpeg-process-1", DicomTransferSyntax.JPEGProcess1, () => new DicomJpegProcess1Codec(), () => new NativeJpegProcess1Codec(), 64, SupportsSequentialDct),
        new("jpeg-process-2-4", DicomTransferSyntax.JPEGProcess2_4, () => new DicomJpegProcess2_4Codec(), () => new NativeJpegProcess4Codec(), 64, SupportsSequentialDct),
        new("jpeg-lossless-14", DicomTransferSyntax.JPEGProcess14, () => new DicomJpegLossless14Codec(), () => new NativeJpegLossless14Codec(), null, SupportsAny),
        new("jpeg-lossless-14-sv1", DicomTransferSyntax.JPEGProcess14SV1, () => new DicomJpegLossless14SV1Codec(), () => new NativeJpegLossless14Sv1Codec(), null, SupportsAny),
        new("jpeg-ls-lossless", DicomTransferSyntax.JPEGLSLossless, () => new DicomJpegLsLosslessCodec(), () => new NativeJpegLsLosslessCodec(), null, SupportsAny, SupportsNativeJpegLsDecoder),
        new("jpeg-ls-near-lossless", DicomTransferSyntax.JPEGLSNearLossless, () => new DicomJpegLsNearLosslessCodec(), () => new NativeJpegLsNearLosslessCodec(), 2, SupportsAny, SupportsNativeJpegLsDecoder),
        new("jpeg2000-lossless", DicomTransferSyntax.JPEG2000Lossless, () => new DicomJpeg2000LosslessCodec(), () => new NativeJpeg2000LosslessCodec(), null, SupportsJpeg2000),
        new("jpeg2000-lossy", DicomTransferSyntax.JPEG2000Lossy, () => new DicomJpeg2000LossyCodec(), () => new NativeJpeg2000LossyCodec(), 6, SupportsJpeg2000),
    };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var repositoryRoot = options.RepositoryRoot ?? FindRepositoryRoot();
            if (options.WorkerFormat is not null)
            {
                return RunWorker(repositoryRoot, options.WorkerFormat);
            }

            return await RunOrchestratorAsync(repositoryRoot, options.ParallelFormats);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"INTEROP|fail|{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunOrchestratorAsync(string repositoryRoot, int parallelFormats)
    {
        Console.WriteLine($"INTEROP|start|formats={CodecDefinitions.Length}|parallel={parallelFormats}|root={repositoryRoot}");
        using var throttle = new SemaphoreSlim(parallelFormats, parallelFormats);
        var tasks = CodecDefinitions.Select(async definition =>
        {
            await throttle.WaitAsync();
            try
            {
                return await RunWorkerProcessAsync(repositoryRoot, definition.Key);
            }
            finally
            {
                throttle.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var result in results.OrderBy(result => result.Format, StringComparer.Ordinal))
        {
            Console.WriteLine($"INTEROP|worker|format={result.Format}|exit={result.ExitCode}|elapsedMs={result.ElapsedMilliseconds}");
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                Console.WriteLine(result.Output.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                Console.Error.WriteLine(result.Error.TrimEnd());
            }
        }

        var failures = results.Count(result => result.ExitCode != 0);
        Console.WriteLine($"INTEROP|summary|formats={results.Length}|failed={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<WorkerResult> RunWorkerProcessAsync(string repositoryRoot, string format)
    {
        var startInfo = CreateWorkerStartInfo(repositoryRoot, format);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start interop worker {format}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var stopwatch = Stopwatch.StartNew();
        await process.WaitForExitAsync();
        stopwatch.Stop();
        return new WorkerResult(format, process.ExitCode, stopwatch.ElapsedMilliseconds, await outputTask, await errorTask);
    }

    private static ProcessStartInfo CreateWorkerStartInfo(string repositoryRoot, string format)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The interop runner executable path is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }

        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add(format);
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(repositoryRoot);
        return startInfo;
    }

    private static int RunWorker(string repositoryRoot, string format)
    {
        var definition = CodecDefinitions.SingleOrDefault(item => string.Equals(item.Key, format, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown interop format '{format}'.", nameof(format));
        var executed = 0;
        foreach (var fixturePath in GetFixturePaths(repositoryRoot))
        {
            var source = LoadRaw(fixturePath);
            if (!definition.Supports(source))
            {
                continue;
            }

            ValidatePureEncodeNativeDecode(source, fixturePath, definition, definition.SupportsNativeDecoder(source));
            ValidateNativeEncodePureDecode(source, fixturePath, definition);
            executed++;
        }

        Console.WriteLine($"INTEROP|pass|format={definition.Key}|fixtures={executed}");
        return 0;
    }

    private static void ValidatePureEncodeNativeDecode(DicomPixelData source, string fixturePath, CodecDefinition definition, bool nativeDecodeSupported)
    {
        if (!nativeDecodeSupported)
        {
            Console.WriteLine($"INTEROP|skip|fixture={Path.GetFileName(fixturePath)}|format={definition.Key}|direction=pure-to-native|reason=native-jpeg-ls-multiframe-baseline-corruption");
            return;
        }

        var pureCodec = definition.CreatePureCodec();
        var nativeCodec = definition.CreateNativeCodec();
        var compressed = Encode(source, definition.Syntax, pureCodec, CreatePureEncodeParameters(definition, pureCodec));
        var decoded = Decode(compressed, nativeCodec, CreateNativeParameters(definition, nativeCodec));
        AssertDecoded(source, compressed, decoded, definition, "pure-to-native", fixturePath);
    }

    private static void ValidateNativeEncodePureDecode(DicomPixelData source, string fixturePath, CodecDefinition definition)
    {
        var nativeCodec = definition.CreateNativeCodec();
        var pureCodec = definition.CreatePureCodec();
        var compressed = Encode(source, definition.Syntax, nativeCodec, CreateNativeParameters(definition, nativeCodec));
        var decoded = Decode(compressed, pureCodec, pureCodec.GetDefaultParameters());
        AssertDecoded(source, compressed, decoded, definition, "native-to-pure", fixturePath);
    }

    private static DicomPixelData LoadRaw(string fixturePath)
    {
        var pixelData = DicomPixelData.Create(DicomFile.Open(fixturePath, FileReadOption.ReadAll).Dataset);
        if (!pixelData.Syntax.IsEncapsulated)
        {
            return pixelData;
        }

        if (pixelData.Syntax != DicomTransferSyntax.JPEGProcess14SV1)
        {
            throw new InvalidOperationException($"No Native source decoder is configured for {pixelData.Syntax.UID.Name} in {fixturePath}.");
        }

        var nativeCodec = new NativeJpegLossless14Sv1Codec();
        var decoded = CreateRawTargetPixelData(pixelData);
        nativeCodec.Decode(pixelData, decoded, nativeCodec.GetDefaultParameters());
        return decoded;
    }

    private static DicomPixelData Encode(DicomPixelData source, DicomTransferSyntax syntax, IDicomCodec codec, DicomCodecParams parameters)
    {
        var compressed = CreateTargetPixelData(source, syntax, source.PhotometricInterpretation.Value);
        codec.Encode(source, compressed, parameters);
        return compressed;
    }

    private static DicomPixelData Decode(DicomPixelData compressed, IDicomCodec codec, DicomCodecParams parameters)
    {
        var decoded = CreateRawTargetPixelData(compressed);
        codec.Decode(compressed, decoded, parameters);
        return decoded;
    }

    private static void AssertDecoded(DicomPixelData source, DicomPixelData compressed, DicomPixelData decoded, CodecDefinition definition, string direction, string fixturePath)
    {
        if (source.NumberOfFrames != compressed.NumberOfFrames || source.NumberOfFrames != decoded.NumberOfFrames)
        {
            throw new InvalidOperationException($"{direction} {Path.GetFileName(fixturePath)} {definition.Key}: frame count was not preserved.");
        }

        if (source.Width != decoded.Width || source.Height != decoded.Height)
        {
            throw new InvalidOperationException($"{direction} {Path.GetFileName(fixturePath)} {definition.Key}: decoded dimensions differ from the source.");
        }

        ValidateCompressionTags(compressed.Dataset, definition.Syntax);
        for (var frame = 0; frame < source.NumberOfFrames; frame++)
        {
            var expected = ToArray(source.GetFrame(frame));
            var actual = ToArray(decoded.GetFrame(frame));
            if (definition.Tolerance.HasValue)
            {
                AssertWithinTolerance(source, expected, actual, definition.Tolerance.Value, frame, direction, fixturePath, definition.Key);
            }
            else
            {
                AssertExact(expected, actual, frame, direction, fixturePath, definition.Key);
            }
        }
    }

    private static void AssertExact(byte[] expected, byte[] actual, int frame, string direction, string fixturePath, string format)
    {
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return;
        }

        var mismatch = FirstMismatch(expected, actual);
        throw new InvalidOperationException($"{direction} {Path.GetFileName(fixturePath)} {format}: frame {frame} byte {mismatch} differs. Expected={ByteAt(expected, mismatch)}, actual={ByteAt(actual, mismatch)}.");
    }

    private static void AssertWithinTolerance(DicomPixelData source, byte[] expected, byte[] actual, int tolerance, int frame, string direction, string fixturePath, string format)
    {
        if (expected.Length != actual.Length)
        {
            throw new InvalidOperationException($"{direction} {Path.GetFileName(fixturePath)} {format}: frame {frame} length differs.");
        }

        var bytesPerSample = source.BitsAllocated / 8;
        for (var offset = 0; offset < expected.Length; offset += bytesPerSample)
        {
            var expectedSample = ReadSample(expected, offset, source.BitsAllocated, source.PixelRepresentation);
            var actualSample = ReadSample(actual, offset, source.BitsAllocated, source.PixelRepresentation);
            var difference = Math.Abs(expectedSample - actualSample);
            if (difference > tolerance)
            {
                throw new InvalidOperationException($"{direction} {Path.GetFileName(fixturePath)} {format}: frame {frame} sample {offset / bytesPerSample} differs by {difference}, tolerance {tolerance}. Expected={expectedSample}, actual={actualSample}.");
            }
        }
    }

    private static void ValidateCompressionTags(DicomDataset dataset, DicomTransferSyntax syntax)
    {
        if (dataset.InternalTransferSyntax != syntax)
        {
            throw new InvalidOperationException($"Compressed dataset syntax is {dataset.InternalTransferSyntax.UID.UID}, expected {syntax.UID.UID}.");
        }

        foreach (var tag in new[] { DicomTag.PhotometricInterpretation, DicomTag.SamplesPerPixel, DicomTag.Rows, DicomTag.Columns, DicomTag.BitsAllocated, DicomTag.BitsStored, DicomTag.HighBit, DicomTag.PixelRepresentation, DicomTag.PixelData })
        {
            if (!dataset.Contains(tag))
            {
                throw new InvalidOperationException($"Compressed dataset is missing required tag {tag}.");
            }
        }
    }

    private static DicomCodecParams CreatePureEncodeParameters(CodecDefinition definition, IDicomCodec pureCodec)
    {
        var parameters = pureCodec.GetDefaultParameters();
        if (parameters is DicomJpegLsParams jpegLsParameters && definition.Syntax == DicomTransferSyntax.JPEGLSNearLossless)
        {
            jpegLsParameters.AllowedError = definition.Tolerance!.Value;
        }

        if (parameters is PureJpeg2000Params jpeg2000Parameters && definition.Syntax == DicomTransferSyntax.JPEG2000Lossy)
        {
            jpeg2000Parameters.Irreversible = true;
            jpeg2000Parameters.Rate = 0;
        }

        return parameters;
    }

    private static DicomCodecParams CreateNativeParameters(CodecDefinition definition, IDicomCodec nativeCodec)
    {
        if (definition.Syntax == DicomTransferSyntax.JPEG2000Lossy)
        {
            return new NativeJpeg2000Params { Irreversible = true, Rate = 0 };
        }

        var parameters = nativeCodec.GetDefaultParameters();
        if (parameters is NativeJpegCodecParams jpegParameters)
        {
            jpegParameters.ConvertColorSpaceToRGB = true;
        }

        if (parameters is NativeJpegLsParams jpegLsParameters && definition.Syntax == DicomTransferSyntax.JPEGLSNearLossless)
        {
            jpegLsParameters.AllowedError = definition.Tolerance!.Value;
        }

        return parameters;
    }

    private static DicomPixelData CreateRawTargetPixelData(DicomPixelData source)
    {
        var photometricInterpretation = source.SamplesPerPixel == 3 ? PhotometricInterpretation.Rgb.Value : source.PhotometricInterpretation.Value;
        return CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian, photometricInterpretation);
    }

    private static DicomPixelData CreateTargetPixelData(DicomPixelData source, DicomTransferSyntax syntax, string photometricInterpretation)
    {
        var dataset = new DicomDataset(syntax)
        {
            { DicomTag.SOPClassUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage) },
            { DicomTag.SOPInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, DicomUID.Generate()) },
            { DicomTag.StudyInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, DicomUID.Generate()) },
            { DicomTag.SeriesInstanceUID, source.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, DicomUID.Generate()) },
            { DicomTag.PhotometricInterpretation, photometricInterpretation },
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

    private static IReadOnlyList<string> GetFixturePaths(string repositoryRoot)
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (!Directory.Exists(fixtureDirectory))
        {
            fixtureDirectory = Path.Combine(repositoryRoot, "tools", "fo-dicom.PureCodecs.InteropValidation", "Fixtures");
        }

        var names = new[] { "sample-01.dcm", "sample-02.dcm", "sample-03.dcm", "sample-04.dcm", "sample-05.dcm" };
        return names.Select(name => Path.Combine(fixtureDirectory, name)).Select(path => File.Exists(path) ? path : throw new FileNotFoundException("Interop fixture was not found.", path)).ToArray();
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "tools", "fo-dicom.PureCodecs.InteropValidation", "Fixtures")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("The repository root containing the bundled interop fixtures was not found.");
    }

    private static bool SupportsAny(DicomPixelData source) => source.BitsAllocated is 8 or 16 && source.SamplesPerPixel is 1 or 3;

    private static bool SupportsSequentialDct(DicomPixelData source) => source.BitsAllocated == 8 && source.BitsStored == 8 && source.SamplesPerPixel is 1 or 3;

    private static bool SupportsJpeg2000(DicomPixelData source) => source.BitsAllocated is 8 or 16 && source.SamplesPerPixel is 1 or 3;

    private static bool SupportsNativeJpegLsDecoder(DicomPixelData source) => SupportsAny(source) && source.NumberOfFrames == 1;

    private static byte[] ToArray(IByteBuffer buffer)
    {
        var bytes = new byte[buffer.Size];
        Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int ReadSample(byte[] bytes, int offset, int bitsAllocated, PixelRepresentation pixelRepresentation)
    {
        if (bitsAllocated == 8)
        {
            return pixelRepresentation == PixelRepresentation.Signed ? unchecked((sbyte)bytes[offset]) : bytes[offset];
        }

        var value = bytes[offset] | (bytes[offset + 1] << 8);
        return pixelRepresentation == PixelRepresentation.Signed ? unchecked((short)value) : value;
    }

    private static int FirstMismatch(byte[] expected, byte[] actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < length; index++)
        {
            if (expected[index] != actual[index])
            {
                return index;
            }
        }

        return length;
    }

    private static string ByteAt(byte[] bytes, int index) => index < bytes.Length ? bytes[index].ToString() : "<end>";

    private sealed record CodecDefinition(
        string Key,
        DicomTransferSyntax Syntax,
        Func<IDicomCodec> CreatePureCodec,
        Func<IDicomCodec> CreateNativeCodec,
        int? Tolerance,
        Func<DicomPixelData, bool> Supports,
        Func<DicomPixelData, bool>? NativeDecoderSupports = null)
    {
        public Func<DicomPixelData, bool> SupportsNativeDecoder => NativeDecoderSupports ?? Supports;
    }

    private sealed record WorkerResult(string Format, int ExitCode, long ElapsedMilliseconds, string Output, string Error);

    private sealed record Options(string? WorkerFormat, string? RepositoryRoot, int ParallelFormats)
    {
        public static Options Parse(string[] args)
        {
            string? workerFormat = null;
            string? repositoryRoot = null;
            var parallelFormats = DefaultParallelFormats;
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--worker":
                        workerFormat = NextValue(args, ref index, "--worker");
                        break;
                    case "--root":
                        repositoryRoot = Path.GetFullPath(NextValue(args, ref index, "--root"));
                        break;
                    case "--parallel":
                        if (!int.TryParse(NextValue(args, ref index, "--parallel"), out parallelFormats) || parallelFormats < 1)
                        {
                            throw new ArgumentException("--parallel must be a positive integer.");
                        }

                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{args[index]}'. Use --worker <format>, --root <path>, or --parallel <count>.");
                }
            }

            return new Options(workerFormat, repositoryRoot, parallelFormats);
        }

        private static string NextValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length)
            {
                throw new ArgumentException($"{option} requires a value.");
            }

            return args[index];
        }
    }
}
