using FellowOakDicom.PureCodecs.Jpeg2000.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg2000
{
    public sealed class DicomHtJpeg2000Params : FellowOakDicom.Imaging.Codec.DicomHtJpeg2000Params
    {
        public double TargetRatio { get; set; }

        public int NumLayers { get; set; } = 1;

        public Jpeg2000ProgressionOrder Jpeg2000ProgressionOrder
        {
            get { return ToJpeg2000ProgressionOrder(ProgressionOrder); }
            set { ProgressionOrder = ToCoreProgressionOrder(value); }
        }

        public static DicomHtJpeg2000Params From(FellowOakDicom.Imaging.Codec.DicomCodecParams parameters)
        {
            if (parameters is DicomHtJpeg2000Params htParameters)
            {
                return htParameters;
            }

            if (parameters is FellowOakDicom.Imaging.Codec.DicomHtJpeg2000Params coreParameters)
            {
                return new DicomHtJpeg2000Params
                {
                    Irreversible = coreParameters.Irreversible,
                    NumberOfDecompositions = coreParameters.NumberOfDecompositions,
                    EmployColorTransform = coreParameters.EmployColorTransform,
                    ProgressionOrder = coreParameters.ProgressionOrder,
                    InsertTlmMarkers = coreParameters.InsertTlmMarkers
                };
            }

            return new DicomHtJpeg2000Params();
        }

        internal static Jpeg2000ProgressionOrder ToJpeg2000ProgressionOrder(FellowOakDicom.Imaging.Codec.ProgressionOrder order)
        {
            return (Jpeg2000ProgressionOrder)(int)order;
        }

        internal static FellowOakDicom.Imaging.Codec.ProgressionOrder ToCoreProgressionOrder(Jpeg2000ProgressionOrder order)
        {
            return (FellowOakDicom.Imaging.Codec.ProgressionOrder)(int)order;
        }
    }
}
