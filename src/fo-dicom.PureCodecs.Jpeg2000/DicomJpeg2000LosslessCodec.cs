using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000LosslessCodec : DicomJpeg2000ClassicCodecBase
    {
        public DicomJpeg2000LosslessCodec()
            : base(DicomTransferSyntax.JPEG2000Lossless, defaultIrreversible: false)
        {
        }
    }
}
