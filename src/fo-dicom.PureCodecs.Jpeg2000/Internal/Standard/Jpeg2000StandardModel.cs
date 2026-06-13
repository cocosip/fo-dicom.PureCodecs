using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardComponent
    {
        private readonly Dictionary<int, Dictionary<int, List<PrecinctBand>>> _bands =
            new Dictionary<int, Dictionary<int, List<PrecinctBand>>>();

        public Jpeg2000StandardComponent(int index, int x0, int y0, int width, int height, int levels, int precision, bool isSigned)
        {
            Index = index;
            X0 = x0;
            Y0 = y0;
            Width = width;
            Height = height;
            Levels = levels;
            Precision = precision;
            IsSigned = isSigned;
            Coefficients = new int[Math.Max(0, width * height)];
            Samples = Array.Empty<int>();
        }

        public int Index { get; }

        public int X0 { get; }

        public int Y0 { get; }

        public int Width { get; }

        public int Height { get; }

        public int Levels { get; }

        public int Precision { get; }

        public bool IsSigned { get; }

        public int[] Coefficients { get; }

        public int[] Samples { get; set; }

        public IEnumerable<int> GetPrecincts(int resolution)
        {
            if (!_bands.TryGetValue(resolution, out var precincts))
            {
                yield break;
            }

            var keys = new List<int>(precincts.Keys);
            keys.Sort();
            foreach (var key in keys)
            {
                yield return key;
            }
        }

        public IReadOnlyList<PrecinctBand> GetBands(int resolution, int precinct)
        {
            if (_bands.TryGetValue(resolution, out var precincts)
                && precincts.TryGetValue(precinct, out var bands))
            {
                return bands;
            }

            return Array.Empty<PrecinctBand>();
        }

        public IReadOnlyList<Jpeg2000StandardCodeBlock> AllCodeBlocks()
        {
            var blocks = new List<Jpeg2000StandardCodeBlock>();
            for (var resolution = 0; resolution <= Levels; resolution++)
            {
                if (!_bands.TryGetValue(resolution, out var precincts))
                {
                    continue;
                }

                foreach (var entry in precincts)
                {
                    foreach (var band in entry.Value)
                    {
                        blocks.AddRange(band.CodeBlocks);
                    }
                }
            }

            return blocks;
        }

        public void BuildCodeBlocks(int codeBlockWidth, int codeBlockHeight)
        {
            if (_bands.Count != 0)
            {
                return;
            }

            var globalIndex = 0;
            for (var resolution = 0; resolution <= Levels; resolution++)
            {
                var subbands = Jpeg2000StandardGeometry.GetSubbands(Width, Height, X0, Y0, Levels, resolution);
                var precinctMap = new Dictionary<int, List<PrecinctBand>>();
                _bands.Add(resolution, precinctMap);

                foreach (var subband in subbands)
                {
                    if (subband.Width == 0 || subband.Height == 0)
                    {
                        continue;
                    }

                    var precinctIndex = 0;
                    var codeBlockWidthExponent = Jpeg2000StandardGeometry.FloorLog2(codeBlockWidth);
                    var codeBlockHeightExponent = Jpeg2000StandardGeometry.FloorLog2(codeBlockHeight);
                    var blockXStart = Jpeg2000StandardGeometry.FloorDivPow2(subband.BandX0, codeBlockWidthExponent) << codeBlockWidthExponent;
                    var blockYStart = Jpeg2000StandardGeometry.FloorDivPow2(subband.BandY0, codeBlockHeightExponent) << codeBlockHeightExponent;
                    var blockXEnd = Jpeg2000StandardGeometry.CeilDivPow2(subband.BandX1, codeBlockWidthExponent) << codeBlockWidthExponent;
                    var blockYEnd = Jpeg2000StandardGeometry.CeilDivPow2(subband.BandY1, codeBlockHeightExponent) << codeBlockHeightExponent;
                    var blockCountX = (blockXEnd - blockXStart) >> codeBlockWidthExponent;
                    var blockCountY = (blockYEnd - blockYStart) >> codeBlockHeightExponent;
                    var band = new PrecinctBand(subband.Orientation, blockCountX, blockCountY);
                    if (!precinctMap.TryGetValue(precinctIndex, out var bands))
                    {
                        bands = new List<PrecinctBand>();
                        precinctMap.Add(precinctIndex, bands);
                    }

                    bands.Add(band);

                    for (var by = 0; by < blockCountY; by++)
                    {
                        for (var bx = 0; bx < blockCountX; bx++)
                        {
                            var codeBlockX0 = Math.Max(blockXStart + (bx * codeBlockWidth), subband.BandX0);
                            var codeBlockY0 = Math.Max(blockYStart + (by * codeBlockHeight), subband.BandY0);
                            var codeBlockX1 = Math.Min(codeBlockX0 - (codeBlockX0 - (blockXStart + (bx * codeBlockWidth))) + codeBlockWidth, subband.BandX1);
                            var codeBlockY1 = Math.Min(codeBlockY0 - (codeBlockY0 - (blockYStart + (by * codeBlockHeight))) + codeBlockHeight, subband.BandY1);
                            var coefficientX0 = subband.OffsetX + codeBlockX0 - subband.BandX0;
                            var coefficientY0 = subband.OffsetY + codeBlockY0 - subband.BandY0;
                            band.CodeBlocks.Add(new Jpeg2000StandardCodeBlock(
                                this,
                                globalIndex++,
                                subband.Orientation,
                                bx,
                                by,
                                coefficientX0,
                                coefficientY0,
                                codeBlockX1 - codeBlockX0,
                                codeBlockY1 - codeBlockY0));
                        }
                    }
                }
            }
        }
    }

    internal sealed class PrecinctBand
    {
        public PrecinctBand(int orientation, int codeBlockCountX, int codeBlockCountY)
        {
            Orientation = orientation;
            CodeBlockCountX = Math.Max(1, codeBlockCountX);
            CodeBlockCountY = Math.Max(1, codeBlockCountY);
            CodeBlocks = new List<Jpeg2000StandardCodeBlock>();
        }

        public int Orientation { get; }

        public int CodeBlockCountX { get; }

        public int CodeBlockCountY { get; }

        public List<Jpeg2000StandardCodeBlock> CodeBlocks { get; }
    }

    internal sealed class Jpeg2000StandardCodeBlock
    {
        private readonly List<byte> _data = new List<byte>();
        private readonly List<Jpeg2000StandardCodeBlockSegment> _segments = new List<Jpeg2000StandardCodeBlockSegment>();

        public Jpeg2000StandardCodeBlock(
            Jpeg2000StandardComponent component,
            int index,
            int orientation,
            int localX,
            int localY,
            int x0,
            int y0,
            int width,
            int height)
        {
            Component = component;
            Index = index;
            Orientation = orientation;
            LocalX = localX;
            LocalY = localY;
            X0 = x0;
            Y0 = y0;
            Width = width;
            Height = height;
        }

        public Jpeg2000StandardComponent Component { get; }

        public int Index { get; }

        public int Orientation { get; }

        public int LocalX { get; }

        public int LocalY { get; }

        public int X0 { get; }

        public int Y0 { get; }

        public int Width { get; }

        public int Height { get; }

        public int TotalPasses { get; private set; }

        public int ZeroBitPlanes { get; private set; }

        public byte[] Data
        {
            get { return _data.ToArray(); }
        }

        public IReadOnlyList<Jpeg2000StandardCodeBlockSegment> Segments
        {
            get { return _segments; }
        }

        public void AppendData(byte[] bytes, int passCount, int zeroBitPlanes)
        {
            _data.AddRange(bytes);
            TotalPasses += passCount;
            ZeroBitPlanes = zeroBitPlanes;
        }

        public void AppendSegment(byte[] bytes, int passCount, int zeroBitPlanes)
        {
            AppendData(bytes, passCount, zeroBitPlanes);
            _segments.Add(new Jpeg2000StandardCodeBlockSegment(bytes, passCount));
        }
    }

    internal readonly struct Jpeg2000StandardCodeBlockSegment
    {
        public Jpeg2000StandardCodeBlockSegment(byte[] data, int passCount)
        {
            Data = data ?? Array.Empty<byte>();
            PassCount = passCount;
        }

        public byte[] Data { get; }

        public int PassCount { get; }
    }
}
