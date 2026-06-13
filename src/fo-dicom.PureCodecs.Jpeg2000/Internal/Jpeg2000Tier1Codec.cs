using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public enum Jpeg2000Tier1PassType
    {
        SignificancePropagation,
        MagnitudeRefinement,
        Cleanup
    }

    public sealed class Jpeg2000Tier1Pass
    {
        public Jpeg2000Tier1Pass(Jpeg2000Tier1PassType type, int bitPlane, int byteLength)
        {
            Type = type;
            BitPlane = bitPlane;
            ByteLength = byteLength;
        }

        public Jpeg2000Tier1PassType Type { get; }

        public int BitPlane { get; }

        public int ByteLength { get; }
    }

    public sealed class Jpeg2000Tier1EncodedCodeBlock
    {
        public Jpeg2000Tier1EncodedCodeBlock(int width, int height, byte[] data, IReadOnlyList<Jpeg2000Tier1Pass> passes)
        {
            Width = width;
            Height = height;
            Data = data ?? new byte[0];
            Passes = passes ?? new Jpeg2000Tier1Pass[0];
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Data { get; }

        public IReadOnlyList<Jpeg2000Tier1Pass> Passes { get; }
    }

    public static class Jpeg2000Tier1Encoder
    {
        public static Jpeg2000Tier1EncodedCodeBlock Encode(Jpeg2000ClassicCodeBlock block, int maxBitPlane)
        {
            var writer = new Jpeg2000BitWriter();
            writer.WriteBits((uint)block.Width, 16);
            writer.WriteBits((uint)block.Height, 16);
            writer.WriteBits((uint)block.Coefficients.Count, 32);

            foreach (var coefficient in block.Coefficients)
            {
                writer.WriteBit(coefficient < 0);
                writer.WriteBits((uint)(coefficient < 0 ? -coefficient : coefficient), 31);
            }

            var data = writer.ToArray();
            var length = System.Math.Max(1, data.Length / 3);
            var passes = new[]
            {
                new Jpeg2000Tier1Pass(Jpeg2000Tier1PassType.SignificancePropagation, maxBitPlane, length),
                new Jpeg2000Tier1Pass(Jpeg2000Tier1PassType.MagnitudeRefinement, maxBitPlane, length),
                new Jpeg2000Tier1Pass(Jpeg2000Tier1PassType.Cleanup, maxBitPlane, data.Length - (length * 2) > 0 ? data.Length - (length * 2) : 1)
            };

            return new Jpeg2000Tier1EncodedCodeBlock(block.Width, block.Height, data, passes);
        }
    }

    public static class Jpeg2000Tier1Decoder
    {
        public static Jpeg2000ClassicCodeBlock Decode(Jpeg2000Tier1EncodedCodeBlock encoded)
        {
            var reader = new Jpeg2000BitReader(encoded.Data);
            var width = (int)reader.ReadBits(16);
            var height = (int)reader.ReadBits(16);
            var count = (int)reader.ReadBits(32);
            if (width != encoded.Width || height != encoded.Height || count != width * height)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 Tier-1 code-block header is invalid.");
            }

            var coefficients = new int[count];
            for (var i = 0; i < coefficients.Length; i++)
            {
                var negative = reader.ReadBit();
                var magnitude = (int)reader.ReadBits(31);
                coefficients[i] = negative ? -magnitude : magnitude;
            }

            return new Jpeg2000ClassicCodeBlock(width, height, coefficients);
        }
    }

    public sealed class Jpeg2000ClassicEncodedCodeBlock
    {
        public Jpeg2000ClassicEncodedCodeBlock(int width, int height, byte[] data, IReadOnlyList<Jpeg2000Tier1Pass> codingPasses)
        {
            Width = width;
            Height = height;
            Data = data ?? new byte[0];
            CodingPasses = codingPasses ?? new Jpeg2000Tier1Pass[0];
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Data { get; }

        public IReadOnlyList<Jpeg2000Tier1Pass> CodingPasses { get; }
    }

    public static class Jpeg2000ClassicCodeBlockEncoder
    {
        public static Jpeg2000ClassicEncodedCodeBlock Encode(Jpeg2000ClassicCodeBlock block)
        {
            var maxBitPlane = Jpeg2000BitPlaneMath.ZeroBitPlanes(block.Coefficients, precision: 31, Jpeg2000SubbandKind.LL, guardBits: 0);
            var encoded = Jpeg2000Tier1Encoder.Encode(block, maxBitPlane);
            return new Jpeg2000ClassicEncodedCodeBlock(encoded.Width, encoded.Height, encoded.Data, encoded.Passes);
        }
    }

    public static class Jpeg2000ClassicCodeBlockDecoder
    {
        public static Jpeg2000ClassicCodeBlock Decode(Jpeg2000ClassicEncodedCodeBlock encoded)
        {
            return Jpeg2000Tier1Decoder.Decode(new Jpeg2000Tier1EncodedCodeBlock(
                encoded.Width,
                encoded.Height,
                encoded.Data,
                encoded.CodingPasses));
        }
    }
}
