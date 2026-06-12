using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000LosslessCodec : UnimplementedDicomCodec
    {
        public DicomHtJpeg2000LosslessCodec()
            : base(DicomTransferSyntax.HTJ2KLossless)
        {
        }
    }
}
