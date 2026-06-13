using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000LossyCodec : DicomJpeg2000ClassicCodecBase
    {
        public DicomJpeg2000LossyCodec()
            : base(DicomTransferSyntax.JPEG2000Lossy, defaultIrreversible: true)
        {
        }
    }
}
