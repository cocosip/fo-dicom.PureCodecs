namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000StartOfTilePart
    {
        private Jpeg2000StartOfTilePart(int tileIndex, uint tilePartLength, int tilePartIndex, int tilePartCount)
        {
            TileIndex = tileIndex;
            TilePartLength = tilePartLength;
            TilePartIndex = tilePartIndex;
            TilePartCount = tilePartCount;
        }

        public int TileIndex { get; }

        public uint TilePartLength { get; }

        public int TilePartIndex { get; }

        public int TilePartCount { get; }

        public static Jpeg2000StartOfTilePart Empty { get; } = new Jpeg2000StartOfTilePart(0, 0, 0, 0);

        public static Jpeg2000StartOfTilePart Parse(Jpeg2000MarkerSegment segment, int tileCount)
        {
            if (segment.Code != Jpeg2000Marker.SOT)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT marker segment expected.");
            }

            if (segment.Payload.Length != 8)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT payload length is invalid.");
            }

            var tileIndex = Jpeg2000Binary.ReadUInt16(segment.Payload, 0);
            if (tileIndex >= tileCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT tile index is outside the tile range.");
            }

            var tilePartLength = Jpeg2000Binary.ReadUInt32(segment.Payload, 2);
            var tilePartIndex = segment.Payload[6];
            var tilePartCount = segment.Payload[7];
            if (tilePartCount != 0 && tilePartIndex >= tilePartCount)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SOT tile-part index is outside the declared tile-part count.");
            }

            return new Jpeg2000StartOfTilePart(tileIndex, tilePartLength, tilePartIndex, tilePartCount);
        }
    }
}
