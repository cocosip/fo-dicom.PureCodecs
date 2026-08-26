namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    internal sealed class JpegLsScanComponent
    {
        public JpegLsScanComponent(int selector, int mappingTableSelector)
        {
            Selector = selector;
            MappingTableSelector = mappingTableSelector;
        }

        public int Selector { get; }

        public int MappingTableSelector { get; }
    }
}
