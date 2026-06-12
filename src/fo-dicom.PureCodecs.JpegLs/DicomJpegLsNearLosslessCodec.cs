using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.JpegLs
{
    public sealed class DicomJpegLsNearLosslessCodec : UnimplementedDicomCodec
    {
        public DicomJpegLsNearLosslessCodec()
            : base(DicomTransferSyntax.JPEGLSNearLossless)
        {
        }
    }
}
