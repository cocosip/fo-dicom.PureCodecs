using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegLossless14Codec : UnimplementedDicomCodec
    {
        public DicomJpegLossless14Codec()
            : base(DicomTransferSyntax.JPEGProcess14)
        {
        }
    }
}
