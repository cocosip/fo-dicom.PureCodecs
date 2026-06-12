using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegProcess1Codec : UnimplementedDicomCodec
    {
        public DicomJpegProcess1Codec()
            : base(DicomTransferSyntax.JPEGProcess1)
        {
        }
    }
}
