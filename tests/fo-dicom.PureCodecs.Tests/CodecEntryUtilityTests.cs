using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Internal;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class CodecEntryUtilityTests
{
    [Fact]
    public void Byte_buffer_conversion_returns_copy()
    {
        var buffer = new MemoryByteBuffer(new byte[] { 1, 2, 3 });

        var copy = buffer.ToArrayCopy();
        copy[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.Data);
        Assert.Equal(new byte[] { 9, 2, 3 }, copy);
    }

    [Fact]
    public void Frame_validation_throws_codec_exception_for_out_of_range_frame()
    {
        var exception = Assert.Throws<DicomCodecException>(
            () => FrameValidation.EnsureFrameIndex(DicomTransferSyntax.RLELossless, frame: 2, frameCount: 2));

        Assert.Contains(DicomTransferSyntax.RLELossless.UID.Name, exception.Message);
        Assert.Contains("frame 2", exception.Message);
    }

    [Fact]
    public void Codec_failure_wrap_preserves_existing_codec_exception()
    {
        var original = new DicomCodecException("codec failure");

        var wrapped = CodecFailure.Wrap(DicomTransferSyntax.RLELossless, "decode", frame: 0, original);

        Assert.Same(original, wrapped);
    }

    [Fact]
    public void Codec_failure_wrap_adds_transfer_syntax_operation_and_frame()
    {
        var inner = new InvalidOperationException("bad stream");

        var wrapped = CodecFailure.Wrap(DicomTransferSyntax.RLELossless, "decode", frame: 3, inner);

        Assert.Contains(DicomTransferSyntax.RLELossless.UID.Name, wrapped.Message);
        Assert.Contains("decode", wrapped.Message);
        Assert.Contains("frame 3", wrapped.Message);
        Assert.Same(inner, wrapped.InnerException);
    }

    [Fact]
    public void Pixel_metadata_snapshot_captures_pixel_data_shape()
    {
        var dataset = new DicomDataset
        {
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)3 },
            { DicomTag.Columns, (ushort)4 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PixelData, new byte[12] },
        };

        var pixelData = DicomPixelData.Create(dataset);

        var snapshot = PixelMetadataSnapshot.From(pixelData);

        Assert.Equal((ushort)4, snapshot.Width);
        Assert.Equal((ushort)3, snapshot.Height);
        Assert.Equal(1, snapshot.NumberOfFrames);
        Assert.Equal((ushort)8, snapshot.BitsAllocated);
        Assert.Equal((ushort)8, snapshot.BitsStored);
        Assert.Equal((ushort)1, snapshot.SamplesPerPixel);
        Assert.Equal(PixelRepresentation.Unsigned, snapshot.PixelRepresentation);
        Assert.Equal(PlanarConfiguration.Interleaved, snapshot.PlanarConfiguration);
        Assert.Equal(PhotometricInterpretation.Monochrome2, snapshot.PhotometricInterpretation);
    }
}
