using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegLossless14Codec : DicomJpegLosslessCodecBase
    {
        public DicomJpegLossless14Codec()
            : base(DicomTransferSyntax.JPEGProcess14, firstOrderPrediction: false)
        {
        }
    }
}
