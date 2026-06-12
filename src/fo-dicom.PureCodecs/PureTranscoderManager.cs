using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs
{
    /// <summary>
    /// Pure managed transcoder manager entry point for fo-dicom.
    /// </summary>
    public sealed class PureTranscoderManager : TranscoderManager
    {
        public PureTranscoderManager()
        {
            LoadCodecs();
        }

        public override void LoadCodecs(string? path = null, string? search = null)
        {
        }
    }
}
