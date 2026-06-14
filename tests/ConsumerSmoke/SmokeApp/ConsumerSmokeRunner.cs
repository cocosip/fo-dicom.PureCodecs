using System;
using System.Linq;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs;

namespace ConsumerSmoke;

internal static class ConsumerSmokeRunner
{
    private static readonly byte[] RawFrame =
    {
        11, 24, 37, 50,
        63, 76, 89, 102,
        115, 128, 141, 154,
        167, 180, 193, 206
    };

    public static int Main(string[] args)
    {
        try
        {
            Run();
            Console.WriteLine("Consumer smoke passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run()
    {
        new DicomSetupBuilder()
            .RegisterServices(services => services
                .AddFellowOakDicom()
                .AddTranscoderManager<PureTranscoderManager>())
            .Build();

        ITranscoderManager manager = new PureTranscoderManager();
        if (!manager.HasCodec(DicomTransferSyntax.RLELossless))
        {
            throw new InvalidOperationException("The package did not register the RLE codec.");
        }

        var rawDataset = CreateRawDataset();
        var compressedDataset = Transcode(rawDataset, DicomTransferSyntax.RLELossless);
        var decodedDataset = Transcode(compressedDataset, DicomTransferSyntax.ExplicitVRLittleEndian);
        var decodedFrame = DicomPixelData.Create(decodedDataset).GetFrame(0).Data;

        if (!RawFrame.SequenceEqual(decodedFrame))
        {
            throw new InvalidOperationException("Decoded frame did not match the original raw frame.");
        }

        if (compressedDataset.InternalTransferSyntax != DicomTransferSyntax.RLELossless)
        {
            throw new InvalidOperationException("Compressed dataset did not use RLE Lossless.");
        }

        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .ToArray();
        foreach (var assemblyName in new[]
        {
            "fo-dicom.PureCodecs",
            "fo-dicom.PureCodecs.Rle",
            "fo-dicom.PureCodecs.Jpeg",
            "fo-dicom.PureCodecs.JpegLs",
            "fo-dicom.PureCodecs.Jpeg2000",
        })
        {
            if (!loadedAssemblies.Contains(assemblyName))
            {
                throw new InvalidOperationException($"{assemblyName} was not loaded from the package.");
            }
        }
    }

    private static DicomDataset Transcode(DicomDataset dataset, DicomTransferSyntax targetSyntax)
    {
        var transcoder = new DicomTranscoder(dataset.InternalTransferSyntax, targetSyntax);
        return transcoder.Transcode(dataset);
    }

    private static DicomDataset CreateRawDataset()
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.PatientID, "PURECODECS-CONSUMER-SMOKE" },
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)4 },
            { DicomTag.Columns, (ushort)4 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(RawFrame));
        return dataset;
    }
}
