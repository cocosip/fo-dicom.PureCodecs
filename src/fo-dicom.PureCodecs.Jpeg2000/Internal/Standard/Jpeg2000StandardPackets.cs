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
        private readonly Jpeg2000CodingStyleDefault? _codingStyle;
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
            : this(data, componentCount, layerCount, resolutionCount, progressionOrder, components, codeBlockWidth, codeBlockHeight, codeBlockStyle, null)
        {
        }

        public Jpeg2000StandardPacketDecoder(
            byte[] data,
            int componentCount,
            int layerCount,
            int resolutionCount,
            Jpeg2000ProgressionOrder progressionOrder,
            Jpeg2000StandardComponent[] components,
            int codeBlockWidth,
            int codeBlockHeight,
            byte codeBlockStyle,
            Jpeg2000CodingStyleDefault? codingStyle)
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
            _codingStyle = codingStyle;
        }

        public IReadOnlyList<Jpeg2000StandardPacket> Decode()
        {
            var packets = new List<Jpeg2000StandardPacket>();
            BuildCodeBlockMaps();

            foreach (var packet in EnumeratePackets())
            {
                packets.Add(DecodePacket(packet.LayerIndex, packet.ResolutionLevel, packet.ComponentIndex, packet.PrecinctIndex));
            }

            return packets;
        }

        private IEnumerable<Jpeg2000PacketModel> EnumeratePackets()
        {
            switch (_progressionOrder)
            {
                case Jpeg2000ProgressionOrder.LRCP:
                    for (var l = 0; l < _layerCount; l++)
                    for (var r = 0; r < _resolutionCount; r++)
                    for (var c = 0; c < _componentCount; c++)
                    foreach (var p in _components[c].GetPrecincts(r))
                    {
                        yield return new Jpeg2000PacketModel(l, r, c, p);
                    }

                    yield break;
                case Jpeg2000ProgressionOrder.RLCP:
                    for (var r = 0; r < _resolutionCount; r++)
                    for (var l = 0; l < _layerCount; l++)
                    for (var c = 0; c < _componentCount; c++)
                    foreach (var p in _components[c].GetPrecincts(r))
                    {
                        yield return new Jpeg2000PacketModel(l, r, c, p);
                    }

                    yield break;
                case Jpeg2000ProgressionOrder.RPCL:
                    for (var r = 0; r < _resolutionCount; r++)
                    {
                        var maxPrecinct = MaxPrecinctIndex(r);
                        for (var p = 0; p <= maxPrecinct; p++)
                        for (var c = 0; c < _componentCount; c++)
                        for (var l = 0; l < _layerCount; l++)
                        {
                            if (_components[c].GetBands(r, p).Count != 0)
                            {
                                yield return new Jpeg2000PacketModel(l, r, c, p);
                            }
                        }
                    }

                    yield break;
                case Jpeg2000ProgressionOrder.PCRL:
                    for (var p = 0; p <= MaxPrecinctIndex(); p++)
                    for (var c = 0; c < _componentCount; c++)
                    for (var r = 0; r < _resolutionCount; r++)
                    for (var l = 0; l < _layerCount; l++)
                    {
                        if (_components[c].GetBands(r, p).Count != 0)
                        {
                            yield return new Jpeg2000PacketModel(l, r, c, p);
                        }
                    }

                    yield break;
                case Jpeg2000ProgressionOrder.CPRL:
                    for (var c = 0; c < _componentCount; c++)
                    for (var p = 0; p <= MaxPrecinctIndex(c); p++)
                    for (var r = 0; r < _resolutionCount; r++)
                    for (var l = 0; l < _layerCount; l++)
                    {
                        if (_components[c].GetBands(r, p).Count != 0)
                        {
                            yield return new Jpeg2000PacketModel(l, r, c, p);
                        }
                    }

                    yield break;
                default:
                    throw Jpeg2000Binary.CreateException($"JPEG 2000 progression order {_progressionOrder} is not supported.");
            }
        }

        private int MaxPrecinctIndex(int? resolution = null, int? component = null)
        {
            var max = 0;
            var c0 = component ?? 0;
            var c1 = component ?? (_componentCount - 1);
            var r0 = resolution ?? 0;
            var r1 = resolution ?? (_resolutionCount - 1);
            for (var c = c0; c <= c1; c++)
            for (var r = r0; r <= r1; r++)
            {
                foreach (var precinct in _components[c].GetPrecincts(r))
                {
                    if (precinct > max)
                    {
                        max = precinct;
                    }
                }
            }

            return max;
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
                    var inclusion = DecodeInclusion(reader, context, state, block.LocalX, block.LocalY, layer);
                    if (!inclusion)
                    {
                        packet.Contributions.Add(new Jpeg2000StandardContribution(block, false, 0, Array.Empty<Jpeg2000StandardContributionSegment>(), state));
                        continue;
                    }

                    if (!state.Included)
                    {
                        state.Included = true;
                        state.NumLenBits = 3;
                        state.ZeroBitPlanes = DecodeZeroBitPlanes(reader, context, block.LocalX, block.LocalY);
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
                if (!contribution.Included)
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
                context = new PacketHeaderContext(width, height, (_codeBlockStyle & 0x40) != 0);
                _contexts.Add(key, context);
            }

            return context;
        }

        private static bool DecodeInclusion(
            Jpeg2000BioReader reader,
            PacketHeaderContext context,
            CodeBlockPacketState state,
            int x,
            int y,
            int layer)
        {
            if (state.Included)
            {
                return reader.ReadBit() != 0;
            }

            return context.HighThroughput
                ? !context.DecodeHighThroughputEmptyBlock(reader, x, y)
                : context.Inclusion.Decode(reader, x, y, layer + 1);
        }

        private static int DecodeZeroBitPlanes(Jpeg2000BioReader reader, PacketHeaderContext context, int x, int y)
        {
            return context.HighThroughput
                ? context.DecodeHighThroughputMissingMsbs(reader, x, y)
                : context.ZeroBitPlanes.DecodeValue(reader, x, y);
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
            if ((codeBlockStyle & 0x40) != 0)
            {
                return DecodeHighThroughputContributionSegments(reader, passCount, state);
            }

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

        private static Jpeg2000StandardContributionSegment[] DecodeHighThroughputContributionSegments(
            Jpeg2000BioReader reader,
            int passCount,
            CodeBlockPacketState state)
        {
            var bits1 = 3;
            while (reader.ReadBit() != 0)
            {
                bits1++;
            }

            var cleanupLength = reader.ReadBits(bits1);
            if (cleanupLength < 2 || cleanupLength >= 65535)
            {
                throw Jpeg2000Binary.CreateException($"HTJ2K cleanup segment length is invalid. length={cleanupLength}, passCount={passCount}, bits1={bits1}, bytesRead={reader.BytesRead}, bitCount={reader.BitCount}.");
            }

            state.CurrentSegmentPasses += Math.Min(1, passCount);
            if (passCount <= 1)
            {
                return new[] { new Jpeg2000StandardContributionSegment(1, cleanupLength) };
            }

            var refinementLength = reader.ReadBits(bits1 + (passCount > 2 ? 1 : 0));
            if (refinementLength >= 2047)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K refinement segment length is invalid.");
            }

            state.CurrentSegmentPasses += passCount - 1;
            return new[]
            {
                new Jpeg2000StandardContributionSegment(1, cleanupLength),
                new Jpeg2000StandardContributionSegment(passCount - 1, refinementLength)
            };
        }

        private void BuildCodeBlockMaps()
        {
            foreach (var component in _components)
            {
                component.BuildCodeBlocks(_codeBlockWidth, _codeBlockHeight, _codingStyle);
            }
        }

        private sealed class PacketHeaderContext
        {
            private readonly CodeBlockPacketState[] _states;
            private readonly int _width;
            private readonly HtPacketTagTree? _htInclusion;
            private readonly HtPacketTagTree? _htInclusionFlags;
            private readonly HtPacketTagTree? _htMissingMsbs;
            private readonly HtPacketTagTree? _htMissingMsbsFlags;

            public PacketHeaderContext(int width, int height, bool highThroughput)
            {
                _width = Math.Max(1, width);
                Inclusion = new Jpeg2000StandardTagTree(width, height);
                ZeroBitPlanes = new Jpeg2000StandardTagTree(width, height);
                _states = new CodeBlockPacketState[Math.Max(1, width) * Math.Max(1, height)];
                for (var i = 0; i < _states.Length; i++)
                {
                    _states[i] = new CodeBlockPacketState();
                }

                HighThroughput = highThroughput;
                if (highThroughput)
                {
                    _htInclusion = new HtPacketTagTree(width, height);
                    _htInclusionFlags = new HtPacketTagTree(width, height);
                    _htMissingMsbs = new HtPacketTagTree(width, height);
                    _htMissingMsbsFlags = new HtPacketTagTree(width, height);
                }
            }

            public Jpeg2000StandardTagTree Inclusion { get; }

            public Jpeg2000StandardTagTree ZeroBitPlanes { get; }

            public bool HighThroughput { get; }

            public CodeBlockPacketState GetState(int x, int y)
            {
                return _states[(y * _width) + x];
            }

            public bool DecodeHighThroughputEmptyBlock(Jpeg2000BioReader reader, int x, int y)
            {
                if (_htInclusion == null || _htInclusionFlags == null)
                {
                    throw Jpeg2000Binary.CreateException("HTJ2K inclusion tree is not initialized.");
                }

                for (var levelPlusOne = _htInclusion.Levels; levelPlusOne > 0; levelPlusOne--)
                {
                    var level = levelPlusOne - 1;
                    var emptyBlock = _htInclusion.Get(x >> level, y >> level, level) == 1;
                    if (emptyBlock)
                    {
                        return true;
                    }

                    if (_htInclusionFlags.Get(x >> level, y >> level, level) == 0)
                    {
                        var bit = reader.ReadBit();
                        emptyBlock = bit == 0;
                        _htInclusion.Set(x >> level, y >> level, level, 1 - bit);
                        _htInclusionFlags.Set(x >> level, y >> level, level, 1);
                    }

                    if (emptyBlock)
                    {
                        return true;
                    }
                }

                return false;
            }

            public int DecodeHighThroughputMissingMsbs(Jpeg2000BioReader reader, int x, int y)
            {
                if (_htMissingMsbs == null || _htMissingMsbsFlags == null)
                {
                    throw Jpeg2000Binary.CreateException("HTJ2K missing-MSB tree is not initialized.");
                }

                var missing = 0;
                for (var levelPlusOne = _htMissingMsbs.Levels; levelPlusOne > 0; levelPlusOne--)
                {
                    var level = levelPlusOne - 1;
                    missing = _htMissingMsbs.Get(x >> levelPlusOne, y >> levelPlusOne, levelPlusOne);
                    if (_htMissingMsbsFlags.Get(x >> level, y >> level, level) == 0)
                    {
                        var bit = 0;
                        while (bit == 0)
                        {
                            bit = reader.ReadBit();
                            missing += 1 - bit;
                        }

                        _htMissingMsbs.Set(x >> level, y >> level, level, missing);
                        _htMissingMsbsFlags.Set(x >> level, y >> level, level, 1);
                    }
                }

                return missing;
            }
        }

        private sealed class HtPacketTagTree
        {
            private readonly int[] _levelWidths;
            private readonly int[] _levelHeights;
            private readonly int[][] _values;

            public HtPacketTagTree(int width, int height)
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                var levels = 1 + Math.Max(Log2Ceil(width), Log2Ceil(height));
                Levels = levels;
                _levelWidths = new int[levels + 1];
                _levelHeights = new int[levels + 1];
                _values = new int[levels + 1][];
                for (var level = 0; level <= levels; level++)
                {
                    var w = (width + (1 << level) - 1) >> level;
                    var h = (height + (1 << level) - 1) >> level;
                    _levelWidths[level] = Math.Max(1, w);
                    _levelHeights[level] = Math.Max(1, h);
                    _values[level] = new int[_levelWidths[level] * _levelHeights[level]];
                }
            }

            public int Levels { get; }

            public int Get(int x, int y, int level)
            {
                if (level < 0 || level >= _values.Length)
                {
                    return 0;
                }

                x = Math.Min(Math.Max(0, x), _levelWidths[level] - 1);
                y = Math.Min(Math.Max(0, y), _levelHeights[level] - 1);
                return _values[level][x + (y * _levelWidths[level])];
            }

            public void Set(int x, int y, int level, int value)
            {
                if (level < 0 || level >= _values.Length)
                {
                    return;
                }

                x = Math.Min(Math.Max(0, x), _levelWidths[level] - 1);
                y = Math.Min(Math.Max(0, y), _levelHeights[level] - 1);
                _values[level][x + (y * _levelWidths[level])] = value;
            }

            private static int Log2Ceil(int value)
            {
                var floor = 0;
                var probe = value;
                while (probe > 1)
                {
                    probe >>= 1;
                    floor++;
                }

                return (value & (value - 1)) == 0 ? floor : floor + 1;
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
