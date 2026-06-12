using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LosslessRpclCodec : UnimplementedDicomCodec
    {
        public DicomHtJpeg2000LosslessRpclCodec()
            : base(DicomTransferSyntax.HTJ2KLosslessRPCL)
        {
        }
    }
}
