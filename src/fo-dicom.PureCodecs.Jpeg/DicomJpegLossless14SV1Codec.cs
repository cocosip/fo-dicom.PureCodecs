using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegLossless14SV1Codec : DicomJpegLosslessCodecBase
    {
        public DicomJpegLossless14SV1Codec()
            : base(DicomTransferSyntax.JPEGProcess14SV1, firstOrderPrediction: true)
        {
        }
    }
}
