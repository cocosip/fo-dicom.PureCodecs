using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LosslessRpclCodec : DicomHtJpeg2000CodecBase
    {
        public DicomHtJpeg2000LosslessRpclCodec()
            : base(DicomTransferSyntax.HTJ2KLosslessRPCL, lossy: false, defaultProgressionOrder: Jpeg2000ProgressionOrder.RPCL)
        {
        }
    }
}
