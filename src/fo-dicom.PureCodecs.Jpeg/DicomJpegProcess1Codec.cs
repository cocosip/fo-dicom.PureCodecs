using FellowOakDicom;
using FellowOakDicom.PureCodecs.Jpeg.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg
{
    public sealed class DicomJpegProcess1Codec : DicomJpegSequentialCodecBase
    {
        public DicomJpegProcess1Codec()
            : base(DicomTransferSyntax.JPEGProcess1, JpegSequentialProcess.Baseline)
        {
        }
    }
}
