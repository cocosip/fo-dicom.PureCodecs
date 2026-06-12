using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal static class PixelDataAssertions
{
    public static void FramesMatchExactly(DicomPixelData expected, DicomPixelData actual)
    {
        AssertFrameCount(expected, actual);

        for (var frame = 0; frame < expected.NumberOfFrames; frame++)
        {
            Assert.Equal(ToArray(expected.GetFrame(frame)), ToArray(actual.GetFrame(frame)));
        }
    }

    public static void FramesMatchWithinTolerance(DicomPixelData expected, DicomPixelData actual, int tolerance)
    {
        AssertFrameCount(expected, actual);

        for (var frame = 0; frame < expected.NumberOfFrames; frame++)
        {
            var expectedBytes = ToArray(expected.GetFrame(frame));
            var actualBytes = ToArray(actual.GetFrame(frame));

            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            for (var i = 0; i < expectedBytes.Length; i++)
            {
                var difference = Math.Abs(expectedBytes[i] - actualBytes[i]);
                Assert.True(
                    difference <= tolerance,
                    $"Frame {frame} byte {i} differed by {difference}, which is greater than tolerance {tolerance}.");
            }
        }
    }

    public static void AssertFrameCount(DicomPixelData expected, DicomPixelData actual)
    {
        Assert.Equal(expected.NumberOfFrames, actual.NumberOfFrames);
    }

    public static void AssertRequiredCompressionTags(DicomDataset dataset, DicomTransferSyntax transferSyntax)
    {
        Assert.Same(transferSyntax, dataset.InternalTransferSyntax);
        Assert.True(dataset.Contains(DicomTag.PhotometricInterpretation));
        Assert.True(dataset.Contains(DicomTag.SamplesPerPixel));
        Assert.True(dataset.Contains(DicomTag.Rows));
        Assert.True(dataset.Contains(DicomTag.Columns));
        Assert.True(dataset.Contains(DicomTag.BitsAllocated));
        Assert.True(dataset.Contains(DicomTag.BitsStored));
        Assert.True(dataset.Contains(DicomTag.HighBit));
        Assert.True(dataset.Contains(DicomTag.PixelRepresentation));
        Assert.True(dataset.Contains(DicomTag.PixelData));
    }

    public static TException AssertManagedCodecException<TException>(Action action)
        where TException : DicomCodecException
    {
        return Assert.Throws<TException>(action);
    }

    private static byte[] ToArray(IByteBuffer buffer)
    {
        var bytes = new byte[buffer.Size];
        Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
