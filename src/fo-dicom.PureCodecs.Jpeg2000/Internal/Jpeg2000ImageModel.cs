using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ImageModel
    {
        private Jpeg2000ImageModel(
            uint width,
            uint height,
            int tilesWide,
            int tilesHigh,
            Jpeg2000ComponentModel[] components,
            Jpeg2000TileModel[] tiles)
        {
            Width = width;
            Height = height;
            TilesWide = tilesWide;
            TilesHigh = tilesHigh;
            Components = components;
            Tiles = tiles;
        }

        public uint Width { get; }

        public uint Height { get; }

        public int TilesWide { get; }

        public int TilesHigh { get; }

        public IReadOnlyList<Jpeg2000ComponentModel> Components { get; }

        public IReadOnlyList<Jpeg2000TileModel> Tiles { get; }

        public static Jpeg2000ImageModel FromSizeSegment(Jpeg2000SizeSegment siz)
        {
            if (siz == null)
            {
                throw new ArgumentNullException(nameof(siz));
            }

            var width = siz.ReferenceGridWidth - siz.ImageOffsetX;
            var height = siz.ReferenceGridHeight - siz.ImageOffsetY;
            var tilesWide = CheckedCeilingDiv(siz.ReferenceGridWidth - siz.TileOffsetX, siz.TileWidth);
            var tilesHigh = CheckedCeilingDiv(siz.ReferenceGridHeight - siz.TileOffsetY, siz.TileHeight);
            var components = new Jpeg2000ComponentModel[siz.Components.Count];
            for (var i = 0; i < components.Length; i++)
            {
                var component = siz.Components[i];
                components[i] = new Jpeg2000ComponentModel(
                    component.Index,
                    component.Precision,
                    component.IsSigned,
                    component.HorizontalSeparation,
                    component.VerticalSeparation);
            }

            var tiles = new Jpeg2000TileModel[tilesWide * tilesHigh];
            var tileIndex = 0;
            for (var y = 0; y < tilesHigh; y++)
            {
                for (var x = 0; x < tilesWide; x++)
                {
                    var x0 = Math.Max(siz.ImageOffsetX, siz.TileOffsetX + (uint)x * siz.TileWidth);
                    var y0 = Math.Max(siz.ImageOffsetY, siz.TileOffsetY + (uint)y * siz.TileHeight);
                    var x1 = Math.Min(siz.ReferenceGridWidth, siz.TileOffsetX + (uint)(x + 1) * siz.TileWidth);
                    var y1 = Math.Min(siz.ReferenceGridHeight, siz.TileOffsetY + (uint)(y + 1) * siz.TileHeight);
                    tiles[tileIndex] = new Jpeg2000TileModel(tileIndex, x0, y0, x1, y1);
                    tileIndex++;
                }
            }

            return new Jpeg2000ImageModel(width, height, tilesWide, tilesHigh, components, tiles);
        }

        private static int CheckedCeilingDiv(uint value, uint divisor)
        {
            var result = (value + divisor - 1) / divisor;
            if (result > int.MaxValue)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 tile grid is too large.");
            }

            return (int)result;
        }
    }
}
