using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardPacketDecoder
    {
        private readonly byte[] _data;
        private readonly int _componentCount;
        private readonly int _layerCount;
        private readonly int _resolutionCount;
        private readonly Jpeg2000ProgressionOrder _progressionOrder;
        private readonly Jpeg2000StandardComponent[] _components;
        private readonly int _codeBlockWidth;
        private readonly int _codeBlockHeight;
        private readonly byte _codeBlockStyle;
        private readonly Dictionary<string, PacketHeaderContext> _contexts = new Dictionary<string, PacketHeaderContext>();
        private int _offset;

        public Jpeg2000StandardPacketDecoder(
            byte[] data,
            int componentCount,
            int layerCount,
            int resolutionCount,
            Jpeg2000ProgressionOrder progressionOrder,
            Jpeg2000StandardComponent[] components,
            int codeBlockWidth,
            int codeBlockHeight,
            byte codeBlockStyle)
        {
            _data = data ?? new byte[0];
            _componentCount = componentCount;
            _layerCount = layerCount;
            _resolutionCount = resolutionCount;
            _progressionOrder = progressionOrder;
            _components = components;
            _codeBlockWidth = codeBlockWidth;
            _codeBlockHeight = codeBlockHeight;
            _codeBlockStyle = codeBlockStyle;
        }

        public IReadOnlyList<Jpeg2000StandardPacket> Decode()
        {
            var packets = new List<Jpeg2000StandardPacket>();
            BuildCodeBlockMaps();

            if (_progressionOrder != Jpeg2000ProgressionOrder.LRCP)
            {
                throw Jpeg2000Binary.CreateException($"JPEG 2000 progression order {_progressionOrder} is not yet supported by the standard decoder.");
            }

            for (var layer = 0; layer < _layerCount; layer++)
            {
                for (var resolution = 0; resolution < _resolutionCount; resolution++)
                {
                    for (var component = 0; component < _componentCount; component++)
                    {
                        foreach (var precinct in _components[component].GetPrecincts(resolution))
                        {
                            packets.Add(DecodePacket(layer, resolution, component, precinct));
                        }
                    }
                }
            }

            return packets;
        }

        private Jpeg2000StandardPacket DecodePacket(int layer, int resolution, int componentIndex, int precinctIndex)
        {
            var packet = new Jpeg2000StandardPacket(layer, resolution, componentIndex, precinctIndex);
            if (_offset >= _data.Length)
            {
                return packet;
            }

            var reader = new Jpeg2000BioReader(_data, _offset);
            var present = reader.ReadBit() != 0;
            if (!present)
            {
                reader.AlignToByte();
                _offset = reader.BytesRead;
                return packet;
            }

            var component = _components[componentIndex];
            foreach (var band in component.GetBands(resolution, precinctIndex))
            {
                var context = GetContext(componentIndex, resolution, precinctIndex, band.Orientation, band.CodeBlockCountX, band.CodeBlockCountY);
                foreach (var block in band.CodeBlocks)
                {
                    var state = context.GetState(block.LocalX, block.LocalY);
                    var inclusion = DecodeInclusion(reader, context.Inclusion, state, block.LocalX, block.LocalY, layer);
                    if (!inclusion)
                    {
                        packet.Contributions.Add(new Jpeg2000StandardContribution(block, false, 0, Array.Empty<Jpeg2000StandardContributionSegment>(), state));
                        continue;
                    }

                    if (!state.Included)
                    {
                        state.Included = true;
                        state.NumLenBits = 3;
                        state.ZeroBitPlanes = context.ZeroBitPlanes.DecodeValue(reader, block.LocalX, block.LocalY);
                        state.StartNewSegment(_codeBlockStyle, first: true);
                    }

                    var passCount = DecodePassCount(reader);
                    state.TotalPasses += passCount;
                    var segments = DecodeContributionSegments(reader, passCount, state, _codeBlockStyle);
                    packet.Contributions.Add(new Jpeg2000StandardContribution(block, true, passCount, segments, state));
                }
            }

            reader.AlignToByte();
            _offset = reader.BytesRead;

            foreach (var contribution in packet.Contributions)
            {
                if (!contribution.Included || contribution.ByteLength == 0)
                {
                    continue;
                }

                foreach (var segment in contribution.Segments)
                {
                    if (segment.ByteLength == 0)
                    {
                        contribution.Block.AppendSegment(Array.Empty<byte>(), segment.PassCount, contribution.State.ZeroBitPlanes);
                        continue;
                    }

                    if (_offset + segment.ByteLength > _data.Length)
                    {
                        throw Jpeg2000Binary.CreateException("JPEG 2000 packet code-block data length exceeds tile data.");
                    }

                    var bytes = new byte[segment.ByteLength];
                    Buffer.BlockCopy(_data, _offset, bytes, 0, bytes.Length);
                    _offset += bytes.Length;
                    contribution.Block.AppendSegment(bytes, segment.PassCount, contribution.State.ZeroBitPlanes);
                }
            }

            return packet;
        }

        private PacketHeaderContext GetContext(int component, int resolution, int precinct, int orientation, int width, int height)
        {
            var key = component + ":" + resolution + ":" + precinct + ":" + orientation;
            if (!_contexts.TryGetValue(key, out var context))
            {
                context = new PacketHeaderContext(width, height);
                _contexts.Add(key, context);
            }

            return context;
        }

        private static bool DecodeInclusion(
            Jpeg2000BioReader reader,
            Jpeg2000StandardTagTree tree,
            CodeBlockPacketState state,
            int x,
            int y,
            int layer)
        {
            if (state.Included)
            {
                return reader.ReadBit() != 0;
            }

            return tree.Decode(reader, x, y, layer + 1);
        }

        private static int DecodePassCount(Jpeg2000BioReader reader)
        {
            if (reader.ReadBit() == 0)
            {
                return 1;
            }

            if (reader.ReadBit() == 0)
            {
                return 2;
            }

            var value = reader.ReadBits(2);
            if (value != 3)
            {
                return 3 + value;
            }

            value = reader.ReadBits(5);
            if (value != 31)
            {
                return 6 + value;
            }

            return 37 + reader.ReadBits(7);
        }

        private static Jpeg2000StandardContributionSegment[] DecodeContributionSegments(
            Jpeg2000BioReader reader,
            int passCount,
            CodeBlockPacketState state,
            byte codeBlockStyle)
        {
            var increment = 0;
            while (reader.ReadBit() != 0)
            {
                increment++;
            }

            state.NumLenBits += increment;
            var remainingPasses = passCount;
            var segments = new List<Jpeg2000StandardContributionSegment>();
            while (remainingPasses > 0)
            {
                var availablePasses = state.CurrentSegmentMaxPasses - state.CurrentSegmentPasses;
                if (availablePasses <= 0)
                {
                    state.StartNewSegment(codeBlockStyle, first: false);
                    availablePasses = state.CurrentSegmentMaxPasses;
                }

                var segmentPasses = Math.Min(availablePasses, remainingPasses);
                var length = reader.ReadBits(state.NumLenBits + Jpeg2000StandardGeometry.FloorLog2(segmentPasses));
                segments.Add(new Jpeg2000StandardContributionSegment(segmentPasses, length));
                state.CurrentSegmentPasses += segmentPasses;
                remainingPasses -= segmentPasses;
            }

            return segments.ToArray();
        }

        private void BuildCodeBlockMaps()
        {
            foreach (var component in _components)
            {
                component.BuildCodeBlocks(_codeBlockWidth, _codeBlockHeight);
            }
        }

        private sealed class PacketHeaderContext
        {
            private readonly CodeBlockPacketState[] _states;
            private readonly int _width;

            public PacketHeaderContext(int width, int height)
            {
                _width = Math.Max(1, width);
                Inclusion = new Jpeg2000StandardTagTree(width, height);
                ZeroBitPlanes = new Jpeg2000StandardTagTree(width, height);
                _states = new CodeBlockPacketState[Math.Max(1, width) * Math.Max(1, height)];
                for (var i = 0; i < _states.Length; i++)
                {
                    _states[i] = new CodeBlockPacketState();
                }
            }

            public Jpeg2000StandardTagTree Inclusion { get; }

            public Jpeg2000StandardTagTree ZeroBitPlanes { get; }

            public CodeBlockPacketState GetState(int x, int y)
            {
                return _states[(y * _width) + x];
            }
        }
    }

    internal sealed class Jpeg2000StandardPacket
    {
        public Jpeg2000StandardPacket(int layer, int resolution, int component, int precinct)
        {
            Layer = layer;
            Resolution = resolution;
            Component = component;
            Precinct = precinct;
            Contributions = new List<Jpeg2000StandardContribution>();
        }

        public int Layer { get; }

        public int Resolution { get; }

        public int Component { get; }

        public int Precinct { get; }

        public List<Jpeg2000StandardContribution> Contributions { get; }
    }

    internal sealed class Jpeg2000StandardContribution
    {
        public Jpeg2000StandardContribution(
            Jpeg2000StandardCodeBlock block,
            bool included,
            int passCount,
            IReadOnlyList<Jpeg2000StandardContributionSegment> segments,
            CodeBlockPacketState state)
        {
            Block = block;
            Included = included;
            PassCount = passCount;
            Segments = segments ?? Array.Empty<Jpeg2000StandardContributionSegment>();
            State = state;
        }

        public Jpeg2000StandardCodeBlock Block { get; }

        public bool Included { get; }

        public int PassCount { get; }

        public IReadOnlyList<Jpeg2000StandardContributionSegment> Segments { get; }

        public int ByteLength
        {
            get
            {
                var length = 0;
                foreach (var segment in Segments)
                {
                    length += segment.ByteLength;
                }

                return length;
            }
        }

        public CodeBlockPacketState State { get; }
    }

    internal readonly struct Jpeg2000StandardContributionSegment
    {
        public Jpeg2000StandardContributionSegment(int passCount, int byteLength)
        {
            PassCount = passCount;
            ByteLength = byteLength;
        }

        public int PassCount { get; }

        public int ByteLength { get; }
    }

    internal sealed class CodeBlockPacketState
    {
        public bool Included { get; set; }

        public int ZeroBitPlanes { get; set; }

        public int NumLenBits { get; set; }

        public int TotalPasses { get; set; }

        public int CurrentSegmentPasses { get; set; }

        public int CurrentSegmentMaxPasses { get; private set; }

        public void StartNewSegment(byte codeBlockStyle, bool first)
        {
            CurrentSegmentPasses = 0;
            if ((codeBlockStyle & 0x04) != 0)
            {
                CurrentSegmentMaxPasses = 1;
            }
            else if ((codeBlockStyle & 0x01) != 0)
            {
                CurrentSegmentMaxPasses = first ? 10 : CurrentSegmentMaxPasses == 1 || CurrentSegmentMaxPasses == 10 ? 2 : 1;
            }
            else
            {
                CurrentSegmentMaxPasses = 109;
            }
        }
    }
}
