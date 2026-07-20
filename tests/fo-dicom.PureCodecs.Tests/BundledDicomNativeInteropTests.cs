using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using FellowOakDicom.PureCodecs.Tests.TestSupport;
using Xunit;
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

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class BundledDicomNativeInteropTests
{
    public static TheoryData<InteropCase> InteropCases
    {
        get
        {
            var data = new TheoryData<InteropCase>();
            foreach (var fixturePath in RegressionFixturePaths.InteropFixtures())
            {
                var source = LoadRaw(fixturePath);
                foreach (var definition in CodecDefinitions)
                {
                    if (definition.Supports(source))
                    {
                        data.Add(new InteropCase(
                            Path.GetFileName(fixturePath),
                            fixturePath,
                            definition.Syntax,
                            definition.CreatePureCodec(),
                            definition.CreateNativeCodec(),
                            definition.Tolerance));
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(InteropCases))]
    public void Pure_encode_native_decode_interoperates(InteropCase testCase)
    {
        var source = LoadRaw(testCase.FixturePath);
        var compressed = Encode(source, testCase.Syntax, testCase.PureCodec, CreatePureEncodeParameters(testCase));
        var decoded = Decode(compressed, testCase.NativeCodec, CreateNativeParameters(testCase));

        AssertDecoded(source, compressed, decoded, testCase);
    }

    [Theory]
    [MemberData(nameof(InteropCases))]
    public void Native_encode_pure_decode_interoperates(InteropCase testCase)
    {
        var source = LoadRaw(testCase.FixturePath);
        var compressed = Encode(source, testCase.Syntax, testCase.NativeCodec, CreateNativeParameters(testCase));
        var decoded = Decode(compressed, testCase.PureCodec, testCase.PureCodec.GetDefaultParameters());

        AssertDecoded(source, compressed, decoded, testCase);
    }

    private static readonly CodecDefinition[] CodecDefinitions =
    {
        new(DicomTransferSyntax.RLELossless, () => new DicomRleLosslessCodec(), () => new NativeRleCodec(), Tolerance: null, SupportsAny),
        new(DicomTransferSyntax.JPEGProcess1, () => new DicomJpegProcess1Codec(), () => new NativeJpegProcess1Codec(), Tolerance: 48, SupportsSequentialDct),
        new(DicomTransferSyntax.JPEGProcess2_4, () => new DicomJpegProcess2_4Codec(), () => new NativeJpegProcess4Codec(), Tolerance: 48, SupportsSequentialDct),
        new(DicomTransferSyntax.JPEGProcess14, () => new DicomJpegLossless14Codec(), () => new NativeJpegLossless14Codec(), Tolerance: null, SupportsAny),
        new(DicomTransferSyntax.JPEGProcess14SV1, () => new DicomJpegLossless14SV1Codec(), () => new NativeJpegLossless14Sv1Codec(), Tolerance: null, SupportsAny),
        new(DicomTransferSyntax.JPEGLSLossless, () => new DicomJpegLsLosslessCodec(), () => new NativeJpegLsLosslessCodec(), Tolerance: null, SupportsAny),
        new(DicomTransferSyntax.JPEGLSNearLossless, () => new DicomJpegLsNearLosslessCodec(), () => new NativeJpegLsNearLosslessCodec(), Tolerance: 2, SupportsAny),
        new(DicomTransferSyntax.JPEG2000Lossless, () => new DicomJpeg2000LosslessCodec(), () => new NativeJpeg2000LosslessCodec(), Tolerance: null, SupportsJpeg2000),
        new(DicomTransferSyntax.JPEG2000Lossy, () => new DicomJpeg2000LossyCodec(), () => new NativeJpeg2000LossyCodec(), Tolerance: 6, SupportsJpeg2000),
    };

    private static DicomPixelData LoadRaw(string fixturePath)
    {
        var file = DicomFile.Open(fixturePath, FileReadOption.ReadAll);
        var source = DicomPixelData.Create(file.Dataset);
        if (!source.Syntax.IsEncapsulated)
        {
            return source;
        }

        var nativeCodec = source.Syntax == DicomTransferSyntax.JPEGProcess14SV1
            ? (IDicomCodec)new NativeJpegLossless14Sv1Codec()
            : throw new Xunit.Sdk.XunitException($"No native source decoder is configured for {source.Syntax.UID.Name} in {fixturePath}.");
        var decoded = CreateRawTargetPixelData(source);
        nativeCodec.Decode(source, decoded, nativeCodec.GetDefaultParameters());
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

    private static void AssertDecoded(DicomPixelData source, DicomPixelData compressed, DicomPixelData decoded, InteropCase testCase)
    {
        PixelDataAssertions.AssertFrameCount(source, compressed);
        PixelDataAssertions.AssertRequiredCompressionTags(compressed.Dataset, testCase.Syntax);
        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);

        if (testCase.Tolerance.HasValue)
        {
            PixelDataAssertions.FramesMatchWithinTolerance(source, decoded, testCase.Tolerance.Value);
        }
        else
        {
            PixelDataAssertions.FramesMatchExactly(source, decoded);
        }
    }

    private static DicomCodecParams CreatePureEncodeParameters(InteropCase testCase)
    {
        var parameters = testCase.PureCodec.GetDefaultParameters();
        if (parameters is PureJpeg2000Params jpeg2000Parameters && testCase.Syntax == DicomTransferSyntax.JPEG2000Lossy)
        {
            jpeg2000Parameters.Irreversible = true;
            jpeg2000Parameters.Rate = 0;
        }

        return parameters;
    }

    private static DicomCodecParams CreateNativeParameters(InteropCase testCase)
    {
        if (testCase.Syntax == DicomTransferSyntax.JPEG2000Lossy)
        {
            return new NativeJpeg2000Params { Irreversible = true, Rate = 0 };
        }

        var parameters = testCase.NativeCodec.GetDefaultParameters();
        if (parameters is NativeJpegCodecParams jpegParameters)
        {
            jpegParameters.ConvertColorSpaceToRGB = true;
        }

        if (parameters is NativeJpegLsParams jpegLsParameters && testCase.Syntax == DicomTransferSyntax.JPEGLSNearLossless)
        {
            jpegLsParameters.AllowedError = 2;
        }

        return parameters;
    }

    private static DicomPixelData CreateRawTargetPixelData(DicomPixelData source)
    {
        var photometricInterpretation = source.SamplesPerPixel == 3
            ? PhotometricInterpretation.Rgb.Value
            : source.PhotometricInterpretation.Value;
        return CreateTargetPixelData(source, DicomTransferSyntax.ExplicitVRLittleEndian, photometricInterpretation);
    }

    private static DicomPixelData CreateTargetPixelData(
        DicomPixelData source,
        DicomTransferSyntax transferSyntax,
        string photometricInterpretation)
    {
        var dataset = new DicomDataset(transferSyntax)
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

    private static bool SupportsAny(DicomPixelData source)
    {
        return source.BitsAllocated is 8 or 16 && source.SamplesPerPixel is 1 or 3;
    }

    private static bool SupportsSequentialDct(DicomPixelData source)
    {
        return source.BitsAllocated == 8
            && source.BitsStored == 8
            && source.SamplesPerPixel is 1 or 3;
    }

    private static bool SupportsJpeg2000(DicomPixelData source)
    {
        return source.BitsAllocated is 8 or 16 && source.SamplesPerPixel is 1 or 3;
    }

    public sealed record InteropCase(
        string FixtureName,
        string FixturePath,
        DicomTransferSyntax Syntax,
        IDicomCodec PureCodec,
        IDicomCodec NativeCodec,
        int? Tolerance)
    {
        public override string ToString()
        {
            return $"{FixtureName}_{Syntax.UID.UID}";
        }
    }

    private sealed record CodecDefinition(
        DicomTransferSyntax Syntax,
        Func<IDicomCodec> CreatePureCodec,
        Func<IDicomCodec> CreateNativeCodec,
        int? Tolerance,
        Func<DicomPixelData, bool> Supports);
}
