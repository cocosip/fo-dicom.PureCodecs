using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000TagTree
    {
        public Jpeg2000TagTree(int width, int height, IReadOnlyList<int> values)
        {
            if (width <= 0 || height <= 0 || values == null || values.Count != width * height)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 tag-tree dimensions are invalid.");
            }

            Width = width;
            Height = height;
            var copy = new int[values.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = values[i];
            }

            Values = copy;
        }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<int> Values { get; }

        public int GetValue(int x, int y)
        {
            return Values[(y * Width) + x];
        }
    }

    public static class Jpeg2000TagTreeEncoder
    {
        public static byte[] Encode(Jpeg2000TagTree tree)
        {
            var writer = new Jpeg2000BitWriter();
            writer.WriteBits((uint)tree.Width, 16);
            writer.WriteBits((uint)tree.Height, 16);
            foreach (var value in tree.Values)
            {
                writer.WriteBits((uint)value, 16);
            }

            return writer.ToArray();
        }
    }

    public static class Jpeg2000TagTreeDecoder
    {
        public static Jpeg2000TagTree Decode(byte[] bytes)
        {
            var reader = new Jpeg2000BitReader(bytes);
            var width = (int)reader.ReadBits(16);
            var height = (int)reader.ReadBits(16);
            var values = new int[width * height];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = (int)reader.ReadBits(16);
            }

            return new Jpeg2000TagTree(width, height, values);
        }
    }
}
