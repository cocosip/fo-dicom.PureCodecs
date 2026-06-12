namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsFrameComponent
    {
        public JpegLsFrameComponent(int identifier, int horizontalSamplingFactor, int verticalSamplingFactor, int mappingTableSelector)
        {
            Identifier = identifier;
            HorizontalSamplingFactor = horizontalSamplingFactor;
            VerticalSamplingFactor = verticalSamplingFactor;
            MappingTableSelector = mappingTableSelector;
        }

        public int Identifier { get; }

        public int HorizontalSamplingFactor { get; }

        public int VerticalSamplingFactor { get; }

        public int MappingTableSelector { get; }
    }
}
