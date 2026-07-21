using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000HtTileEncoder
    {
        private const int CodeBlockWidth = 64;
        private const int CodeBlockHeight = 64;

        public byte[] EncodeLossless(DicomPixelData pixelData, byte[] frame, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
            return Concat(EncodeLosslessTileParts(pixelData, frame, progressionOrder, decompositionLevels, pixelData.BitsStored));
        }

        public byte[][] EncodeLosslessTileParts(DicomPixelData pixelData, byte[] frame, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels, int codingBitDepth)
        {
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K encoder currently supports monochrome and RGB frames only.");
            }

            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
            var components = ReadComponentSamples(frame, pixelData.SamplesPerPixel, pixelData.BitsAllocated, codingBitDepth, isSigned);
            ApplyForwardLevelShift(components, codingBitDepth, isSigned);
            if (components.Length == 3)
            {
                ApplyForwardRct(components);
            }

            var encodedComponents = new List<List<List<Jpeg2000EncodedBlock>>>(components.Length);
            foreach (var component in components)
            {
                var coefficients = Jpeg2000StandardWavelet.Forward53(component, pixelData.Width, pixelData.Height, decompositionLevels, 0, 0);
                encodedComponents.Add(BuildHtCodeBlocksByResolution(coefficients, pixelData.Width, pixelData.Height, codingBitDepth, components.Length == 3, decompositionLevels));
            }

            return EncodePacketsByResolution(encodedComponents, progressionOrder, decompositionLevels);
        }

        public byte[] EncodeLossy(DicomPixelData pixelData, byte[] frame, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels, ushort[] encodedSteps)
        {
            return Concat(EncodeLossyTileParts(pixelData, frame, progressionOrder, decompositionLevels, encodedSteps, pixelData.BitsStored));
        }

        public byte[][] EncodeLossyTileParts(DicomPixelData pixelData, byte[] frame, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels, ushort[] encodedSteps, int codingBitDepth)
        {
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K encoder currently supports monochrome and RGB frames only.");
            }

            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
            var components = ReadComponentSamples(frame, pixelData.SamplesPerPixel, pixelData.BitsAllocated, codingBitDepth, isSigned);
            ApplyForwardLevelShift(components, codingBitDepth, isSigned);
            var values = CreateDoubleComponents(components);
            if (values.Length == 3)
            {
                ApplyForwardIct(values);
            }

            ScaleComponents(values, 1.0 / (1 << codingBitDepth));

            var encodedComponents = new List<List<List<Jpeg2000EncodedBlock>>>(values.Length);
            foreach (var component in values)
            {
                Jpeg2000StandardIrreversibleWavelet.Forward97(component, pixelData.Width, pixelData.Height, decompositionLevels);
                encodedComponents.Add(BuildHtIrreversibleCodeBlocksByResolution(component, pixelData.Width, pixelData.Height, codingBitDepth, decompositionLevels, encodedSteps));
            }

            return EncodePacketsByResolution(encodedComponents, progressionOrder, decompositionLevels);
        }

        private static byte[] EncodePackets(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
            return Concat(EncodePacketsByResolution(components, progressionOrder, decompositionLevels));
        }

        private static byte[][] EncodePacketsByResolution(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
            var bytesByResolution = new List<byte>[decompositionLevels + 1];
            for (var i = 0; i < bytesByResolution.Length; i++)
            {
                bytesByResolution[i] = new List<byte>();
            }

            foreach (var packet in EnumeratePackets(components, progressionOrder, decompositionLevels))
            {
                var blocks = components[packet.ComponentIndex][packet.ResolutionLevel];
                if (blocks.Count == 0)
                {
                    bytesByResolution[packet.ResolutionLevel].Add(0);
                    continue;
                }

                var packetBlocks = FilterPrecinct(blocks, packet.PrecinctIndex);
                bytesByResolution[packet.ResolutionLevel].AddRange(EncodeHighThroughputPacket(packetBlocks));
            }

            var result = new byte[bytesByResolution.Length][];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = bytesByResolution[i].ToArray();
            }

            return result;
        }

        private static byte[] Concat(IReadOnlyList<byte[]> parts)
        {
            var length = 0;
            foreach (var part in parts)
            {
                length += part.Length;
            }

            var result = new byte[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static byte[] EncodeHighThroughputPacket(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
        {
            blocks = blocks ?? Array.Empty<Jpeg2000EncodedBlock>();
            var bands = SplitBands(blocks);
            var header = new Jpeg2000PacketBitWriter();
            var body = new List<byte>();
            var coded = false;
            var skippedSubbands = 0;

            foreach (var band in bands)
            {
                if (band.Count == 0)
                {
                    continue;
                }

                var inclusion = HtPacketTree.FromInclusion(band);
                if (inclusion.IsEmpty)
                {
                    if (coded)
                    {
                        header.WriteBit(0);
                    }
                    else
                    {
                        skippedSubbands++;
                    }

                    continue;
                }

                if (!coded)
                {
                    header.WriteBit(1);
                    for (var i = 0; i < skippedSubbands; i++)
                    {
                        header.WriteBit(0);
                    }

                    coded = true;
                }

                var inclusionFlags = new HtPacketTree(band.GridWidth, band.GridHeight, 0);
                var mmsb = HtPacketTree.FromMissingMostSignificantBits(band);
                var mmsbFlags = new HtPacketTree(band.GridWidth, band.GridHeight, 0);
                for (var y = 0; y < band.GridHeight; y++)
                {
                    for (var x = 0; x < band.GridWidth; x++)
                    {
                        var block = band.GetBlock(x, y);
                        var emptyBlock = false;
                        for (var levelPlusOne = inclusion.Levels; levelPlusOne > 0; levelPlusOne--)
                        {
                            var level = levelPlusOne - 1;
                            if (inclusionFlags.Get(x >> level, y >> level, level) == 0)
                            {
                                var skipped = inclusion.Get(x >> level, y >> level, level) - inclusion.Get(x >> levelPlusOne, y >> levelPlusOne, levelPlusOne);
                                header.WriteBit(1 - skipped);
                                inclusionFlags.Set(x >> level, y >> level, level, 1);
                            }

                            emptyBlock = inclusion.Get(x >> level, y >> level, level) != 0;
                            if (emptyBlock)
                            {
                                break;
                            }
                        }

                        if (emptyBlock || block == null || block.PassCount == 0 || block.Data.Length == 0)
                        {
                            continue;
                        }

                        var missing = 0;
                        for (var levelPlusOne = mmsb.Levels; levelPlusOne > 0; levelPlusOne--)
                        {
                            var level = levelPlusOne - 1;
                            missing = mmsb.Get(x >> levelPlusOne, y >> levelPlusOne, levelPlusOne);
                            if (mmsbFlags.Get(x >> level, y >> level, level) != 0)
                            {
                                continue;
                            }

                            var zeros = mmsb.Get(x >> level, y >> level, level) - missing;
                            for (var i = 0; i < zeros; i++)
                            {
                                header.WriteBit(0);
                            }

                            header.WriteBit(1);
                            mmsbFlags.Set(x >> level, y >> level, level, 1);
                        }

                        header.WriteBit(0);
                        var bits = Math.Max(0, BitLength(block.Data.Length) - 3);
                        for (var i = 0; i < bits; i++)
                        {
                            header.WriteBit(1);
                        }

                        header.WriteBit(0);
                        header.WriteBits(block.Data.Length, bits + 3);
                    }
                }

                foreach (var block in band.Blocks)
                {
                    if (block.PassCount > 0 && block.Data.Length > 0)
                    {
                        body.AddRange(block.Data);
                    }
                }
            }

            if (!coded)
            {
                return new byte[] { 0 };
            }

            header.Align();
            var bytes = new List<byte>(header.Bytes.Count + body.Count);
            bytes.AddRange(header.Bytes);
            bytes.AddRange(body);
            return bytes.ToArray();
        }

        private static List<HtPacketBand> SplitBands(IReadOnlyList<Jpeg2000EncodedBlock> blocks)
        {
            var bands = new List<HtPacketBand>();
            var offset = 0;
            while (offset < blocks.Count)
            {
                var block = blocks[offset];
                var count = Math.Max(1, block.GridWidth * block.GridHeight);
                var band = new HtPacketBand(block.GridWidth, block.GridHeight);
                for (var i = 0; i < count && offset + i < blocks.Count; i++)
                {
                    band.Add(blocks[offset + i]);
                }

                bands.Add(band);
                offset += count;
            }

            return bands;
        }

        private static int BitLength(int value)
        {
            var bits = 0;
            while (value > 0)
            {
                bits++;
                value >>= 1;
            }

            return bits;
        }

        private static IReadOnlyList<Jpeg2000EncodedBlock> FilterPrecinct(IReadOnlyList<Jpeg2000EncodedBlock> blocks, int precinct)
        {
            var filtered = new List<Jpeg2000EncodedBlock>();
            foreach (var block in blocks)
            {
                if (block.Precinct == precinct)
                {
                    filtered.Add(block);
                }
            }

            return filtered;
        }

        private static IEnumerable<Jpeg2000PacketModel> EnumeratePackets(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
            switch (progressionOrder)
            {
                case Jpeg2000ProgressionOrder.LRCP:
                    for (var layer = 0; layer < 1; layer++)
                    for (var resolution = 0; resolution <= decompositionLevels; resolution++)
                    for (var component = 0; component < components.Count; component++)
                    for (var precinct = 0; precinct <= MaxPrecinct(components, resolution, component); precinct++)
                    {
                        if (HasPrecinct(components[component][resolution], precinct))
                        {
                            yield return new Jpeg2000PacketModel(layer, resolution, component, precinct);
                        }
                    }

                    yield break;
                case Jpeg2000ProgressionOrder.RPCL:
                    for (var resolution = 0; resolution <= decompositionLevels; resolution++)
                    for (var precinct = 0; precinct <= MaxPrecinct(components, resolution); precinct++)
                    for (var component = 0; component < components.Count; component++)
                    for (var layer = 0; layer < 1; layer++)
                    {
                        if (HasPrecinct(components[component][resolution], precinct))
                        {
                            yield return new Jpeg2000PacketModel(layer, resolution, component, precinct);
                        }
                    }

                    yield break;
                default:
                    foreach (var packet in Jpeg2000ProgressionOrderIterator.Enumerate(progressionOrder, 1, decompositionLevels + 1, components.Count, 1))
                    {
                        yield return packet;
                    }

                    yield break;
            }
        }

        private static bool HasPrecinct(IReadOnlyList<Jpeg2000EncodedBlock> blocks, int precinct)
        {
            if (blocks.Count == 0 && precinct == 0)
            {
                return true;
            }

            foreach (var block in blocks)
            {
                if (block.Precinct == precinct)
                {
                    return true;
                }
            }

            return false;
        }

        private static int MaxPrecinct(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components, int resolution, int? component = null)
        {
            var max = 0;
            var c0 = component ?? 0;
            var c1 = component ?? components.Count - 1;
            for (var c = c0; c <= c1; c++)
            {
                foreach (var block in components[c][resolution])
                {
                    if (block.Precinct > max)
                    {
                        max = block.Precinct;
                    }
                }
            }

            return max;
        }

        private static List<List<Jpeg2000EncodedBlock>> BuildHtCodeBlocksByResolution(int[] coefficients, int width, int height, int precision, bool usesMultipleComponentTransform, int decompositionLevels)
        {
            var result = new List<List<Jpeg2000EncodedBlock>>();
            for (var resolution = 0; resolution <= decompositionLevels; resolution++)
            {
                result.Add(BuildHtCodeBlocksForResolution(coefficients, width, height, precision, resolution, usesMultipleComponentTransform, decompositionLevels));
            }

            return result;
        }

        private static List<Jpeg2000EncodedBlock> BuildHtCodeBlocksForResolution(int[] coefficients, int width, int height, int precision, int resolution, bool usesMultipleComponentTransform, int decompositionLevels)
        {
            var blocks = new List<Jpeg2000EncodedBlock>();
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, decompositionLevels, precision, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            foreach (var precinct in component.GetPrecincts(resolution))
            {
                foreach (var band in component.GetBands(resolution, precinct))
                {
                    foreach (var block in band.CodeBlocks)
                    {
                        var blockCoefficients = new int[block.Width * block.Height];
                        for (var y = 0; y < block.Height; y++)
                        {
                            Array.Copy(coefficients, ((block.Y0 + y) * width) + block.X0, blockCoefficients, y * block.Width, block.Width);
                        }

                        var kMax = Jpeg2000ReversibleBiboGains.BandKmax(
                            precision,
                            usesMultipleComponentTransform,
                            decompositionLevels,
                            resolution,
                            block.Orientation);
                        var missingMostSignificantBits = kMax - 1;
                        var data = HasNonZero(blockCoefficients)
                            ? Jpeg2000HtCodeBlockEncoder.EncodeStandardCleanupPass(
                                new Jpeg2000ClassicCodeBlock(block.Width, block.Height, ScaleForOpenJphReversibleCodeBlock(blockCoefficients, kMax)),
                                missingMostSignificantBits)
                            : Array.Empty<byte>();
                        blocks.Add(new Jpeg2000EncodedBlock(
                            block.LocalX,
                            block.LocalY,
                            band.CodeBlockCountX,
                            band.CodeBlockCountY,
                            data.Length == 0 ? 0 : missingMostSignificantBits,
                            data.Length == 0 ? 0 : 1,
                            data,
                            data.Length == 0 ? Array.Empty<int>() : new[] { data.Length },
                            data.Length == 0 ? Array.Empty<byte[]>() : new[] { data },
                            resolution,
                            block.Orientation,
                            precinct));
                    }
                }
            }

            return blocks;
        }

        private static List<List<Jpeg2000EncodedBlock>> BuildHtIrreversibleCodeBlocksByResolution(double[] coefficients, int width, int height, int precision, int decompositionLevels, ushort[] encodedSteps)
        {
            var result = new List<List<Jpeg2000EncodedBlock>>();
            for (var resolution = 0; resolution <= decompositionLevels; resolution++)
            {
                result.Add(BuildHtIrreversibleCodeBlocksForResolution(coefficients, width, height, precision, resolution, decompositionLevels, encodedSteps));
            }

            return result;
        }

        private static List<Jpeg2000EncodedBlock> BuildHtIrreversibleCodeBlocksForResolution(double[] coefficients, int width, int height, int precision, int resolution, int decompositionLevels, ushort[] encodedSteps)
        {
            var blocks = new List<Jpeg2000EncodedBlock>();
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, decompositionLevels, precision, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            foreach (var precinct in component.GetPrecincts(resolution))
            {
                foreach (var band in component.GetBands(resolution, precinct))
                {
                    var stepIndex = Jpeg2000QuantizationTable.SubbandIndex(resolution, band.Orientation);
                    var encodedStep = stepIndex >= 0 && stepIndex < encodedSteps.Length ? encodedSteps[stepIndex] : encodedSteps[encodedSteps.Length - 1];
                    var kMax = Jpeg2000HtIrreversibleQuantization.Kmax(encodedStep, guardBits: 1);
                    var delta = Jpeg2000HtIrreversibleQuantization.DecodeDelta(encodedStep, band.Orientation, kMax);
                    foreach (var block in band.CodeBlocks)
                    {
                        var blockCoefficients = new double[block.Width * block.Height];
                        for (var y = 0; y < block.Height; y++)
                        {
                            Array.Copy(coefficients, ((block.Y0 + y) * width) + block.X0, blockCoefficients, y * block.Width, block.Width);
                        }

                        var scaled = Jpeg2000HtIrreversibleQuantization.ToSignMagnitude(blockCoefficients, delta);
                        var missingMostSignificantBits = Math.Max(0, kMax - 1);
                        var data = HasNonZeroSignMagnitude(scaled)
                            ? Jpeg2000HtCodeBlockEncoder.EncodeStandardCleanupPass(
                                new Jpeg2000ClassicCodeBlock(block.Width, block.Height, scaled),
                                missingMostSignificantBits)
                            : Array.Empty<byte>();
                        blocks.Add(new Jpeg2000EncodedBlock(
                            block.LocalX,
                            block.LocalY,
                            band.CodeBlockCountX,
                            band.CodeBlockCountY,
                            data.Length == 0 ? 0 : missingMostSignificantBits,
                            data.Length == 0 ? 0 : 1,
                            data,
                            data.Length == 0 ? Array.Empty<int>() : new[] { data.Length },
                            data.Length == 0 ? Array.Empty<byte[]>() : new[] { data },
                            resolution,
                            block.Orientation,
                            precinct));
                    }
                }
            }

            return blocks;
        }

        private static int[] ScaleForOpenJphReversibleCodeBlock(int[] coefficients, int kMax)
        {
            var shift = 31 - kMax;
            var scaled = new int[coefficients.Length];
            for (var i = 0; i < coefficients.Length; i++)
            {
                var value = coefficients[i];
                var sign = value < 0 ? unchecked((int)0x80000000) : 0;
                var magnitude = Math.Abs(value) << shift;
                scaled[i] = sign | magnitude;
            }

            return scaled;
        }

        private static bool HasNonZero(int[] values)
        {
            foreach (var value in values)
            {
                if (value != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNonZeroSignMagnitude(int[] values)
        {
            foreach (var value in values)
            {
                if ((value & 0x7FFFFFFF) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GainBits(int orientation)
        {
            return orientation == 3 ? 2 : orientation == 0 ? 0 : 1;
        }

        private static int[][] ReadComponentSamples(byte[] frame, int samplesPerPixel, int bitsAllocated, int bitsStored, bool isSigned)
        {
            var bytesPerSample = bitsAllocated / 8;
            var sampleCount = frame.Length / bytesPerSample;
            var pixelCount = sampleCount / samplesPerPixel;
            var components = new int[samplesPerPixel][];
            for (var component = 0; component < components.Length; component++)
            {
                components[component] = new int[pixelCount];
            }

            var signBit = 1 << (bitsStored - 1);
            var mask = (1 << bitsStored) - 1;
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < samplesPerPixel; component++)
                {
                    var sample = (pixel * samplesPerPixel) + component;
                    var offset = sample * bytesPerSample;
                    var value = bytesPerSample == 1 ? frame[offset] : frame[offset] | (frame[offset + 1] << 8);
                    value &= mask;
                    if (isSigned && (value & signBit) != 0)
                    {
                        value -= 1 << bitsStored;
                    }

                    components[component][pixel] = value;
                }
            }

            return components;
        }

        private static void ApplyForwardLevelShift(int[][] components, int precision, bool isSigned)
        {
            if (isSigned)
            {
                return;
            }

            var shift = 1 << (precision - 1);
            foreach (var component in components)
            {
                for (var i = 0; i < component.Length; i++)
                {
                    component[i] -= shift;
                }
            }
        }

        private static void ApplyForwardRct(int[][] components)
        {
            for (var i = 0; i < components[0].Length; i++)
            {
                var r = components[0][i];
                var g = components[1][i];
                var b = components[2][i];
                var y = (r + (g * 2) + b) >> 2;
                var db = b - g;
                var dr = r - g;
                components[0][i] = y;
                components[1][i] = db;
                components[2][i] = dr;
            }
        }

        private static double[][] CreateDoubleComponents(int[][] components)
        {
            var values = new double[components.Length][];
            for (var component = 0; component < components.Length; component++)
            {
                values[component] = new double[components[component].Length];
                for (var i = 0; i < components[component].Length; i++)
                {
                    values[component][i] = components[component][i];
                }
            }

            return values;
        }

        private static void ApplyForwardIct(double[][] components)
        {
            for (var i = 0; i < components[0].Length; i++)
            {
                var r = (float)components[0][i];
                var g = (float)components[1][i];
                var b = (float)components[2][i];
                components[0][i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                components[1][i] = (-0.16875f * r) - (0.331260f * g) + (0.5f * b);
                components[2][i] = (0.5f * r) - (0.418690f * g) - (0.081310f * b);
            }
        }

        private static void ScaleComponents(double[][] components, double scale)
        {
            foreach (var component in components)
            {
                for (var i = 0; i < component.Length; i++)
                {
                    component[i] *= scale;
                }
            }
        }

        private sealed class HtPacketBand
        {
            private readonly Jpeg2000EncodedBlock?[] _blocks;

            public HtPacketBand(int gridWidth, int gridHeight)
            {
                GridWidth = Math.Max(1, gridWidth);
                GridHeight = Math.Max(1, gridHeight);
                _blocks = new Jpeg2000EncodedBlock?[GridWidth * GridHeight];
                Blocks = new List<Jpeg2000EncodedBlock>();
            }

            public int GridWidth { get; }

            public int GridHeight { get; }

            public List<Jpeg2000EncodedBlock> Blocks { get; }

            public int Count => Blocks.Count;

            public void Add(Jpeg2000EncodedBlock block)
            {
                Blocks.Add(block);
                if (block.X >= 0 && block.X < GridWidth && block.Y >= 0 && block.Y < GridHeight)
                {
                    _blocks[block.X + (block.Y * GridWidth)] = block;
                }
            }

            public Jpeg2000EncodedBlock? GetBlock(int x, int y)
            {
                return x >= 0 && x < GridWidth && y >= 0 && y < GridHeight
                    ? _blocks[x + (y * GridWidth)]
                    : null;
            }
        }

        private sealed class HtPacketTree
        {
            private readonly int[] _widths;
            private readonly int[] _heights;
            private readonly int[][] _values;

            public HtPacketTree(int width, int height, int initialValue)
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                Levels = 1 + Math.Max(Log2Ceil(width), Log2Ceil(height));
                _widths = new int[Levels + 1];
                _heights = new int[Levels + 1];
                _values = new int[Levels + 1][];
                for (var level = 0; level <= Levels; level++)
                {
                    _widths[level] = Math.Max(1, (width + (1 << level) - 1) >> level);
                    _heights[level] = Math.Max(1, (height + (1 << level) - 1) >> level);
                    var capacity = level == Levels
                        ? 1
                        : 1 << ((Levels - level - 1) << 1);
                    _values[level] = new int[capacity];
                    for (var i = 0; i < _values[level].Length; i++)
                    {
                        _values[level][i] = initialValue;
                    }
                }

                _values[Levels][0] = 0;
            }

            public int Levels { get; }

            public bool IsEmpty => Get(0, 0, Levels - 1) != 0;

            public int Get(int x, int y, int level)
            {
                if (level < 0 || level >= _values.Length)
                {
                    return 0;
                }

                return _values[level][x + (y * _widths[level])];
            }

            public void Set(int x, int y, int level, int value)
            {
                if (level < 0 || level >= _values.Length)
                {
                    return;
                }

                _values[level][x + (y * _widths[level])] = value;
            }

            public static HtPacketTree FromInclusion(HtPacketBand band)
            {
                var tree = new HtPacketTree(band.GridWidth, band.GridHeight, 1);
                foreach (var block in band.Blocks)
                {
                    tree.Set(block.X, block.Y, 0, block.PassCount > 0 && block.Data.Length > 0 ? 0 : 1);
                }

                tree.PropagateMin();
                return tree;
            }

            public static HtPacketTree FromMissingMostSignificantBits(HtPacketBand band)
            {
                var tree = new HtPacketTree(band.GridWidth, band.GridHeight, 0);
                foreach (var block in band.Blocks)
                {
                    tree.Set(block.X, block.Y, 0, block.ZeroBitPlanes);
                }

                tree.PropagateMin();
                return tree;
            }

            private void PropagateMin()
            {
                for (var level = 1; level < Levels; level++)
                {
                    for (var y = 0; y < _heights[level]; y++)
                    {
                        for (var x = 0; x < _widths[level]; x++)
                        {
                            var a = Get(x << 1, y << 1, level - 1);
                            var b = Get((x << 1) + 1, y << 1, level - 1);
                            var c = Get(x << 1, (y << 1) + 1, level - 1);
                            var d = Get((x << 1) + 1, (y << 1) + 1, level - 1);
                            Set(x, y, level, Math.Min(Math.Min(a, b), Math.Min(c, d)));
                        }
                    }
                }

                Set(0, 0, Levels, 0);
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
}
