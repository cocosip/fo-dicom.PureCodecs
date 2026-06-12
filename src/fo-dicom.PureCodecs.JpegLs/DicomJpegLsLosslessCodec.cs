using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.JpegLs
{
    public sealed class DicomJpegLsLosslessCodec : UnimplementedDicomCodec
    {
        public DicomJpegLsLosslessCodec()
            : base(DicomTransferSyntax.JPEGLSLossless)
        {
        }
    }
}
