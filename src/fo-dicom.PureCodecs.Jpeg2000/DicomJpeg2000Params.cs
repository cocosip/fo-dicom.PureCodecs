using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomJpeg2000Params : DicomCodecParams
    {
        public bool Irreversible { get; set; }

        public double Rate { get; set; }

        public int RateLevels { get; set; }

        public double TargetRatio { get; set; }

        public int NumLayers { get; set; } = 1;

        public bool IncludeFinalLosslessLayer { get; set; }

        public bool AllowMCT { get; set; } = true;

        public static DicomJpeg2000Params From(DicomCodecParams parameters)
        {
            if (parameters is DicomJpeg2000Params jpeg2000Parameters)
            {
                return jpeg2000Parameters;
            }

            return new DicomJpeg2000Params();
        }
    }
}
