using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal static class DicomPixelDataFixtures
{
    public static DicomDataset CreateMonochrome8(
        ushort rows = 4,
        ushort columns = 5,
        byte[]? frame = null)
    {
        var pixels = frame ?? CreateRamp(rows * columns, seed: 11);
        var dataset = CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(pixels));
        return dataset;
    }

    public static DicomDataset CreateMonochrome16(
        ushort rows = 3,
        ushort columns = 4,
        byte[]? frame = null)
    {
        var pixels = frame ?? CreateRamp(rows * columns * 2, seed: 29);
        var dataset = CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 16,
            bitsStored: 16,
            highBit: 15,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(pixels));
        return dataset;
    }

    public static DicomDataset CreateRgbInterleaved(
        ushort rows = 3,
        ushort columns = 3,
        byte[]? frame = null)
    {
        var pixels = frame ?? CreateRamp(rows * columns * 3, seed: 47);
        var dataset = CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 3,
            photometricInterpretation: PhotometricInterpretation.Rgb,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: PlanarConfiguration.Interleaved,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(pixels));
        return dataset;
    }

    public static DicomDataset CreateRgbPlanar(
        ushort rows = 3,
        ushort columns = 3,
        byte[]? frame = null)
    {
        var pixels = frame ?? CreateRamp(rows * columns * 3, seed: 53);
        var dataset = CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 3,
            photometricInterpretation: PhotometricInterpretation.Rgb,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: PlanarConfiguration.Planar,
            numberOfFrames: 1,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(pixels));
        return dataset;
    }

    public static DicomDataset CreateMultiFrameMonochrome8(
        ushort rows = 2,
        ushort columns = 3,
        int frameCount = 3)
    {
        var dataset = CreateBaseDataset(
            rows,
            columns,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: null,
            numberOfFrames: frameCount,
            transferSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

        var pixelData = DicomPixelData.Create(dataset, true);
        var frameLength = rows * columns;
        for (var frame = 0; frame < frameCount; frame++)
        {
            pixelData.AddFrame(new MemoryByteBuffer(CreateRamp(frameLength, seed: frame * 17)));
        }

        return dataset;
    }

    public static DicomPixelData CreateEmptyMonochrome8PixelData(DicomTransferSyntax transferSyntax)
    {
        var dataset = CreateBaseDataset(
            rows: 4,
            columns: 5,
            samplesPerPixel: 1,
            photometricInterpretation: PhotometricInterpretation.Monochrome2,
            bitsAllocated: 8,
            bitsStored: 8,
            highBit: 7,
            planarConfiguration: null,
            numberOfFrames: 1,
            transferSyntax: transferSyntax);

        return DicomPixelData.Create(dataset, true);
    }

    private static DicomDataset CreateBaseDataset(
        ushort rows,
        ushort columns,
        ushort samplesPerPixel,
        PhotometricInterpretation photometricInterpretation,
        ushort bitsAllocated,
        ushort bitsStored,
        ushort highBit,
        PlanarConfiguration? planarConfiguration,
        int numberOfFrames,
        DicomTransferSyntax transferSyntax)
    {
        var dataset = new DicomDataset(transferSyntax)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.PatientID, "PURECODECS" },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.PhotometricInterpretation, photometricInterpretation.Value },
            { DicomTag.Rows, rows },
            { DicomTag.Columns, columns },
            { DicomTag.BitsAllocated, bitsAllocated },
            { DicomTag.BitsStored, bitsStored },
            { DicomTag.HighBit, highBit },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, samplesPerPixel },
        };

        if (numberOfFrames > 1)
        {
            dataset.Add(DicomTag.NumberOfFrames, numberOfFrames.ToString());
        }

        if (planarConfiguration.HasValue)
        {
            dataset.Add(DicomTag.PlanarConfiguration, (ushort)planarConfiguration.Value);
        }

        return dataset;
    }

    private static byte[] CreateRamp(int length, int seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((seed + i * 13) % 251);
        }

        return bytes;
    }
}
