namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegFrameComponent
    {
        public JpegFrameComponent(int identifier, int horizontalSamplingFactor, int verticalSamplingFactor, int quantizationTableId)
        {
            Identifier = identifier;
            HorizontalSamplingFactor = horizontalSamplingFactor;
            VerticalSamplingFactor = verticalSamplingFactor;
            QuantizationTableId = quantizationTableId;
        }

        public int Identifier { get; }

        public int HorizontalSamplingFactor { get; }

        public int VerticalSamplingFactor { get; }

        public int QuantizationTableId { get; }
    }
}
