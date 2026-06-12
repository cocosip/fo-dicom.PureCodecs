using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class PureTranscoderManagerTests
{
    [Fact]
    public void Constructor_creates_transcoder_manager()
    {
        var manager = new PureTranscoderManager();

        Assert.IsAssignableFrom<TranscoderManager>(manager);
    }
}
