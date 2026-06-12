using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegLossless14SV1Codec : UnimplementedDicomCodec
    {
        public DicomJpegLossless14SV1Codec()
            : base(DicomTransferSyntax.JPEGProcess14SV1)
        {
        }
    }
}
