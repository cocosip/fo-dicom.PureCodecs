using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;

if (args.Length < 3)
{
    Console.Error.WriteLine(
        "Usage: htj2k-validation <source.dcm> <compressed-htj2k.dcm> <output-directory> " +
        "[--lossy-tolerance <samples>]");
    return 2;
}

var sourcePath = args[0];
var compressedPath = args[1];
var outputDirectory = args[2];
int? lossyTolerance = null;
for (var index = 3; index < args.Length; index++)
{
    if (args[index] != "--lossy-tolerance"
        || ++index >= args.Length
        || !int.TryParse(args[index], out var parsedTolerance)
        || parsedTolerance < 0)
    {
        Console.Error.WriteLine("VALIDATION|failed|--lossy-tolerance requires a non-negative integer.");
        return 2;
    }

    lossyTolerance = parsedTolerance;
}

new DicomSetupBuilder()
    .RegisterServices(services => services
        .AddFellowOakDicom()
        .AddTranscoderManager<NativeTranscoderManager>())
    .SkipValidation()
    .Build();

try
{
    var sourceFile = DicomFile.Open(sourcePath, FileReadOption.ReadAll);
    var compressedFile = DicomFile.Open(compressedPath, FileReadOption.ReadAll);
    var sourcePixelData = DicomPixelData.Create(sourceFile.Dataset);
    var compressedPixelData = DicomPixelData.Create(compressedFile.Dataset);
    ValidateCompressedDataset(sourcePixelData, compressedPixelData, compressedFile.Dataset);

    var lossless = IsLosslessSyntax(compressedPixelData.Syntax);
    if (!lossless && !lossyTolerance.HasValue)
    {
        throw new InvalidOperationException("HTJ2K lossy validation requires --lossy-tolerance <samples>.");
    }

    var decodedDataset = new DicomTranscoder(
            compressedPixelData.Syntax,
            DicomTransferSyntax.ExplicitVRLittleEndian)
        .Transcode(compressedFile.Dataset);

    Directory.CreateDirectory(outputDirectory);
    var decodedPath = Path.Combine(outputDirectory, "decoded.dcm");
    new DicomFile(decodedDataset).Save(decodedPath);

    var decodedPixelData = DicomPixelData.Create(decodedDataset);
    ValidateComparablePixelData(sourcePixelData, decodedPixelData);
    var maxDifference = MaxSampleDifference(sourcePixelData, decodedPixelData);
    var tolerance = lossless ? 0 : lossyTolerance!.Value;
    if (maxDifference > tolerance)
    {
        throw new InvalidDataException(
            $"Maximum sample difference {maxDifference} exceeds tolerance {tolerance}.");
    }

    Console.WriteLine(
        $"VALIDATION|passed|maxDiff={maxDifference}|tolerance={tolerance}" +
        $"|frames={decodedPixelData.NumberOfFrames}|decoded={decodedPath}");
    return 0;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Console.Error.WriteLine($"VALIDATION|failed|{exception.GetType().Name}: {exception.Message}");
    return 1;
}

static void ValidateCompressedDataset(
    DicomPixelData source,
    DicomPixelData compressed,
    DicomDataset compressedDataset)
{
    if (compressed.Syntax != DicomTransferSyntax.HTJ2KLossless
        && compressed.Syntax != DicomTransferSyntax.HTJ2KLosslessRPCL
        && compressed.Syntax != DicomTransferSyntax.HTJ2K)
    {
        throw new InvalidOperationException("Transfer syntax is not HTJ2K.");
    }

    if (!compressedDataset.InternalTransferSyntax.IsEncapsulated)
    {
        throw new InvalidOperationException("HTJ2K PixelData is not encapsulated.");
    }

    if (source.NumberOfFrames != compressed.NumberOfFrames)
    {
        throw new InvalidOperationException(
            $"Compressed frame count {compressed.NumberOfFrames} differs from source frame count {source.NumberOfFrames}.");
    }
}

static void ValidateComparablePixelData(DicomPixelData expected, DicomPixelData actual)
{
    if (expected.NumberOfFrames != actual.NumberOfFrames)
    {
        throw new InvalidOperationException(
            $"Decoded frame count {actual.NumberOfFrames} differs from source frame count {expected.NumberOfFrames}.");
    }

    if (expected.Width != actual.Width || expected.Height != actual.Height)
    {
        throw new InvalidOperationException(
            $"Decoded dimensions {actual.Width}x{actual.Height} differ from source dimensions {expected.Width}x{expected.Height}.");
    }

    if (expected.BitsAllocated != actual.BitsAllocated
        || expected.BitsStored != actual.BitsStored
        || expected.HighBit != actual.HighBit
        || expected.PixelRepresentation != actual.PixelRepresentation
        || expected.SamplesPerPixel != actual.SamplesPerPixel
        || expected.PhotometricInterpretation != actual.PhotometricInterpretation)
    {
        throw new InvalidOperationException("Decoded pixel metadata differs from the source.");
    }

    for (var frameIndex = 0; frameIndex < expected.NumberOfFrames; frameIndex++)
    {
        var expectedLength = expected.GetFrame(frameIndex).Size;
        var actualLength = actual.GetFrame(frameIndex).Size;
        if (expectedLength != actualLength)
        {
            throw new InvalidOperationException(
                $"Decoded frame {frameIndex} length {actualLength} differs from source length {expectedLength}.");
        }
    }
}

static int MaxSampleDifference(DicomPixelData expected, DicomPixelData actual)
{
    var bytesPerSample = Math.Max(1, expected.BitsAllocated / 8);
    if (bytesPerSample is not 1 and not 2)
    {
        throw new NotSupportedException($"Bits Allocated {expected.BitsAllocated} is not supported by this validator.");
    }

    var maximum = 0;
    for (var frameIndex = 0; frameIndex < expected.NumberOfFrames; frameIndex++)
    {
        var expectedFrame = expected.GetFrame(frameIndex);
        var actualFrame = actual.GetFrame(frameIndex);
        for (var offset = 0; offset < expectedFrame.Size; offset += bytesPerSample)
        {
            var expectedSample = ReadSample(
                expectedFrame.Data,
                offset,
                bytesPerSample,
                expected.PixelRepresentation);
            var actualSample = ReadSample(
                actualFrame.Data,
                offset,
                bytesPerSample,
                actual.PixelRepresentation);
            maximum = Math.Max(maximum, Math.Abs(expectedSample - actualSample));
        }
    }

    return maximum;
}

static int ReadSample(
    byte[] bytes,
    int offset,
    int bytesPerSample,
    PixelRepresentation pixelRepresentation)
{
    if (bytesPerSample == 1)
    {
        return pixelRepresentation == PixelRepresentation.Signed
            ? unchecked((sbyte)bytes[offset])
            : bytes[offset];
    }

    var value = bytes[offset] | (bytes[offset + 1] << 8);
    return pixelRepresentation == PixelRepresentation.Signed
        ? unchecked((short)value)
        : value;
}

static bool IsLosslessSyntax(DicomTransferSyntax syntax)
{
    return syntax == DicomTransferSyntax.HTJ2KLossless
        || syntax == DicomTransferSyntax.HTJ2KLosslessRPCL;
}
