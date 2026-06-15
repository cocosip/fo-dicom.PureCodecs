namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
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
            byte[]? tileData = null;
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

                        tileData = reader.ReadTileData(sot);
                        break;
                    case Jpeg2000Marker.EOC:
                        reachedEndOfCodestream = true;
                        break;
                }
            }

            if (siz == null || cod == null || qcd == null || sot == null || tileData == null)
            {
                throw Jpeg2000Binary.CreateException(codestreamName + " codestream is missing required marker data.");
            }

            return new Jpeg2000ParsedTilePart(siz, cod, qcd, sot, tileData);
        }
    }
}
