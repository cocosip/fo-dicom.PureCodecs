using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000LossyCodec : UnimplementedDicomCodec
    {
        public DicomJpeg2000LossyCodec()
            : base(DicomTransferSyntax.JPEG2000Lossy)
        {
        }
    }
}
