using FellowOakDicom;
using FellowOakDicom.PureCodecs.JpegLs.Internal;

namespace FellowOakDicom.PureCodecs.JpegLs
{
    public sealed class DicomJpegLsNearLosslessCodec : DicomJpegLsCodecBase
    {
        public DicomJpegLsNearLosslessCodec()
            : base(DicomTransferSyntax.JPEGLSNearLossless)
        {
        }
    }
}
