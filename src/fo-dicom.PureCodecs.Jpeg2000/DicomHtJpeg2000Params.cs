using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000Params : DicomCodecParams
    {
        public Jpeg2000ProgressionOrder ProgressionOrder { get; set; } = Jpeg2000ProgressionOrder.LRCP;

        public double TargetRatio { get; set; }

        public int NumLayers { get; set; } = 1;

        public static DicomHtJpeg2000Params From(DicomCodecParams parameters)
        {
            if (parameters is DicomHtJpeg2000Params htParameters)
            {
                return htParameters;
            }

            return new DicomHtJpeg2000Params();
        }
    }
}
