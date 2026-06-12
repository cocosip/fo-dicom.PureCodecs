namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000TileModel
    {
        public Jpeg2000TileModel(int index, uint x0, uint y0, uint x1, uint y1)
        {
            Index = index;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public int Index { get; }

        public uint X0 { get; }

        public uint Y0 { get; }

        public uint X1 { get; }

        public uint Y1 { get; }

        public uint Width
        {
            get { return X1 - X0; }
        }

        public uint Height
        {
            get { return Y1 - Y0; }
        }
    }
}
