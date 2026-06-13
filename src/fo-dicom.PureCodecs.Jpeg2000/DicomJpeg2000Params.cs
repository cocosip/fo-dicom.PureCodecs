using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000Params : FellowOakDicom.Imaging.Codec.DicomJpeg2000Params
    {
        public Jpeg2000ProgressionOrder ProgressionOrder { get; set; } = Jpeg2000ProgressionOrder.LRCP;

        public double TargetRatio { get; set; }

        public int NumLayers { get; set; } = 1;

        public bool IncludeFinalLosslessLayer { get; set; }

        public static DicomJpeg2000Params From(DicomCodecParams parameters)
        {
            if (parameters is DicomJpeg2000Params jpeg2000Parameters)
            {
                return jpeg2000Parameters;
            }

            if (parameters is FellowOakDicom.Imaging.Codec.DicomJpeg2000Params coreParameters)
            {
                return new DicomJpeg2000Params
                {
                    Irreversible = coreParameters.Irreversible,
                    Rate = coreParameters.Rate,
                    RateLevels = coreParameters.RateLevels,
                    IsVerbose = coreParameters.IsVerbose,
                    AllowMCT = coreParameters.AllowMCT,
                    UpdatePhotometricInterpretation = coreParameters.UpdatePhotometricInterpretation,
                    EncodeSignedPixelValuesAsUnsigned = coreParameters.EncodeSignedPixelValuesAsUnsigned
                };
            }

            return new DicomJpeg2000Params();
        }
    }
}
