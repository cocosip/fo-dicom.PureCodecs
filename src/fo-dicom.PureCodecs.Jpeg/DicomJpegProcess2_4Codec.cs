using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegProcess2_4Codec : UnimplementedDicomCodec
    {
        public DicomJpegProcess2_4Codec()
            : base(DicomTransferSyntax.JPEGProcess2_4)
        {
        }
    }
}
