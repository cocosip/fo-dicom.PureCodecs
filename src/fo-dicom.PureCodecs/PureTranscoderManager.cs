using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;

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
            Codecs.Clear();

            AddCodec(new DicomRleLosslessCodec());

            AddCodec(new DicomJpegProcess1Codec());
            AddCodec(new DicomJpegProcess2_4Codec());
            AddCodec(new DicomJpegLossless14Codec());
            AddCodec(new DicomJpegLossless14SV1Codec());

            AddCodec(new DicomJpegLsLosslessCodec());
            AddCodec(new DicomJpegLsNearLosslessCodec());

            AddCodec(new DicomJpeg2000LosslessCodec());
            AddCodec(new DicomJpeg2000LossyCodec());
            AddCodec(new DicomHtJpeg2000LosslessCodec());
            AddCodec(new DicomHtJpeg2000LosslessRpclCodec());
            AddCodec(new DicomHtJpeg2000LossyCodec());
        }

        private void AddCodec(IDicomCodec codec)
        {
            Codecs[codec.TransferSyntax] = codec;
        }
    }
}
