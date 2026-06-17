using System.Security.Cryptography;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.NativeCodec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: htj2k-validation <source.dcm> <compressed-htj2k.dcm> <output-directory>");
    return 2;
}

var sourcePath = args[0];
var compressedPath = args[1];
var outputDirectory = args[2];
Directory.CreateDirectory(outputDirectory);

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

    var structure = ValidateStructure(compressedFile, compressedPixelData);
    Console.WriteLine($"STRUCTURE|ok|{structure}");

    var decodedDataset = new DicomTranscoder(compressedPixelData.Syntax, DicomTransferSyntax.ExplicitVRLittleEndian)
        .Transcode(compressedFile.Dataset);
    var decodedPixelData = DicomPixelData.Create(decodedDataset);
    var maxDiff = MaxSampleDifference(sourcePixelData, decodedPixelData);
    Console.WriteLine($"NATIVE-DECODE|ok|maxDiff={maxDiff}|decodedSyntax={decodedPixelData.Syntax.UID.UID}");

    var sourceRender = RenderGrayscale(sourcePixelData, preferDicomWindow: true);
    var decodedRender = RenderGrayscale(decodedPixelData, preferDicomWindow: true);
    var sourceAutoRender = RenderGrayscale(sourcePixelData, preferDicomWindow: false);
    var decodedAutoRender = RenderGrayscale(decodedPixelData, preferDicomWindow: false);
    var sourceHash = Sha256(sourceRender);
    var decodedHash = Sha256(decodedRender);
    WriteGrayscaleBmp(Path.Combine(outputDirectory, "source.bmp"), sourcePixelData.Width, sourcePixelData.Height, sourceRender);
    WriteGrayscaleBmp(Path.Combine(outputDirectory, "decoded.bmp"), decodedPixelData.Width, decodedPixelData.Height, decodedRender);
    WriteGrayscaleBmp(Path.Combine(outputDirectory, "source_auto.bmp"), sourcePixelData.Width, sourcePixelData.Height, sourceAutoRender);
    WriteGrayscaleBmp(Path.Combine(outputDirectory, "decoded_auto.bmp"), decodedPixelData.Width, decodedPixelData.Height, decodedAutoRender);
    Console.WriteLine($"RENDER|ok|sourceHash={sourceHash}|decodedHash={decodedHash}|maxDisplayDiff={MaxByteDifference(sourceRender, decodedRender)}|sourceBmp={Path.Combine(outputDirectory, "source.bmp")}|decodedBmp={Path.Combine(outputDirectory, "decoded.bmp")}");
    Console.WriteLine($"AUTO-RENDER|ok|sourceHash={Sha256(sourceAutoRender)}|decodedHash={Sha256(decodedAutoRender)}|maxDisplayDiff={MaxByteDifference(sourceAutoRender, decodedAutoRender)}|sourceBmp={Path.Combine(outputDirectory, "source_auto.bmp")}|decodedBmp={Path.Combine(outputDirectory, "decoded_auto.bmp")}");

    return maxDiff == 0 || !IsLosslessSyntax(compressedPixelData.Syntax) ? 0 : 1;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Console.Error.WriteLine($"VALIDATION|fail|{exception.GetType().Name}: {exception.Message}");
    return 1;
}

static string ValidateStructure(DicomFile file, DicomPixelData pixelData)
{
    if (!pixelData.Syntax.UID.UID.StartsWith("1.2.840.10008.1.2.4.20", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Transfer syntax is not HTJ2K.");
    }

    if (!file.Dataset.InternalTransferSyntax.IsEncapsulated)
    {
        throw new InvalidOperationException("HTJ2K PixelData is not encapsulated.");
    }

    var frame = ToArray(pixelData.GetFrame(0));
    Jpeg2000CodestreamReader.EnsureRawCodestream(frame);
    var markerCodes = new List<byte>();
    var capFound = false;
    var codFound = false;
    var qcdFound = false;
    var sotCount = 0;
    var eocFound = false;
    var offset = 0;

    while (offset < frame.Length)
    {
        if (offset + 1 >= frame.Length || frame[offset] != 0xFF)
        {
            throw new InvalidOperationException("JPEG 2000 marker prefix 0xFF was not found.");
        }

        var segment = ReadMarker(frame, offset);
        markerCodes.Add(segment.Code);
        switch (segment.Code)
        {
            case Jpeg2000Marker.CAP:
                capFound = true;
                break;
            case Jpeg2000Marker.COD:
                codFound = true;
                var cod = Jpeg2000CodingStyleDefault.Parse(segment);
                if ((cod.CodeBlockStyle & 0x40) == 0)
                {
                    throw new InvalidOperationException("COD marker does not signal HT code-blocks.");
                }

                break;
            case Jpeg2000Marker.QCD:
                qcdFound = true;
                break;
            case Jpeg2000Marker.SOT:
                sotCount++;
                var sot = Jpeg2000StartOfTilePart.Parse(segment, tileCount: 1);
                offset += 2 + ReadBeUInt16(frame, offset + 2);
                if (offset + 1 >= frame.Length || frame[offset] != 0xFF || frame[offset + 1] != Jpeg2000Marker.SOD)
                {
                    throw new InvalidOperationException("SOT marker is not followed by SOD.");
                }

                markerCodes.Add(Jpeg2000Marker.SOD);
                offset += 2;
                var tileBytes = sot.TilePartLength == 0
                    ? FindMarker(frame, Jpeg2000Marker.EOC, offset) - offset
                    : checked((int)sot.TilePartLength - 14);
                if (tileBytes < 0 || offset + tileBytes > frame.Length)
                {
                    throw new InvalidOperationException("SOT tile-part length is outside the codestream.");
                }

                offset += tileBytes;
                continue;
            case Jpeg2000Marker.SOD:
                throw new InvalidOperationException("SOD marker was found without a preceding SOT.");
            case Jpeg2000Marker.EOC:
                eocFound = true;
                break;
        }

        if (segment.Code == Jpeg2000Marker.EOC)
        {
            break;
        }

        offset += Jpeg2000Marker.HasLength(segment.Code)
            ? 2 + ReadBeUInt16(frame, offset + 2)
            : 2;
    }

    if (!capFound || !codFound || !qcdFound || sotCount == 0 || !eocFound)
    {
        throw new InvalidOperationException("Codestream is missing required HTJ2K markers.");
    }

    return $"syntax={pixelData.Syntax.UID.UID}|frame0={frame.Length}|markers={string.Join(",", markerCodes.Select(code => "FF" + code.ToString("X2")))}|tileParts={sotCount}";
}

static Jpeg2000MarkerSegment ReadMarker(byte[] codestream, int offset)
{
    var code = codestream[offset + 1];
    if (!Jpeg2000Marker.HasLength(code))
    {
        return new Jpeg2000MarkerSegment(code, Array.Empty<byte>());
    }

    var length = ReadBeUInt16(codestream, offset + 2);
    var payload = new byte[length - 2];
    Buffer.BlockCopy(codestream, offset + 4, payload, 0, payload.Length);
    return new Jpeg2000MarkerSegment(code, payload);
}

static int ReadBeUInt16(byte[] bytes, int offset)
{
    return (bytes[offset] << 8) | bytes[offset + 1];
}

static int FindMarker(byte[] bytes, byte marker, int start)
{
    for (var i = start; i < bytes.Length - 1; i++)
    {
        if (bytes[i] == 0xFF && bytes[i + 1] == marker)
        {
            return i;
        }
    }

    return -1;
}

static byte[] RenderGrayscale(DicomPixelData pixelData, bool preferDicomWindow)
{
    if (pixelData.SamplesPerPixel != 1)
    {
        throw new InvalidOperationException("The validation renderer currently supports monochrome images.");
    }

    var frame = ToArray(pixelData.GetFrame(0));
    var samples = new int[pixelData.Width * pixelData.Height];
    var min = int.MaxValue;
    var max = int.MinValue;
    var bytesPerSample = Math.Max(1, pixelData.BitsAllocated / 8);
    for (var i = 0; i < samples.Length; i++)
    {
        var sample = ReadSample(frame, i * bytesPerSample, bytesPerSample, pixelData.PixelRepresentation);
        samples[i] = sample;
        min = Math.Min(min, sample);
        max = Math.Max(max, sample);
    }

    var center = preferDicomWindow ? GetDouble(pixelData.Dataset, DicomTag.WindowCenter) : null;
    var width = preferDicomWindow ? GetDouble(pixelData.Dataset, DicomTag.WindowWidth) : null;
    var low = center.HasValue && width.HasValue ? center.Value - (width.Value / 2.0) : min;
    var high = center.HasValue && width.HasValue ? center.Value + (width.Value / 2.0) : max;
    if (high <= low)
    {
        high = low + 1;
    }

    var invert = pixelData.PhotometricInterpretation == PhotometricInterpretation.Monochrome1;
    var rendered = new byte[samples.Length];
    for (var i = 0; i < samples.Length; i++)
    {
        var normalized = (samples[i] - low) / (high - low);
        var value = (byte)Math.Round(Math.Clamp(normalized, 0.0, 1.0) * 255.0);
        rendered[i] = invert ? (byte)(255 - value) : value;
    }

    return rendered;
}

static double? GetDouble(DicomDataset dataset, DicomTag tag)
{
    return dataset.TryGetValues<double>(tag, out var values) && values.Length > 0 ? values[0] : null;
}

static int MaxSampleDifference(DicomPixelData expected, DicomPixelData actual)
{
    var max = 0;
    for (var frameIndex = 0; frameIndex < expected.NumberOfFrames; frameIndex++)
    {
        var expectedFrame = ToArray(expected.GetFrame(frameIndex));
        var actualFrame = ToArray(actual.GetFrame(frameIndex));
        var bytesPerSample = Math.Max(1, expected.BitsAllocated / 8);
        for (var offset = 0; offset + bytesPerSample <= Math.Min(expectedFrame.Length, actualFrame.Length); offset += bytesPerSample)
        {
            var expectedSample = ReadSample(expectedFrame, offset, bytesPerSample, expected.PixelRepresentation);
            var actualSample = ReadSample(actualFrame, offset, bytesPerSample, actual.PixelRepresentation);
            max = Math.Max(max, Math.Abs(expectedSample - actualSample));
        }
    }

    return max;
}

static int ReadSample(byte[] bytes, int offset, int bytesPerSample, PixelRepresentation pixelRepresentation)
{
    if (bytesPerSample == 1)
    {
        return pixelRepresentation == PixelRepresentation.Signed ? unchecked((sbyte)bytes[offset]) : bytes[offset];
    }

    var value = bytes[offset] | (bytes[offset + 1] << 8);
    return pixelRepresentation == PixelRepresentation.Signed ? unchecked((short)value) : value;
}

static byte[] ToArray(IByteBuffer buffer)
{
    var bytes = new byte[buffer.Size];
    Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
    return bytes;
}

static bool IsLosslessSyntax(DicomTransferSyntax syntax)
{
    return syntax == DicomTransferSyntax.HTJ2KLossless || syntax == DicomTransferSyntax.HTJ2KLosslessRPCL;
}

static string Sha256(byte[] bytes)
{
    return Convert.ToHexString(SHA256.HashData(bytes));
}

static int MaxByteDifference(byte[] expected, byte[] actual)
{
    var max = Math.Abs(expected.Length - actual.Length);
    for (var i = 0; i < Math.Min(expected.Length, actual.Length); i++)
    {
        max = Math.Max(max, Math.Abs(expected[i] - actual[i]));
    }

    return max;
}

static void WriteGrayscaleBmp(string path, int width, int height, byte[] pixels)
{
    const int fileHeaderSize = 14;
    const int dibHeaderSize = 40;
    const int paletteSize = 256 * 4;
    var stride = ((width + 3) / 4) * 4;
    var imageSize = stride * height;
    var pixelOffset = fileHeaderSize + dibHeaderSize + paletteSize;
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write((byte)'B');
    writer.Write((byte)'M');
    writer.Write(pixelOffset + imageSize);
    writer.Write((ushort)0);
    writer.Write((ushort)0);
    writer.Write(pixelOffset);
    writer.Write(dibHeaderSize);
    writer.Write(width);
    writer.Write(height);
    writer.Write((ushort)1);
    writer.Write((ushort)8);
    writer.Write(0);
    writer.Write(imageSize);
    writer.Write(2835);
    writer.Write(2835);
    writer.Write(256);
    writer.Write(256);
    for (var i = 0; i < 256; i++)
    {
        writer.Write((byte)i);
        writer.Write((byte)i);
        writer.Write((byte)i);
        writer.Write((byte)0);
    }

    var padding = new byte[stride - width];
    for (var y = height - 1; y >= 0; y--)
    {
        writer.Write(pixels, y * width, width);
        writer.Write(padding);
    }
}
