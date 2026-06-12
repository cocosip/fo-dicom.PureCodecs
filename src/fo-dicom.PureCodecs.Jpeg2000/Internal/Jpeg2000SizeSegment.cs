using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000SizeSegment
    {
        private Jpeg2000SizeSegment(
            int capabilities,
            uint referenceGridWidth,
            uint referenceGridHeight,
            uint imageOffsetX,
            uint imageOffsetY,
            uint tileWidth,
            uint tileHeight,
            uint tileOffsetX,
            uint tileOffsetY,
            Jpeg2000SizeComponent[] components)
        {
            Capabilities = capabilities;
            ReferenceGridWidth = referenceGridWidth;
            ReferenceGridHeight = referenceGridHeight;
            ImageOffsetX = imageOffsetX;
            ImageOffsetY = imageOffsetY;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileOffsetX = tileOffsetX;
            TileOffsetY = tileOffsetY;
            Components = components;
        }

        public int Capabilities { get; }

        public uint ReferenceGridWidth { get; }

        public uint ReferenceGridHeight { get; }

        public uint ImageOffsetX { get; }

        public uint ImageOffsetY { get; }

        public uint TileWidth { get; }

        public uint TileHeight { get; }

        public uint TileOffsetX { get; }

        public uint TileOffsetY { get; }

        public IReadOnlyList<Jpeg2000SizeComponent> Components { get; }

        public static Jpeg2000SizeSegment Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.SIZ)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ marker segment expected.");
            }

            var payload = segment.Payload;
            if (payload.Length < 36)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ payload is too short.");
            }

            var componentCount = Jpeg2000Binary.ReadUInt16(payload, 34);
            var expectedLength = 36 + componentCount * 3;
            if (payload.Length != expectedLength)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ component payload length is invalid.");
            }

            var components = new Jpeg2000SizeComponent[componentCount];
            var offset = 36;
            for (var index = 0; index < componentCount; index++)
            {
                var ssiz = payload[offset];
                components[index] = new Jpeg2000SizeComponent(
                    index,
                    (ssiz & 0x7F) + 1,
                    (ssiz & 0x80) != 0,
                    payload[offset + 1],
                    payload[offset + 2]);
                offset += 3;
            }

            var siz = new Jpeg2000SizeSegment(
                Jpeg2000Binary.ReadUInt16(payload, 0),
                Jpeg2000Binary.ReadUInt32(payload, 2),
                Jpeg2000Binary.ReadUInt32(payload, 6),
                Jpeg2000Binary.ReadUInt32(payload, 10),
                Jpeg2000Binary.ReadUInt32(payload, 14),
                Jpeg2000Binary.ReadUInt32(payload, 18),
                Jpeg2000Binary.ReadUInt32(payload, 22),
                Jpeg2000Binary.ReadUInt32(payload, 26),
                Jpeg2000Binary.ReadUInt32(payload, 30),
                components);

            if (siz.ReferenceGridWidth <= siz.ImageOffsetX || siz.ReferenceGridHeight <= siz.ImageOffsetY)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ image bounds are invalid.");
            }

            if (siz.TileWidth == 0 || siz.TileHeight == 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 SIZ tile dimensions must be non-zero.");
            }

            return siz;
        }
    }
}
