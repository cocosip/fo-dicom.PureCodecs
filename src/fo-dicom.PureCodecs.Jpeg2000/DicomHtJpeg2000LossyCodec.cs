using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LossyCodec : DicomHtJpeg2000CodecBase
    {
        public DicomHtJpeg2000LossyCodec()
            : base(DicomTransferSyntax.HTJ2K, lossy: true, defaultProgressionOrder: Jpeg2000ProgressionOrder.RPCL)
        {
        }
    }
}
