using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests;

public sealed class PublicApiContractTests
{
    private static readonly string[] RequiredInternalBaseTypes =
    {
        "FellowOakDicom.PureCodecs.Jpeg.Internal.DicomJpegLosslessCodecBase",
        "FellowOakDicom.PureCodecs.Jpeg.Internal.DicomJpegSequentialCodecBase",
        "FellowOakDicom.PureCodecs.Jpeg2000.Internal.DicomHtJpeg2000CodecBase",
        "FellowOakDicom.PureCodecs.Jpeg2000.Internal.DicomJpeg2000ClassicCodecBase",
        "FellowOakDicom.PureCodecs.Jpeg2000.Internal.Jpeg2000ProgressionOrder",
        "FellowOakDicom.PureCodecs.JpegLs.Internal.DicomJpegLsCodecBase",
        "FellowOakDicom.PureCodecs.JpegLs.Internal.JpegLsInterleaveMode"
    };

    [Fact]
    public void Codec_assemblies_export_only_required_internal_base_types()
    {
        var assemblies = new[]
        {
            typeof(PureTranscoderManager).Assembly,
            typeof(DicomJpegProcess1Codec).Assembly,
            typeof(DicomJpeg2000LosslessCodec).Assembly,
            typeof(DicomJpegLsLosslessCodec).Assembly,
            typeof(DicomRleLosslessCodec).Assembly
        };

        var exportedInternalTypes = assemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace?.EndsWith(".Internal", StringComparison.Ordinal) == true)
            .Select(type => type.FullName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RequiredInternalBaseTypes, exportedInternalTypes);
        Assert.DoesNotContain(
            assemblies.SelectMany(assembly => assembly.GetExportedTypes()),
            type => type.Name == "UnimplementedDicomCodec");
    }
}
