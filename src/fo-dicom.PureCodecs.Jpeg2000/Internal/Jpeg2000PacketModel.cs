namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000PacketModel
    {
        public Jpeg2000PacketModel(int layerIndex, int resolutionLevel, int componentIndex, int precinctIndex)
        {
            LayerIndex = layerIndex;
            ResolutionLevel = resolutionLevel;
            ComponentIndex = componentIndex;
            PrecinctIndex = precinctIndex;
        }

        public int LayerIndex { get; }

        public int ResolutionLevel { get; }

        public int ComponentIndex { get; }

        public int PrecinctIndex { get; }

        public override string ToString()
        {
            return $"L{LayerIndex}-R{ResolutionLevel}-C{ComponentIndex}-P{PrecinctIndex}";
        }
    }
}
