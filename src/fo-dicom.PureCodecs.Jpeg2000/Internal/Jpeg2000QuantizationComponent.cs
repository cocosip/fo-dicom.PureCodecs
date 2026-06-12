namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000QuantizationComponent : Jpeg2000Quantization
    {
        private Jpeg2000QuantizationComponent(int componentIndex, Jpeg2000Quantization quantization)
            : base(quantization.Style, quantization.GuardBits, CopySteps(quantization))
        {
            ComponentIndex = componentIndex;
        }

        public int ComponentIndex { get; }

        public Jpeg2000ResolvedQuantization InheritFrom(Jpeg2000QuantizationDefault defaults)
        {
            return new Jpeg2000ResolvedQuantization(ComponentIndex, this, defaults.GuardBits);
        }

        public static Jpeg2000QuantizationComponent Parse(Jpeg2000MarkerSegment segment, int componentCount)
        {
            if (segment.Code != Jpeg2000Marker.QCC)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 QCC marker segment expected.");
            }

            var componentBytes = componentCount <= 256 ? 1 : 2;
            if (segment.Payload.Length < componentBytes + 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 QCC payload is too short.");
            }

            var componentIndex = componentBytes == 1 ? segment.Payload[0] : Jpeg2000Binary.ReadUInt16(segment.Payload, 0);
            if (componentIndex >= componentCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 QCC component index is outside the component range.");
            }

            return new Jpeg2000QuantizationComponent(componentIndex, ParsePayload(segment.Payload, componentBytes));
        }
    }
}
