using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000PacketContribution
    {
        public Jpeg2000PacketContribution(int codeBlockIndex, int codingPassCount, int byteLength)
        {
            CodeBlockIndex = codeBlockIndex;
            CodingPassCount = codingPassCount;
            ByteLength = byteLength;
        }

        public int CodeBlockIndex { get; }

        public int CodingPassCount { get; }

        public int ByteLength { get; }
    }

    public sealed class Jpeg2000ClassicPacket
    {
        public Jpeg2000ClassicPacket(
            int layerIndex,
            int resolutionLevel,
            int componentIndex,
            int precinctIndex,
            IReadOnlyList<Jpeg2000PacketContribution> contributions)
        {
            LayerIndex = layerIndex;
            ResolutionLevel = resolutionLevel;
            ComponentIndex = componentIndex;
            PrecinctIndex = precinctIndex;
            Contributions = contributions ?? new Jpeg2000PacketContribution[0];
        }

        public int LayerIndex { get; }

        public int ResolutionLevel { get; }

        public int ComponentIndex { get; }

        public int PrecinctIndex { get; }

        public IReadOnlyList<Jpeg2000PacketContribution> Contributions { get; }

        public bool IsEmpty => Contributions.Count == 0;

        public static Jpeg2000ClassicPacket Empty(int layerIndex, int resolutionLevel, int componentIndex, int precinctIndex)
        {
            return new Jpeg2000ClassicPacket(layerIndex, resolutionLevel, componentIndex, precinctIndex, new Jpeg2000PacketContribution[0]);
        }
    }

    public static class Jpeg2000ClassicPacketEncoder
    {
        public static byte[] Encode(Jpeg2000ClassicPacket packet)
        {
            var writer = new Jpeg2000BitWriter();
            writer.WriteBits((uint)packet.LayerIndex, 16);
            writer.WriteBits((uint)packet.ResolutionLevel, 16);
            writer.WriteBits((uint)packet.ComponentIndex, 16);
            writer.WriteBits((uint)packet.PrecinctIndex, 16);
            writer.WriteBits((uint)packet.Contributions.Count, 16);
            foreach (var contribution in packet.Contributions)
            {
                writer.WriteBits((uint)contribution.CodeBlockIndex, 16);
                writer.WriteBits((uint)contribution.CodingPassCount, 16);
                writer.WriteBits((uint)contribution.ByteLength, 32);
            }

            return writer.ToArray();
        }
    }

    public static class Jpeg2000ClassicPacketDecoder
    {
        public static Jpeg2000ClassicPacket Decode(byte[] bytes)
        {
            var reader = new Jpeg2000BitReader(bytes);
            var layerIndex = (int)reader.ReadBits(16);
            var resolutionLevel = (int)reader.ReadBits(16);
            var componentIndex = (int)reader.ReadBits(16);
            var precinctIndex = (int)reader.ReadBits(16);
            var contributionCount = (int)reader.ReadBits(16);
            var contributions = new Jpeg2000PacketContribution[contributionCount];
            for (var i = 0; i < contributions.Length; i++)
            {
                contributions[i] = new Jpeg2000PacketContribution(
                    (int)reader.ReadBits(16),
                    (int)reader.ReadBits(16),
                    (int)reader.ReadBits(32));
            }

            return new Jpeg2000ClassicPacket(layerIndex, resolutionLevel, componentIndex, precinctIndex, contributions);
        }
    }
}
