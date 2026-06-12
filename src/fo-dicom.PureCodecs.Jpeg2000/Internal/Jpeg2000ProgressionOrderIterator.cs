using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000ProgressionOrderIterator
    {
        public static IReadOnlyList<Jpeg2000PacketModel> Enumerate(
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            int resolutionCount,
            int componentCount,
            int precinctCount)
        {
            if (layerCount < 1 || resolutionCount < 1 || componentCount < 1 || precinctCount < 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 progression dimensions must be positive.");
            }

            var packets = new List<Jpeg2000PacketModel>();
            switch (progressionOrder)
            {
                case Jpeg2000ProgressionOrder.LRCP:
                    AddLrcp(packets, layerCount, resolutionCount, componentCount, precinctCount);
                    break;
                case Jpeg2000ProgressionOrder.RLCP:
                    AddRlcp(packets, layerCount, resolutionCount, componentCount, precinctCount);
                    break;
                case Jpeg2000ProgressionOrder.RPCL:
                    AddRpcl(packets, layerCount, resolutionCount, componentCount, precinctCount);
                    break;
                case Jpeg2000ProgressionOrder.PCRL:
                    AddPcrl(packets, layerCount, resolutionCount, componentCount, precinctCount);
                    break;
                case Jpeg2000ProgressionOrder.CPRL:
                    AddCprl(packets, layerCount, resolutionCount, componentCount, precinctCount);
                    break;
                default:
                    throw Jpeg2000Binary.CreateException($"JPEG 2000 progression order {progressionOrder} is not supported.");
            }

            return packets;
        }

        private static void AddLrcp(List<Jpeg2000PacketModel> packets, int layers, int resolutions, int components, int precincts)
        {
            for (var l = 0; l < layers; l++)
            for (var r = 0; r < resolutions; r++)
            for (var c = 0; c < components; c++)
            for (var p = 0; p < precincts; p++)
            {
                packets.Add(new Jpeg2000PacketModel(l, r, c, p));
            }
        }

        private static void AddRlcp(List<Jpeg2000PacketModel> packets, int layers, int resolutions, int components, int precincts)
        {
            for (var r = 0; r < resolutions; r++)
            for (var l = 0; l < layers; l++)
            for (var c = 0; c < components; c++)
            for (var p = 0; p < precincts; p++)
            {
                packets.Add(new Jpeg2000PacketModel(l, r, c, p));
            }
        }

        private static void AddRpcl(List<Jpeg2000PacketModel> packets, int layers, int resolutions, int components, int precincts)
        {
            for (var r = 0; r < resolutions; r++)
            for (var p = 0; p < precincts; p++)
            for (var c = 0; c < components; c++)
            for (var l = 0; l < layers; l++)
            {
                packets.Add(new Jpeg2000PacketModel(l, r, c, p));
            }
        }

        private static void AddPcrl(List<Jpeg2000PacketModel> packets, int layers, int resolutions, int components, int precincts)
        {
            for (var p = 0; p < precincts; p++)
            for (var c = 0; c < components; c++)
            for (var r = 0; r < resolutions; r++)
            for (var l = 0; l < layers; l++)
            {
                packets.Add(new Jpeg2000PacketModel(l, r, c, p));
            }
        }

        private static void AddCprl(List<Jpeg2000PacketModel> packets, int layers, int resolutions, int components, int precincts)
        {
            for (var c = 0; c < components; c++)
            for (var p = 0; p < precincts; p++)
            for (var r = 0; r < resolutions; r++)
            for (var l = 0; l < layers; l++)
            {
                packets.Add(new Jpeg2000PacketModel(l, r, c, p));
            }
        }
    }
}
