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
            Jpeg2000StartOfTilePart startOfTile,
            byte[] tileData)
        {
            Size = size;
            CodingStyle = codingStyle;
            Quantization = quantization;
            StartOfTile = startOfTile;
            TileData = tileData;
        }

        public Jpeg2000SizeSegment Size { get; }

        public Jpeg2000CodingStyleDefault CodingStyle { get; }

        public Jpeg2000QuantizationDefault Quantization { get; }

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

                        parts.Add(new TilePartData(sot, reader.ReadTileData(sot, tilePartStartOffset)));
                        sot = null;
                        tilePartStartOffset = -1;
                        break;
                    case Jpeg2000Marker.EOC:
                        reachedEndOfCodestream = true;
                        break;
                }
            }

            if (siz == null || mainCod == null || mainQcd == null || tileParts.Count == 0)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing required marker data.");
            }

            var image = Jpeg2000ImageModel.FromSizeSegment(siz);
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
                foreach (var part in parts)
                {
                    data.Add(part.Data);
                }

                tiles.Add(new Jpeg2000ParsedTilePart(
                    siz,
                    tileCodingStyles.TryGetValue(tileIndex, out var cod) ? cod : mainCod,
                    tileQuantizations.TryGetValue(tileIndex, out var qcd) ? qcd : mainQcd,
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
            Jpeg2000CodestreamReader.EnsureRawCodestream(codestream);
            var reader = new Jpeg2000CodestreamReader(codestream);
            Jpeg2000SizeSegment? siz = null;
            Jpeg2000CodingStyleDefault? cod = null;
            Jpeg2000QuantizationDefault? qcd = null;
            Jpeg2000StartOfTilePart? sot = null;
            var tileDataParts = new System.Collections.Generic.List<byte[]>();
            var reachedEndOfCodestream = false;

            while (!reader.EndOfData && !reachedEndOfCodestream)
            {
                var segment = reader.ReadNext();
                switch (segment.Code)
                {
                    case Jpeg2000Marker.SOC:
                        break;
                    case Jpeg2000Marker.SIZ:
                        siz = Jpeg2000SizeSegment.Parse(segment);
                        break;
                    case Jpeg2000Marker.COD:
                        cod = Jpeg2000CodingStyleDefault.Parse(segment);
                        break;
                    case Jpeg2000Marker.QCD:
                        qcd = Jpeg2000QuantizationDefault.Parse(segment);
                        break;
                    case Jpeg2000Marker.SOT:
                        sot = Jpeg2000StartOfTilePart.Parse(segment, tileCount: 1);
                        break;
                    case Jpeg2000Marker.SOD:
                        if (sot == null)
                        {
                            throw Jpeg2000Binary.CreateException(sodFamilyName + " SOD marker was found before SOT.");
                        }

                        tileDataParts.Add(reader.ReadTileData(sot));
                        sot = null;
                        break;
                    case Jpeg2000Marker.EOC:
                        reachedEndOfCodestream = true;
                        break;
                }
            }

            if (siz == null || cod == null || qcd == null || tileDataParts.Count == 0)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing required marker data.");
            }

            return new Jpeg2000ParsedTilePart(siz, cod, qcd, sot ?? Jpeg2000StartOfTilePart.Empty, Concat(tileDataParts));
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
            public TilePartData(Jpeg2000StartOfTilePart startOfTile, byte[] data)
            {
                StartOfTile = startOfTile;
                Data = data;
            }

            public Jpeg2000StartOfTilePart StartOfTile { get; }

            public byte[] Data { get; }
        }
    }
}
