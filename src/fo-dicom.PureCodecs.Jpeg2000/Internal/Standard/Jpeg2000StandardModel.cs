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

        public double[]? IrreversibleCoefficients { get; set; }

        public int[] Samples { get; set; }

        public double[]? FloatSamples { get; set; }

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
                if (precincts[key].Count != 0)
                {
                    yield return key;
                }
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
            BuildCodeBlocks(codeBlockWidth, codeBlockHeight, codingStyle: null);
        }

        public void BuildCodeBlocks(int codeBlockWidth, int codeBlockHeight, Jpeg2000CodingStyle? codingStyle)
        {
            if (_bands.Count != 0)
            {
                return;
            }

            var globalIndex = 0;
            for (var resolution = 0; resolution <= Levels; resolution++)
            {
                var subbands = Jpeg2000StandardGeometry.GetSubbands(Width, Height, X0, Y0, Levels, resolution);
                var precinctMap = CreatePrecinctMap(codingStyle, resolution, Jpeg2000StandardGeometry.GetResolution(Width, Height, X0, Y0, Levels, resolution), out var precinctInfo);
                _bands.Add(resolution, precinctMap);

                foreach (var subband in subbands)
                {
                    if (subband.Width == 0 || subband.Height == 0)
                    {
                        continue;
                    }

                    var codeBlockWidthExponent = Jpeg2000StandardGeometry.FloorLog2(codeBlockWidth);
                    var codeBlockHeightExponent = Jpeg2000StandardGeometry.FloorLog2(codeBlockHeight);
                    var blockXStart = Jpeg2000StandardGeometry.FloorDivPow2(subband.BandX0, codeBlockWidthExponent) << codeBlockWidthExponent;
                    var blockYStart = Jpeg2000StandardGeometry.FloorDivPow2(subband.BandY0, codeBlockHeightExponent) << codeBlockHeightExponent;
                    var blockXEnd = Jpeg2000StandardGeometry.CeilDivPow2(subband.BandX1, codeBlockWidthExponent) << codeBlockWidthExponent;
                    var blockYEnd = Jpeg2000StandardGeometry.CeilDivPow2(subband.BandY1, codeBlockHeightExponent) << codeBlockHeightExponent;
                    var blockCountX = (blockXEnd - blockXStart) >> codeBlockWidthExponent;
                    var blockCountY = (blockYEnd - blockYStart) >> codeBlockHeightExponent;

                    for (var precinctY = 0; precinctY < precinctInfo.PrecinctCountY; precinctY++)
                    {
                        for (var precinctX = 0; precinctX < precinctInfo.PrecinctCountX; precinctX++)
                        {
                            var precinctIndex = precinctX + (precinctY * precinctInfo.PrecinctCountX);
                            var window = ResolveSubbandPrecinctWindow(
                                subband,
                                resolution,
                                precinctInfo,
                                precinctX,
                                precinctY,
                                codeBlockWidthExponent,
                                codeBlockHeightExponent);
                            if (window.Width == 0 || window.Height == 0)
                            {
                                continue;
                            }

                            var band = new PrecinctBand(subband.Orientation, window.Width, window.Height);
                            precinctMap[precinctIndex].Add(band);
                            for (var localY = 0; localY < window.Height; localY++)
                            {
                                var blockY = window.Y + localY;
                                if (blockY < 0 || blockY >= blockCountY)
                                {
                                    continue;
                                }

                                for (var localX = 0; localX < window.Width; localX++)
                                {
                                    var blockX = window.X + localX;
                                    if (blockX < 0 || blockX >= blockCountX)
                                    {
                                        continue;
                                    }

                                    var nominalX0 = blockXStart + (blockX * codeBlockWidth);
                                    var nominalY0 = blockYStart + (blockY * codeBlockHeight);
                                    var codeBlockX0 = Math.Max(nominalX0, subband.BandX0);
                                    var codeBlockY0 = Math.Max(nominalY0, subband.BandY0);
                                    var codeBlockX1 = Math.Min(nominalX0 + codeBlockWidth, subband.BandX1);
                                    var codeBlockY1 = Math.Min(nominalY0 + codeBlockHeight, subband.BandY1);
                                    var coefficientX0 = subband.OffsetX + codeBlockX0 - subband.BandX0;
                                    var coefficientY0 = subband.OffsetY + codeBlockY0 - subband.BandY0;
                                    band.CodeBlocks.Add(new Jpeg2000StandardCodeBlock(
                                        this,
                                        globalIndex++,
                                        subband.Orientation,
                                        localX,
                                        localY,
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
        }

        private static Dictionary<int, List<PrecinctBand>> CreatePrecinctMap(
            Jpeg2000CodingStyle? codingStyle,
            int resolution,
            ResolutionGeometry resolutionGeometry,
            out PrecinctInfo precinctInfo)
        {
            if (codingStyle == null || !codingStyle.HasPrecinctSizes || resolution >= codingStyle.PrecinctSizes.Count)
            {
                precinctInfo = CreatePrecinctInfo(resolutionGeometry, 15, 15);
            }
            else
            {
                var value = codingStyle.PrecinctSizes[resolution];
                precinctInfo = CreatePrecinctInfo(resolutionGeometry, value & 0x0F, (value >> 4) & 0x0F);
            }

            var map = new Dictionary<int, List<PrecinctBand>>();
            for (var y = 0; y < precinctInfo.PrecinctCountY; y++)
            {
                for (var x = 0; x < precinctInfo.PrecinctCountX; x++)
                {
                    map.Add(x + (y * precinctInfo.PrecinctCountX), new List<PrecinctBand>());
                }
            }

            return map;
        }

        private static PrecinctInfo CreatePrecinctInfo(ResolutionGeometry resolutionGeometry, int precinctWidthExponent, int precinctHeightExponent)
        {
            var x1 = resolutionGeometry.X0 + resolutionGeometry.Width;
            var y1 = resolutionGeometry.Y0 + resolutionGeometry.Height;
            var precinctCountX = Math.Max(1, Jpeg2000StandardGeometry.CeilDivPow2(x1, precinctWidthExponent) - Jpeg2000StandardGeometry.FloorDivPow2(resolutionGeometry.X0, precinctWidthExponent));
            var precinctCountY = Math.Max(1, Jpeg2000StandardGeometry.CeilDivPow2(y1, precinctHeightExponent) - Jpeg2000StandardGeometry.FloorDivPow2(resolutionGeometry.Y0, precinctHeightExponent));
            var precinctLeft = Jpeg2000StandardGeometry.FloorDivPow2(resolutionGeometry.X0, precinctWidthExponent) << precinctWidthExponent;
            var precinctTop = Jpeg2000StandardGeometry.FloorDivPow2(resolutionGeometry.Y0, precinctHeightExponent) << precinctHeightExponent;
            return new PrecinctInfo(precinctWidthExponent, precinctHeightExponent, precinctCountX, precinctCountY, precinctLeft, precinctTop, resolutionGeometry);
        }

        private static CodeBlockWindow ResolveSubbandPrecinctWindow(
            SubbandGeometry subband,
            int resolution,
            PrecinctInfo precinctInfo,
            int precinctX,
            int precinctY,
            int codeBlockWidthExponent,
            int codeBlockHeightExponent)
        {
            var resolutionX0 = precinctInfo.Resolution.X0;
            var resolutionY0 = precinctInfo.Resolution.Y0;
            var resolutionX1 = precinctInfo.Resolution.X0 + precinctInfo.Resolution.Width;
            var resolutionY1 = precinctInfo.Resolution.Y0 + precinctInfo.Resolution.Height;
            var precinctRegionX0 = Math.Max(resolutionX0, precinctInfo.Left + (precinctX << precinctInfo.WidthExponent));
            var precinctRegionX1 = Math.Min(resolutionX1, precinctInfo.Left + ((precinctX + 1) << precinctInfo.WidthExponent));
            var precinctRegionY0 = Math.Max(resolutionY0, precinctInfo.Top + (precinctY << precinctInfo.HeightExponent));
            var precinctRegionY1 = Math.Min(resolutionY1, precinctInfo.Top + ((precinctY + 1) << precinctInfo.HeightExponent));
            var xShift = resolution == 0 ? 0 : 1;
            var yShift = resolution == 0 ? 0 : 1;
            precinctRegionX0 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionX0 - (subband.Orientation & 1), xShift);
            precinctRegionX1 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionX1 - (subband.Orientation & 1), xShift);
            precinctRegionY0 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionY0 - (subband.Orientation >> 1), yShift);
            precinctRegionY1 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionY1 - (subband.Orientation >> 1), yShift);

            var x0 = Jpeg2000StandardGeometry.FloorDivPow2(precinctRegionX0, codeBlockWidthExponent) - Jpeg2000StandardGeometry.FloorDivPow2(subband.BandX0, codeBlockWidthExponent);
            var x1 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionX1, codeBlockWidthExponent) - Jpeg2000StandardGeometry.FloorDivPow2(subband.BandX0, codeBlockWidthExponent);
            var y0 = Jpeg2000StandardGeometry.FloorDivPow2(precinctRegionY0, codeBlockHeightExponent) - Jpeg2000StandardGeometry.FloorDivPow2(subband.BandY0, codeBlockHeightExponent);
            var y1 = Jpeg2000StandardGeometry.CeilDivPow2(precinctRegionY1, codeBlockHeightExponent) - Jpeg2000StandardGeometry.FloorDivPow2(subband.BandY0, codeBlockHeightExponent);
            return new CodeBlockWindow(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        private readonly struct PrecinctInfo
        {
            public PrecinctInfo(int widthExponent, int heightExponent, int precinctCountX, int precinctCountY, int left, int top, ResolutionGeometry resolution)
            {
                WidthExponent = widthExponent;
                HeightExponent = heightExponent;
                PrecinctCountX = precinctCountX;
                PrecinctCountY = precinctCountY;
                Left = left;
                Top = top;
                Resolution = resolution;
            }

            public int WidthExponent { get; }

            public int HeightExponent { get; }

            public int PrecinctCountX { get; }

            public int PrecinctCountY { get; }

            public int Left { get; }

            public int Top { get; }

            public ResolutionGeometry Resolution { get; }
        }

        private readonly struct CodeBlockWindow
        {
            public CodeBlockWindow(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public int X { get; }

            public int Y { get; }

            public int Width { get; }

            public int Height { get; }
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
