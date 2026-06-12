using FellowOakDicom;
using FellowOakDicom.PureCodecs.JpegLs.Internal;

namespace FellowOakDicom.PureCodecs.JpegLs
{
    public sealed class DicomJpegLsLosslessCodec : DicomJpegLsCodecBase
    {
        public DicomJpegLsLosslessCodec()
            : base(DicomTransferSyntax.JPEGLSLossless)
        {
        }
    }
}
