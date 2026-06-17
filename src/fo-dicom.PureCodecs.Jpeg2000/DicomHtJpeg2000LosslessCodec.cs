using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LosslessCodec : DicomHtJpeg2000CodecBase
    {
        public DicomHtJpeg2000LosslessCodec()
            : base(DicomTransferSyntax.HTJ2KLossless, lossy: false, defaultProgressionOrder: Jpeg2000ProgressionOrder.RPCL)
        {
        }
    }
}
