using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.JpegLs.Internal;

namespace FellowOakDicom.PureCodecs.JpegLs
{
    public enum DicomJpegLsColorTransform
    {
        None = 0,
        HP1 = 1,
        HP2 = 2,
        HP3 = 3
    }

    public sealed class DicomJpegLsParams : DicomCodecParams
    {
        public DicomJpegLsParams()
        {
            AllowedError = 3;
            InterleaveMode = JpegLsInterleaveMode.Line;
            ColorTransform = DicomJpegLsColorTransform.HP1;
        }

        public int AllowedError { get; set; }

        public JpegLsInterleaveMode InterleaveMode { get; set; }

        public DicomJpegLsColorTransform ColorTransform { get; set; }
    }
}
