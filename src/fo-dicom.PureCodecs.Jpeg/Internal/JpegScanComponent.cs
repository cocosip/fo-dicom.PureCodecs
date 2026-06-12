namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegScanComponent
    {
        public JpegScanComponent(int selector, int dcTableId, int acTableId)
        {
            Selector = selector;
            DcTableId = dcTableId;
            AcTableId = acTableId;
        }

        public int Selector { get; }

        public int DcTableId { get; }

        public int AcTableId { get; }
    }
}
