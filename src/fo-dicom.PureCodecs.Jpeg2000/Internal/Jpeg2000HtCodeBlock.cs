using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000HtQuad : IEquatable<Jpeg2000HtQuad>
    {
        public Jpeg2000HtQuad(int x, int y, int significancePattern)
        {
            X = x;
            Y = y;
            SignificancePattern = significancePattern;
        }

        public int X { get; }

        public int Y { get; }

        public int SignificancePattern { get; }

        public bool Equals(Jpeg2000HtQuad other)
        {
            return X == other.X && Y == other.Y && SignificancePattern == other.SignificancePattern;
        }

        public override bool Equals(object? obj)
        {
            return obj is Jpeg2000HtQuad other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ SignificancePattern;
                return hash;
            }
        }
    }

    public sealed class Jpeg2000HtCleanupResult
    {
        public Jpeg2000HtCleanupResult(IReadOnlyList<Jpeg2000HtQuad> quads, IReadOnlyList<bool> melEvents, IReadOnlyList<Jpeg2000HtVlcSymbol> vlcSymbols)
        {
            Quads = quads ?? Array.Empty<Jpeg2000HtQuad>();
            MelEvents = melEvents ?? Array.Empty<bool>();
            VlcSymbols = vlcSymbols ?? Array.Empty<Jpeg2000HtVlcSymbol>();
        }

        public IReadOnlyList<Jpeg2000HtQuad> Quads { get; }

        public IReadOnlyList<bool> MelEvents { get; }

        public IReadOnlyList<Jpeg2000HtVlcSymbol> VlcSymbols { get; }
    }

    public static class Jpeg2000HtCleanupPass
    {
        public static Jpeg2000HtCleanupResult Encode(Jpeg2000ClassicCodeBlock block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var quads = new List<Jpeg2000HtQuad>();
            var melEvents = new List<bool>();
            var vlcSymbols = new List<Jpeg2000HtVlcSymbol>();
            for (var y = 0; y < block.Height; y += 2)
            {
                for (var x = 0; x < block.Width; x += 2)
                {
                    var rho = SignificancePattern(block, x, y);
                    quads.Add(new Jpeg2000HtQuad(x, y, rho));
                    melEvents.Add(rho != 0);
                    vlcSymbols.Add(ToVlcSymbol(rho));
                }
            }

            return new Jpeg2000HtCleanupResult(quads, melEvents, vlcSymbols);
        }

        private static int SignificancePattern(Jpeg2000ClassicCodeBlock block, int x, int y)
        {
            var rho = 0;
            if (IsSignificant(block, x, y))
            {
                rho |= 0x1;
            }

            if (IsSignificant(block, x + 1, y))
            {
                rho |= 0x2;
            }

            if (IsSignificant(block, x, y + 1))
            {
                rho |= 0x4;
            }

            if (IsSignificant(block, x + 1, y + 1))
            {
                rho |= 0x8;
            }

            return rho;
        }

        private static bool IsSignificant(Jpeg2000ClassicCodeBlock block, int x, int y)
        {
            return x < block.Width && y < block.Height && block.Coefficients[y * block.Width + x] != 0;
        }

        private static Jpeg2000HtVlcSymbol ToVlcSymbol(int rho)
        {
            switch (rho)
            {
                case 0x1:
                case 0x2:
                case 0x8:
                    return new Jpeg2000HtVlcSymbol(0, rho, hasMagnitudeResidual: false, embK: 0, emb1: 0);
                default:
                    return new Jpeg2000HtVlcSymbol(0, 0x3, hasMagnitudeResidual: true, embK: 0x2, emb1: 0x2);
            }
        }
    }

    public sealed class Jpeg2000HtEncodedCodeBlock
    {
        public Jpeg2000HtEncodedCodeBlock(int width, int height, byte[] magSgn, byte[] mel, byte[] vlc)
        {
            Width = width;
            Height = height;
            MagSgn = magSgn ?? Array.Empty<byte>();
            Mel = mel ?? Array.Empty<byte>();
            Vlc = vlc ?? Array.Empty<byte>();
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] MagSgn { get; }

        public byte[] Mel { get; }

        public byte[] Vlc { get; }

        public int MagSgnLength => MagSgn.Length;

        public int MelLength => Mel.Length;

        public int VlcLength => Vlc.Length;
    }

    public static class Jpeg2000HtCodeBlockEncoder
    {
        public static Jpeg2000HtEncodedCodeBlock Encode(Jpeg2000ClassicCodeBlock block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var cleanup = Jpeg2000HtCleanupPass.Encode(block);
            return new Jpeg2000HtEncodedCodeBlock(
                block.Width,
                block.Height,
                Jpeg2000HtMagSgnEncoder.Encode(block.Coefficients),
                Jpeg2000HtMelEncoder.EncodeEvents(cleanup.MelEvents),
                Jpeg2000HtVlcEncoder.Encode(cleanup.VlcSymbols, initialQuadRow: true));
        }
    }

    public static class Jpeg2000HtCodeBlockDecoder
    {
        public static Jpeg2000ClassicCodeBlock Decode(Jpeg2000HtEncodedCodeBlock encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var coefficients = Jpeg2000HtMagSgnDecoder.Decode(encoded.MagSgn, encoded.Width * encoded.Height);
            return new Jpeg2000ClassicCodeBlock(encoded.Width, encoded.Height, coefficients);
        }

        public static Jpeg2000ClassicCodeBlock DecodeStandardCleanupPass(byte[] cleanupPass, int width, int height, int missingMostSignificantBits)
        {
            return Jpeg2000HtStandardCleanupPassDecoder.Decode(cleanupPass, width, height, missingMostSignificantBits);
        }
    }
}
