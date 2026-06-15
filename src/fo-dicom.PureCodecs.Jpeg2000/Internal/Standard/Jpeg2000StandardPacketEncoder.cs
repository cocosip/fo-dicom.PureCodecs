using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000EncodedBlock
    {
        public Jpeg2000EncodedBlock(int x, int y, int gridWidth, int gridHeight, int zeroBitPlanes, int passCount, byte[] data)
            : this(x, y, gridWidth, gridHeight, zeroBitPlanes, passCount, data, null)
        {
        }

        public Jpeg2000EncodedBlock(int x, int y, int gridWidth, int gridHeight, int zeroBitPlanes, int passCount, byte[] data, int[]? passLengths)
            : this(x, y, gridWidth, gridHeight, zeroBitPlanes, passCount, data, passLengths, null)
        {
        }

        public Jpeg2000EncodedBlock(int x, int y, int gridWidth, int gridHeight, int zeroBitPlanes, int passCount, byte[] data, int[]? passLengths, byte[][]? passSnapshots)
            : this(x, y, gridWidth, gridHeight, zeroBitPlanes, passCount, data, passLengths, passSnapshots, resolution: 0, orientation: 0)
        {
        }

        public Jpeg2000EncodedBlock(
            int x,
            int y,
            int gridWidth,
            int gridHeight,
            int zeroBitPlanes,
            int passCount,
            byte[] data,
            int[]? passLengths,
            byte[][]? passSnapshots,
            int resolution,
            int orientation)
        {
            X = x;
            Y = y;
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            ZeroBitPlanes = zeroBitPlanes;
            PassCount = passCount;
            Data = data ?? new byte[0];
            PassLengths = passLengths ?? new int[0];
            PassSnapshots = passSnapshots ?? new byte[0][];
            Resolution = resolution;
            Orientation = orientation;
        }

        public int X { get; }

        public int Y { get; }

        public int GridWidth { get; }

        public int GridHeight { get; }

        public int ZeroBitPlanes { get; }

        public int PassCount { get; }

        public byte[] Data { get; }

        public int[] PassLengths { get; }

        public byte[][] PassSnapshots { get; }

        public int Resolution { get; }

        public int Orientation { get; }

        public Jpeg2000EncodedBlock TruncateToPasses(int passCount)
        {
            if (passCount <= 0 || Data.Length == 0)
            {
                return new Jpeg2000EncodedBlock(X, Y, GridWidth, GridHeight, ZeroBitPlanes, 0, new byte[0], PassLengths, PassSnapshots, Resolution, Orientation);
            }

            if (passCount >= PassCount || PassLengths.Length == 0)
            {
                return this;
            }

            var index = Math.Min(passCount, PassLengths.Length) - 1;
            if (PassSnapshots.Length > index && PassSnapshots[index].Length > 0)
            {
                return new Jpeg2000EncodedBlock(X, Y, GridWidth, GridHeight, ZeroBitPlanes, passCount, PassSnapshots[index], PassLengths, PassSnapshots, Resolution, Orientation);
            }

            var length = PassLengths[index];
            if (length <= 0)
            {
                return new Jpeg2000EncodedBlock(X, Y, GridWidth, GridHeight, ZeroBitPlanes, 0, new byte[0], PassLengths, PassSnapshots, Resolution, Orientation);
            }

            if (length > Data.Length)
            {
                length = Data.Length;
            }

            var data = new byte[length];
            Buffer.BlockCopy(Data, 0, data, 0, data.Length);
            return new Jpeg2000EncodedBlock(X, Y, GridWidth, GridHeight, ZeroBitPlanes, passCount, data, PassLengths, PassSnapshots, Resolution, Orientation);
        }
    }

    internal static class Jpeg2000StandardPacketEncoder
    {
        public static byte[] EncodeSingleLayerPacket(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
        {
            return CreateLayeredEncoder(blocks).EncodePacket(blocks, layerIndex: 0);
        }

        public static byte[] EncodeLayeredPackets(IReadOnlyList<Jpeg2000EncodedBlock[]> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return new byte[0];
            }

            var encoder = CreateLayeredEncoder(layers[0]);
            var bytes = new List<byte>();
            for (var layer = 0; layer < layers.Count; layer++)
            {
                bytes.AddRange(encoder.EncodePacket(layers[layer], layer));
            }

            return bytes.ToArray();
        }

        public static LayeredPacketEncoder CreateLayeredEncoder(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
        {
            return new LayeredPacketEncoder(blocks);
        }

        public sealed class LayeredPacketEncoder
        {
            private readonly List<BandPacketState> _bands = new List<BandPacketState>();

            public LayeredPacketEncoder(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
            {
                var offset = 0;
                blocks = blocks ?? Array.Empty<Jpeg2000EncodedBlock>();
                while (offset < blocks.Count)
                {
                    var gridWidth = blocks[offset].GridWidth;
                    var gridHeight = blocks[offset].GridHeight;
                    var count = gridWidth * gridHeight;
                    _bands.Add(new BandPacketState(offset, count, gridWidth, gridHeight));
                    offset += count;
                }
            }

            public byte[] EncodePacket(IReadOnlyList<Jpeg2000EncodedBlock> blocks, int layerIndex)
            {
                blocks = blocks ?? Array.Empty<Jpeg2000EncodedBlock>();
                if (!HasContribution(blocks))
                {
                    return new byte[] { 0 };
                }

                var header = new Jpeg2000PacketBitWriter();
                header.WriteBit(1);
                var body = new List<byte>();
                foreach (var band in _bands)
                {
                    EncodeBandLayer(header, body, blocks, band, layerIndex);
                }

                header.Align();
                var bytes = new List<byte>(header.Bytes.Count + body.Count);
                bytes.AddRange(header.Bytes);
                bytes.AddRange(body);
                return bytes.ToArray();
            }

            private bool HasContribution(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
            {
                foreach (var band in _bands)
                {
                    for (var i = 0; i < band.Count && band.Offset + i < blocks.Count; i++)
                    {
                        var block = blocks[band.Offset + i];
                        var state = band.States[i];
                        if (block.PassCount > state.PassCount && block.Data.Length > state.ByteLength)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static void EncodeBandLayer(
                Jpeg2000PacketBitWriter header,
                List<byte> body,
                IReadOnlyList<Jpeg2000EncodedBlock> blocks,
                BandPacketState band,
                int layerIndex)
            {
                for (var i = 0; i < band.Count && band.Offset + i < blocks.Count; i++)
                {
                    var block = blocks[band.Offset + i];
                    var state = band.States[i];
                    var passDelta = Math.Max(0, block.PassCount - state.PassCount);
                    var byteDelta = Math.Max(0, block.Data.Length - state.ByteLength);
                    if (!state.Included && passDelta > 0 && byteDelta > 0)
                    {
                        band.Inclusion.SetValue(block.X, block.Y, layerIndex);
                        band.ZeroBitPlanes.SetValue(block.X, block.Y, block.ZeroBitPlanes);
                    }
                }

                for (var i = 0; i < band.Count && band.Offset + i < blocks.Count; i++)
                {
                    var block = blocks[band.Offset + i];
                    var state = band.States[i];
                    var passDelta = Math.Max(0, block.PassCount - state.PassCount);
                    var byteDelta = Math.Max(0, block.Data.Length - state.ByteLength);
                    var contributes = passDelta > 0 && byteDelta > 0;

                    if (!state.Included)
                    {
                        band.Inclusion.Encode(header, block.X, block.Y, layerIndex + 1);
                        if (!contributes)
                        {
                            continue;
                        }

                        band.ZeroBitPlanes.Encode(header, block.X, block.Y, 32);
                        state.Included = true;
                        state.NumLenBits = 3;
                    }
                    else
                    {
                        header.WriteBit(contributes ? 1 : 0);
                        if (!contributes)
                        {
                            continue;
                        }
                    }

                    EncodePassCount(header, passDelta);
                    var required = RequiredLengthBits(byteDelta, passDelta);
                    while (state.NumLenBits < required)
                    {
                        header.WriteBit(1);
                        state.NumLenBits++;
                    }

                    header.WriteBit(0);
                    header.WriteBits(byteDelta, state.NumLenBits + FloorLog2(passDelta));
                    for (var index = state.ByteLength; index < block.Data.Length; index++)
                    {
                        body.Add(block.Data[index]);
                    }

                    state.PassCount = block.PassCount;
                    state.ByteLength = block.Data.Length;
                }
            }
        }

        private sealed class BandPacketState
        {
            public BandPacketState(int offset, int count, int width, int height)
            {
                Offset = offset;
                Count = count;
                Inclusion = new Jpeg2000PacketTagTree(width, height);
                ZeroBitPlanes = new Jpeg2000PacketTagTree(width, height);
                States = new CodeBlockLayerState[Math.Max(1, count)];
                for (var i = 0; i < States.Length; i++)
                {
                    States[i] = new CodeBlockLayerState();
                }
            }

            public int Offset { get; }

            public int Count { get; }

            public Jpeg2000PacketTagTree Inclusion { get; }

            public Jpeg2000PacketTagTree ZeroBitPlanes { get; }

            public CodeBlockLayerState[] States { get; }
        }

        private sealed class CodeBlockLayerState
        {
            public bool Included { get; set; }

            public int PassCount { get; set; }

            public int ByteLength { get; set; }

            public int NumLenBits { get; set; }
        }

        public static byte[] EncodeLayerPacket(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return new byte[] { 0 };
            }

            var header = new Jpeg2000PacketBitWriter();
            header.WriteBit(1);
            var body = new List<byte>();
            var offset = 0;
            while (offset < blocks.Count)
            {
                var gridWidth = blocks[offset].GridWidth;
                var gridHeight = blocks[offset].GridHeight;
                var count = gridWidth * gridHeight;
                var bandBlocks = new List<Jpeg2000EncodedBlock>(count);
                for (var i = 0; i < count && offset + i < blocks.Count; i++)
                {
                    bandBlocks.Add(blocks[offset + i]);
                }

                EncodeBand(header, body, bandBlocks, gridWidth, gridHeight);
                offset += count;
            }

            header.Align();
            var bytes = new List<byte>(header.Bytes.Count + body.Count);
            bytes.AddRange(header.Bytes);
            bytes.AddRange(body);
            return bytes.ToArray();
        }

        private static void EncodeBand(
            Jpeg2000PacketBitWriter header,
            List<byte> body,
            IReadOnlyList<Jpeg2000EncodedBlock> blocks,
            int gridWidth,
            int gridHeight)
        {
            var inclusion = new Jpeg2000PacketTagTree(gridWidth, gridHeight);
            var zeroBitPlanes = new Jpeg2000PacketTagTree(gridWidth, gridHeight);
            foreach (var block in blocks)
            {
                inclusion.SetValue(block.X, block.Y, block.PassCount > 0 && block.Data.Length > 0 ? 0 : 999);
                zeroBitPlanes.SetValue(block.X, block.Y, block.ZeroBitPlanes);
            }

            foreach (var block in blocks)
            {
                if (block.PassCount <= 0 || block.Data.Length == 0)
                {
                    inclusion.Encode(header, block.X, block.Y, 1);
                    continue;
                }

                inclusion.Encode(header, block.X, block.Y, 1);
                zeroBitPlanes.Encode(header, block.X, block.Y, 32);
                EncodePassCount(header, block.PassCount);
                var numLenBits = 3;
                var required = RequiredLengthBits(block.Data.Length, block.PassCount);
                while (numLenBits < required)
                {
                    header.WriteBit(1);
                    numLenBits++;
                }

                header.WriteBit(0);
                header.WriteBits(block.Data.Length, numLenBits + FloorLog2(block.PassCount));
                body.AddRange(block.Data);
            }
        }

        private static void EncodePassCount(Jpeg2000PacketBitWriter writer, int passCount)
        {
            if (passCount == 1)
            {
                writer.WriteBit(0);
                return;
            }

            if (passCount == 2)
            {
                writer.WriteBit(1);
                writer.WriteBit(0);
                return;
            }

            if (passCount <= 5)
            {
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBits(passCount - 3, 2);
                return;
            }

            if (passCount <= 36)
            {
                writer.WriteBit(1);
                writer.WriteBit(1);
                writer.WriteBits(3, 2);
                writer.WriteBits(passCount - 6, 5);
                return;
            }

            writer.WriteBit(1);
            writer.WriteBit(1);
            writer.WriteBits(3, 2);
            writer.WriteBits(31, 5);
            writer.WriteBits(passCount - 37, 7);
        }

        private static int RequiredLengthBits(int length, int passCount)
        {
            var bits = 0;
            var value = Math.Max(0, length);
            while (value > 0)
            {
                bits++;
                value >>= 1;
            }

            return Math.Max(3, bits - FloorLog2(passCount));
        }

        private static int FloorLog2(int value)
        {
            var result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }

            return result;
        }
    }

    internal sealed class Jpeg2000PacketBitWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private byte _buffer;
        private int _bitCount;

        public IReadOnlyList<byte> Bytes => _bytes;

        public void WriteBit(int bit)
        {
            if (bit != 0)
            {
                _buffer |= (byte)(1 << (7 - _bitCount));
            }

            _bitCount++;
            if (_bitCount == 8)
            {
                FlushByte();
            }
        }

        public void WriteBits(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                WriteBit((value >> i) & 1);
            }
        }

        public void Align()
        {
            if (_bitCount > 0)
            {
                FlushByte();
            }
        }

        private void FlushByte()
        {
            var value = _buffer;
            _bytes.Add(value);
            _buffer = 0;
            _bitCount = value == 0xFF ? 1 : 0;
        }
    }

    internal sealed class Jpeg2000PacketTagTree
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _levels;
        private readonly int[][] _values;
        private readonly int[][] _low;
        private readonly bool[][] _known;
        private readonly int[] _levelWidths;
        private readonly int[] _levelHeights;

        public Jpeg2000PacketTagTree(int width, int height)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            var levels = 1;
            var w = _width;
            var h = _height;
            while (w > 1 || h > 1)
            {
                levels++;
                w = (w + 1) / 2;
                h = (h + 1) / 2;
            }

            _levels = levels;
            _levelWidths = new int[levels];
            _levelHeights = new int[levels];
            _values = new int[levels][];
            _low = new int[levels][];
            _known = new bool[levels][];
            w = _width;
            h = _height;
            for (var level = 0; level < levels; level++)
            {
                _levelWidths[level] = w;
                _levelHeights[level] = h;
                var size = w * h;
                _values[level] = new int[size];
                _low[level] = new int[size];
                _known[level] = new bool[size];
                for (var i = 0; i < size; i++)
                {
                    _values[level][i] = 999;
                }

                w = (w + 1) / 2;
                h = (h + 1) / 2;
            }
        }

        public void SetValue(int x, int y, int value)
        {
            var px = x;
            var py = y;
            for (var level = 0; level < _levels; level++)
            {
                var index = (py * _levelWidths[level]) + px;
                if (_values[level][index] <= value)
                {
                    break;
                }

                _values[level][index] = value;
                px /= 2;
                py /= 2;
            }
        }

        public void Encode(Jpeg2000PacketBitWriter writer, int x, int y, int threshold)
        {
            var stack = new TagNode[_levels];
            var px = x;
            var py = y;
            for (var level = 0; level < _levels; level++)
            {
                stack[level] = new TagNode(level, (py * _levelWidths[level]) + px);
                px /= 2;
                py /= 2;
            }

            var low = 0;
            for (var i = stack.Length - 1; i >= 0; i--)
            {
                var node = stack[i];
                if (low > _low[node.Level][node.Index])
                {
                    _low[node.Level][node.Index] = low;
                }
                else
                {
                    low = _low[node.Level][node.Index];
                }

                while (low < threshold)
                {
                    if (low >= _values[node.Level][node.Index])
                    {
                        if (!_known[node.Level][node.Index])
                        {
                            writer.WriteBit(1);
                            _known[node.Level][node.Index] = true;
                        }

                        break;
                    }

                    writer.WriteBit(0);
                    low++;
                }

                _low[node.Level][node.Index] = low;
            }
        }

        private readonly struct TagNode
        {
            public TagNode(int level, int index)
            {
                Level = level;
                Index = index;
            }

            public int Level { get; }

            public int Index { get; }
        }
    }
}
