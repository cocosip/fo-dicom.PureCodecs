using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegProcess2_4Codec : DicomJpegSequentialCodecBase
    {
        public DicomJpegProcess2_4Codec()
            : base(DicomTransferSyntax.JPEGProcess2_4, JpegSequentialProcess.Extended)
        {
        }
    }
}
