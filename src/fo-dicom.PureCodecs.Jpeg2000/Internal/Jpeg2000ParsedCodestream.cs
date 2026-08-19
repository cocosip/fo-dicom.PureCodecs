using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000ParsedCodestream
    {
        public Jpeg2000ParsedCodestream(Jpeg2000SizeSegment size, IReadOnlyList<Jpeg2000ParsedTilePart> tiles)
        {
            Size = size;
            Tiles = tiles;
        }

        public Jpeg2000SizeSegment Size { get; }

        public IReadOnlyList<Jpeg2000ParsedTilePart> Tiles { get; }
    }

    internal sealed class Jpeg2000ParsedTilePart
    {
        public Jpeg2000ParsedTilePart(
            Jpeg2000SizeSegment size,
            Jpeg2000CodingStyleDefault codingStyle,
            Jpeg2000QuantizationDefault quantization,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<int> regionOfInterestShifts,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[]? packedPacketHeaders,
            Jpeg2000StartOfTilePart startOfTile,
            byte[] tileData)
        {
            Size = size;
            CodingStyle = codingStyle;
            Quantization = quantization;
            ComponentCodingStyles = componentCodingStyles;
            ComponentQuantizations = componentQuantizations;
            RegionOfInterestShifts = regionOfInterestShifts;
            ProgressionChanges = progressionChanges;
            PackedPacketHeaders = packedPacketHeaders;
            StartOfTile = startOfTile;
            TileData = tileData;
        }

        public Jpeg2000SizeSegment Size { get; }

        public Jpeg2000CodingStyleDefault CodingStyle { get; }

        public Jpeg2000QuantizationDefault Quantization { get; }

        public IReadOnlyList<Jpeg2000ResolvedCodingStyle> ComponentCodingStyles { get; }

        public IReadOnlyList<Jpeg2000ResolvedQuantization> ComponentQuantizations { get; }

        public IReadOnlyList<int> RegionOfInterestShifts { get; }

        public IReadOnlyList<Jpeg2000ProgressionOrderChange> ProgressionChanges { get; }

        public byte[]? PackedPacketHeaders { get; }

        public Jpeg2000StartOfTilePart StartOfTile { get; }

        public byte[] TileData { get; }
    }

    internal static class Jpeg2000CodestreamParser
    {
        public static Jpeg2000ParsedCodestream ParseTiles(
            byte[] codestream,
            string sodFamilyName,
            string codestreamName)
        {
            Jpeg2000CodestreamReader.EnsureRawCodestream(codestream);
            var reader = new Jpeg2000CodestreamReader(codestream);
            Jpeg2000SizeSegment? siz = null;
            Jpeg2000CodingStyleDefault? mainCod = null;
            Jpeg2000QuantizationDefault? mainQcd = null;
            Jpeg2000StartOfTilePart? sot = null;
            var tilePartStartOffset = -1;
            var tileParts = new Dictionary<int, List<TilePartData>>();
            var tileCodingStyles = new Dictionary<int, Jpeg2000CodingStyleDefault>();
            var tileQuantizations = new Dictionary<int, Jpeg2000QuantizationDefault>();
            var mainComponentCodingStyles = new Dictionary<int, Jpeg2000CodingStyleComponent>();
            var mainComponentQuantizations = new Dictionary<int, Jpeg2000QuantizationComponent>();
            var tileComponentCodingStyles = new Dictionary<int, Dictionary<int, Jpeg2000CodingStyleComponent>>();
            var tileComponentQuantizations = new Dictionary<int, Dictionary<int, Jpeg2000QuantizationComponent>>();
            var mainProgressionChanges = new List<Jpeg2000ProgressionOrderChange>();
            var tileProgressionChanges = new Dictionary<int, List<Jpeg2000ProgressionOrderChange>>();
            var mainRegionOfInterestShifts = new Dictionary<int, int>();
            var tileRegionOfInterestShifts = new Dictionary<int, Dictionary<int, int>>();
            var ppmSegments = new List<Jpeg2000MarkerSegment>();
            var currentPptSegments = new List<Jpeg2000MarkerSegment>();
            var tilePartSequence = 0;
            var reachedTilePart = false;
            var reachedEndOfCodestream = false;

            while (!reader.EndOfData && !reachedEndOfCodestream)
            {
                var segment = reader.ReadNext();
                switch (segment.Code)
                {
                    case Jpeg2000Marker.SOC:
                        break;
                    case Jpeg2000Marker.SIZ:
                        if (sot != null)
                        {
                            throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ marker is not valid in a tile-part header.");
                        }

                        siz = Jpeg2000SizeSegment.Parse(segment);
                        break;
                    case Jpeg2000Marker.COD:
                        var parsedCod = Jpeg2000CodingStyleDefault.Parse(segment);
                        if (sot == null)
                        {
                            mainCod = parsedCod;
                        }
                        else
                        {
                            tileCodingStyles[sot.TileIndex] = parsedCod;
                        }

                        break;
                    case Jpeg2000Marker.COC:
                        if (siz == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " COC marker was found before SIZ.");
                        }

                        var parsedCoc = Jpeg2000CodingStyleComponent.Parse(segment, siz.Components.Count);
                        AddComponentOverride(mainComponentCodingStyles, tileComponentCodingStyles, sot, parsedCoc.ComponentIndex, parsedCoc);
                        break;
                    case Jpeg2000Marker.QCD:
                        var parsedQcd = Jpeg2000QuantizationDefault.Parse(segment);
                        if (sot == null)
                        {
                            mainQcd = parsedQcd;
                        }
                        else
                        {
                            tileQuantizations[sot.TileIndex] = parsedQcd;
                        }

                        break;
                    case Jpeg2000Marker.QCC:
                        if (siz == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " QCC marker was found before SIZ.");
                        }

                        var parsedQcc = Jpeg2000QuantizationComponent.Parse(segment, siz.Components.Count);
                        AddComponentOverride(mainComponentQuantizations, tileComponentQuantizations, sot, parsedQcc.ComponentIndex, parsedQcc);
                        break;
                    case Jpeg2000Marker.POC:
                        if (siz == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " POC marker was found before SIZ.");
                        }

                        var parsedPoc = Jpeg2000ProgressionOrderChange.Parse(segment, siz.Components.Count);
                        if (sot == null)
                        {
                            mainProgressionChanges.AddRange(parsedPoc);
                        }
                        else
                        {
                            if (!tileProgressionChanges.TryGetValue(sot.TileIndex, out var tilePoc))
                            {
                                tilePoc = new List<Jpeg2000ProgressionOrderChange>();
                                tileProgressionChanges.Add(sot.TileIndex, tilePoc);
                            }

                            tilePoc.AddRange(parsedPoc);
                        }

                        break;
                    case Jpeg2000Marker.RGN:
                        if (siz == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " RGN marker was found before SIZ.");
                        }

                        var parsedRgn = Jpeg2000RegionOfInterest.Parse(segment, siz.Components.Count);
                        AddComponentOverride(
                            mainRegionOfInterestShifts,
                            tileRegionOfInterestShifts,
                            sot,
                            parsedRgn.ComponentIndex,
                            parsedRgn.Shift);
                        break;
                    case Jpeg2000Marker.PPM:
                        if (sot != null || reachedTilePart)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " PPM marker is only valid in the main header.");
                        }

                        ppmSegments.Add(segment);
                        break;
                    case Jpeg2000Marker.PPT:
                        if (sot == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " PPT marker is only valid in a tile-part header.");
                        }

                        if (ppmSegments.Count != 0)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " codestream cannot contain both PPM and PPT packet headers.");
                        }

                        currentPptSegments.Add(segment);
                        break;
                    case Jpeg2000Marker.SOT:
                        if (siz == null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " SOT marker was found before SIZ.");
                        }

                        if (sot != null)
                        {
                            throw Jpeg2000Binary.CreateException(codestreamName + " SOT marker was found before the preceding SOD.");
                        }

                        var tileCount = Jpeg2000ImageModel.FromSizeSegment(siz).Tiles.Count;
                        sot = Jpeg2000StartOfTilePart.Parse(segment, tileCount);
                        reachedTilePart = true;
                        tilePartStartOffset = reader.LastMarkerOffset;
                        break;
                    case Jpeg2000Marker.SOD:
                        if (sot == null)
                        {
                            throw Jpeg2000Binary.CreateException(sodFamilyName + " SOD marker was found before SOT.");
                        }

                        if (!tileParts.TryGetValue(sot.TileIndex, out var parts))
                        {
                            parts = new List<TilePartData>();
                            tileParts.Add(sot.TileIndex, parts);
                        }

                        var pptHeaders = currentPptSegments.Count == 0
                            ? null
                            : Jpeg2000PackedPacketHeaderParser.ParsePpt(currentPptSegments);
                        parts.Add(new TilePartData(
                            sot,
                            reader.ReadTileData(sot, tilePartStartOffset),
                            pptHeaders,
                            tilePartSequence++));
                        currentPptSegments.Clear();
                        sot = null;
                        tilePartStartOffset = -1;
                        break;
                    case Jpeg2000Marker.EOC:
                        reachedEndOfCodestream = true;
                        break;
                }
            }

            if (!reachedEndOfCodestream)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing EOC.");
            }

            if (siz == null || mainCod == null || mainQcd == null || tileParts.Count == 0)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing required marker data.");
            }

            var image = Jpeg2000ImageModel.FromSizeSegment(siz);
            var ppmHeaders = ppmSegments.Count == 0
                ? null
                : Jpeg2000PackedPacketHeaderParser.ParsePpm(ppmSegments);
            if (ppmHeaders != null && ppmHeaders.Count != tilePartSequence)
            {
                throw Jpeg2000Binary.CreateException(
                    codestreamName + " PPM packet-header chunk count does not match the tile-part count.");
            }

            var tiles = new List<Jpeg2000ParsedTilePart>(image.Tiles.Count);
            for (var tileIndex = 0; tileIndex < image.Tiles.Count; tileIndex++)
            {
                if (!tileParts.TryGetValue(tileIndex, out var parts))
                {
                    throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing tile " + tileIndex + ".");
                }

                parts.Sort((left, right) => left.StartOfTile.TilePartIndex.CompareTo(right.StartOfTile.TilePartIndex));
                ValidateTileParts(parts, tileIndex);
                var data = new List<byte[]>();
                var packedHeaders = new List<byte[]>();
                foreach (var part in parts)
                {
                    data.Add(part.Data);
                    if (ppmHeaders != null)
                    {
                        packedHeaders.Add(ppmHeaders[part.SequenceIndex]);
                    }
                    else if (part.PackedPacketHeaders != null)
                    {
                        packedHeaders.Add(part.PackedPacketHeaders);
                    }
                }

                var hasTileCod = tileCodingStyles.TryGetValue(tileIndex, out var cod);
                var hasTileQcd = tileQuantizations.TryGetValue(tileIndex, out var qcd);
                var effectiveCod = hasTileCod ? cod : mainCod;
                var effectiveQcd = hasTileQcd ? qcd : mainQcd;
                tiles.Add(new Jpeg2000ParsedTilePart(
                    siz,
                    effectiveCod,
                    effectiveQcd,
                    ResolveCodingStyles(
                        siz.Components.Count,
                        mainCod,
                        mainComponentCodingStyles,
                        hasTileCod ? cod : null,
                        GetTileOverrides(tileComponentCodingStyles, tileIndex)),
                    ResolveQuantizations(
                        siz.Components.Count,
                        mainQcd,
                        mainComponentQuantizations,
                        hasTileQcd ? qcd : null,
                        GetTileOverrides(tileComponentQuantizations, tileIndex)),
                    ResolveRegionOfInterestShifts(
                        siz.Components.Count,
                        mainRegionOfInterestShifts,
                        GetTileOverrides(tileRegionOfInterestShifts, tileIndex)),
                    ResolveProgressionChanges(mainProgressionChanges, tileProgressionChanges, tileIndex),
                    ppmHeaders != null || packedHeaders.Count != 0 ? Concat(packedHeaders) : null,
                    parts[0].StartOfTile,
                    Concat(data)));
            }

            return new Jpeg2000ParsedCodestream(siz, tiles);
        }

        public static Jpeg2000ParsedTilePart ParseSingleTilePart(
            byte[] codestream,
            string sodFamilyName,
            string codestreamName)
        {
            var parsed = ParseTiles(codestream, sodFamilyName, codestreamName);
            if (parsed.Tiles.Count != 1)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream must contain exactly one tile.");
            }

            return parsed.Tiles[0];
        }

        private static void AddComponentOverride<T>(
            Dictionary<int, T> mainOverrides,
            Dictionary<int, Dictionary<int, T>> tileOverrides,
            Jpeg2000StartOfTilePart? sot,
            int componentIndex,
            T value)
        {
            if (sot == null)
            {
                mainOverrides[componentIndex] = value;
                return;
            }

            if (!tileOverrides.TryGetValue(sot.TileIndex, out var components))
            {
                components = new Dictionary<int, T>();
                tileOverrides.Add(sot.TileIndex, components);
            }

            components[componentIndex] = value;
        }

        private static IReadOnlyDictionary<int, T> GetTileOverrides<T>(
            Dictionary<int, Dictionary<int, T>> overrides,
            int tileIndex)
        {
            return overrides.TryGetValue(tileIndex, out var components)
                ? components
                : new Dictionary<int, T>();
        }

        private static Jpeg2000ResolvedCodingStyle[] ResolveCodingStyles(
            int componentCount,
            Jpeg2000CodingStyleDefault mainDefault,
            IReadOnlyDictionary<int, Jpeg2000CodingStyleComponent> mainOverrides,
            Jpeg2000CodingStyleDefault? tileDefault,
            IReadOnlyDictionary<int, Jpeg2000CodingStyleComponent> tileOverrides)
        {
            var resolved = new Jpeg2000ResolvedCodingStyle[componentCount];
            for (var component = 0; component < componentCount; component++)
            {
                if (tileOverrides.TryGetValue(component, out var tileOverride))
                {
                    resolved[component] = tileOverride.InheritFrom(tileDefault ?? mainDefault);
                }
                else if (tileDefault != null)
                {
                    resolved[component] = Jpeg2000ResolvedCodingStyle.FromDefault(component, tileDefault);
                }
                else if (mainOverrides.TryGetValue(component, out var mainOverride))
                {
                    resolved[component] = mainOverride.InheritFrom(mainDefault);
                }
                else
                {
                    resolved[component] = Jpeg2000ResolvedCodingStyle.FromDefault(component, mainDefault);
                }
            }

            return resolved;
        }

        private static Jpeg2000ResolvedQuantization[] ResolveQuantizations(
            int componentCount,
            Jpeg2000QuantizationDefault mainDefault,
            IReadOnlyDictionary<int, Jpeg2000QuantizationComponent> mainOverrides,
            Jpeg2000QuantizationDefault? tileDefault,
            IReadOnlyDictionary<int, Jpeg2000QuantizationComponent> tileOverrides)
        {
            var resolved = new Jpeg2000ResolvedQuantization[componentCount];
            for (var component = 0; component < componentCount; component++)
            {
                if (tileOverrides.TryGetValue(component, out var tileOverride))
                {
                    resolved[component] = tileOverride.InheritFrom(tileDefault ?? mainDefault);
                }
                else if (tileDefault != null)
                {
                    resolved[component] = Jpeg2000ResolvedQuantization.FromDefault(component, tileDefault);
                }
                else if (mainOverrides.TryGetValue(component, out var mainOverride))
                {
                    resolved[component] = mainOverride.InheritFrom(mainDefault);
                }
                else
                {
                    resolved[component] = Jpeg2000ResolvedQuantization.FromDefault(component, mainDefault);
                }
            }

            return resolved;
        }

        private static IReadOnlyList<Jpeg2000ProgressionOrderChange> ResolveProgressionChanges(
            IReadOnlyList<Jpeg2000ProgressionOrderChange> mainChanges,
            IReadOnlyDictionary<int, List<Jpeg2000ProgressionOrderChange>> tileChanges,
            int tileIndex)
        {
            if (!tileChanges.TryGetValue(tileIndex, out var tile) || tile.Count == 0)
            {
                return mainChanges;
            }

            var resolved = new List<Jpeg2000ProgressionOrderChange>(mainChanges.Count + tile.Count);
            resolved.AddRange(mainChanges);
            resolved.AddRange(tile);
            return resolved;
        }

        private static int[] ResolveRegionOfInterestShifts(
            int componentCount,
            IReadOnlyDictionary<int, int> mainShifts,
            IReadOnlyDictionary<int, int> tileShifts)
        {
            var resolved = new int[componentCount];
            for (var component = 0; component < componentCount; component++)
            {
                if (tileShifts.TryGetValue(component, out var tileShift))
                {
                    resolved[component] = tileShift;
                }
                else if (mainShifts.TryGetValue(component, out var mainShift))
                {
                    resolved[component] = mainShift;
                }
            }

            return resolved;
        }

        private static byte[] Concat(System.Collections.Generic.IReadOnlyList<byte[]> parts)
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
                System.Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static void ValidateTileParts(IReadOnlyList<TilePartData> parts, int tileIndex)
        {
            var declaredCount = parts[0].StartOfTile.TilePartCount;
            for (var index = 0; index < parts.Count; index++)
            {
                var start = parts[index].StartOfTile;
                if (start.TilePartIndex != index)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 tile " + tileIndex + " has missing or duplicate tile-part indexes.");
                }

                if (declaredCount != 0 && start.TilePartCount != declaredCount)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 tile " + tileIndex + " has inconsistent tile-part counts.");
                }
            }

            if (declaredCount != 0 && parts.Count != declaredCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 tile " + tileIndex + " is missing declared tile parts.");
            }
        }

        private sealed class TilePartData
        {
            public TilePartData(
                Jpeg2000StartOfTilePart startOfTile,
                byte[] data,
                byte[]? packedPacketHeaders,
                int sequenceIndex)
            {
                StartOfTile = startOfTile;
                Data = data;
                PackedPacketHeaders = packedPacketHeaders;
                SequenceIndex = sequenceIndex;
            }

            public Jpeg2000StartOfTilePart StartOfTile { get; }

            public byte[] Data { get; }

            public byte[]? PackedPacketHeaders { get; }

            public int SequenceIndex { get; }
        }
    }
}
