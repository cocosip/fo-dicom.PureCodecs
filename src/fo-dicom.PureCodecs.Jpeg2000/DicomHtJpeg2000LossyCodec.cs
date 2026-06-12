using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LossyCodec : UnimplementedDicomCodec
    {
        public DicomHtJpeg2000LossyCodec()
            : base(DicomTransferSyntax.HTJ2K)
        {
        }
    }
}
