using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;
using Xunit;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal static class Phase1TransferSyntaxes
{
    public static readonly DicomTransferSyntax[] All =
    {
        DicomTransferSyntax.RLELossless,
        DicomTransferSyntax.JPEGProcess1,
        DicomTransferSyntax.JPEGProcess2_4,
        DicomTransferSyntax.JPEGProcess14,
        DicomTransferSyntax.JPEGProcess14SV1,
        DicomTransferSyntax.JPEGLSLossless,
        DicomTransferSyntax.JPEGLSNearLossless,
        DicomTransferSyntax.JPEG2000Lossless,
        DicomTransferSyntax.JPEG2000Lossy,
        DicomTransferSyntax.HTJ2KLossless,
        DicomTransferSyntax.HTJ2KLosslessRPCL,
        DicomTransferSyntax.HTJ2K,
    };

    public static TheoryData<DicomTransferSyntax, DicomTransferSyntax> AllPairs
    {
        get
        {
            var pairs = new TheoryData<DicomTransferSyntax, DicomTransferSyntax>();
            foreach (var source in All)
            {
                foreach (var target in All)
                {
                    pairs.Add(source, target);
                }
            }

            return pairs;
        }
    }

    public static TheoryData<DicomTransferSyntax, IDicomCodec, int?> RoundTripCodecs => new()
    {
        { DicomTransferSyntax.RLELossless, new DicomRleLosslessCodec(), null },
        { DicomTransferSyntax.JPEGProcess1, new DicomJpegProcess1Codec(), 20 },
        { DicomTransferSyntax.JPEGProcess2_4, new DicomJpegProcess2_4Codec(), 20 },
        { DicomTransferSyntax.JPEGProcess14, new DicomJpegLossless14Codec(), null },
        { DicomTransferSyntax.JPEGProcess14SV1, new DicomJpegLossless14SV1Codec(), null },
        { DicomTransferSyntax.JPEGLSLossless, new DicomJpegLsLosslessCodec(), null },
        { DicomTransferSyntax.JPEGLSNearLossless, new DicomJpegLsNearLosslessCodec(), 2 },
        { DicomTransferSyntax.JPEG2000Lossless, new DicomJpeg2000LosslessCodec(), null },
        { DicomTransferSyntax.JPEG2000Lossy, new DicomJpeg2000LossyCodec(), 6 },
        { DicomTransferSyntax.HTJ2KLossless, new DicomHtJpeg2000LosslessCodec(), null },
        { DicomTransferSyntax.HTJ2KLosslessRPCL, new DicomHtJpeg2000LosslessRpclCodec(), null },
        { DicomTransferSyntax.HTJ2K, new DicomHtJpeg2000LossyCodec(), 128 },
    };
}
