namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000QuantizationDefault : Jpeg2000Quantization
    {
        private Jpeg2000QuantizationDefault(Jpeg2000Quantization quantization)
            : base(quantization.Style, quantization.GuardBits, CopySteps(quantization))
        {
        }

        public static Jpeg2000QuantizationDefault Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.QCD)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 QCD marker segment expected.");
            }

            return new Jpeg2000QuantizationDefault(ParsePayload(segment.Payload, 0));
        }
    }
}
