using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000LosslessCodec : UnimplementedDicomCodec
    {
        public DicomJpeg2000LosslessCodec()
            : base(DicomTransferSyntax.JPEG2000Lossless)
        {
        }
    }
}
