namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000PrecinctModel
    {
        public Jpeg2000PrecinctModel(
            int resolutionLevel,
            int componentIndex,
            int precinctX,
            int precinctY,
            uint x0,
            uint y0,
            uint x1,
            uint y1)
        {
            if (x1 < x0 || y1 < y0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 precinct bounds are invalid.");
            }

            ResolutionLevel = resolutionLevel;
            ComponentIndex = componentIndex;
            PrecinctX = precinctX;
            PrecinctY = precinctY;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public int ResolutionLevel { get; }

        public int ComponentIndex { get; }

        public int PrecinctX { get; }

        public int PrecinctY { get; }

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
