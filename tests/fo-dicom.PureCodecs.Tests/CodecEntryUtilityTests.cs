using System;
using FellowOakDicom;
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

}
