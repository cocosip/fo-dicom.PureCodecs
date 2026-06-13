using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000HtVlcSymbol : IEquatable<Jpeg2000HtVlcSymbol>
    {
        public Jpeg2000HtVlcSymbol(int context, int rho, bool hasMagnitudeResidual, int embK, int emb1)
        {
            Context = context;
            Rho = rho;
            HasMagnitudeResidual = hasMagnitudeResidual;
            EmbK = embK;
            Emb1 = emb1;
        }

        public int Context { get; }

        public int Rho { get; }

        public bool HasMagnitudeResidual { get; }

        public int EmbK { get; }

        public int Emb1 { get; }

        public bool Equals(Jpeg2000HtVlcSymbol other)
        {
            return Context == other.Context
                && Rho == other.Rho
                && HasMagnitudeResidual == other.HasMagnitudeResidual
                && EmbK == other.EmbK
                && Emb1 == other.Emb1;
        }

        public override bool Equals(object? obj)
        {
            return obj is Jpeg2000HtVlcSymbol other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Context;
                hash = (hash * 397) ^ Rho;
                hash = (hash * 397) ^ (HasMagnitudeResidual ? 1 : 0);
                hash = (hash * 397) ^ EmbK;
                hash = (hash * 397) ^ Emb1;
                return hash;
            }
        }
    }

    public sealed class Jpeg2000HtVlcEntry
    {
        public Jpeg2000HtVlcEntry(int context, int rho, bool hasMagnitudeResidual, int embK, int emb1, int codeword, int codewordLength)
        {
            if (context < 0 || context > 7)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K VLC context is outside the Annex C table range.");
            }

            if (codewordLength <= 0 || codewordLength > 7 || codeword >= (1 << codewordLength))
            {
                throw Jpeg2000Binary.CreateException("HTJ2K VLC codeword is invalid.");
            }

            Context = context;
            Rho = rho;
            HasMagnitudeResidual = hasMagnitudeResidual;
            EmbK = embK;
            Emb1 = emb1;
            Codeword = codeword;
            CodewordLength = codewordLength;
        }

        public int Context { get; }

        public int Rho { get; }

        public bool HasMagnitudeResidual { get; }

        public int EmbK { get; }

        public int Emb1 { get; }

        public int Codeword { get; }

        public int CodewordLength { get; }

        public Jpeg2000HtVlcSymbol Symbol => new Jpeg2000HtVlcSymbol(Context, Rho, HasMagnitudeResidual, EmbK, Emb1);
    }

    public sealed class Jpeg2000HtVlcTable
    {
        public static readonly Jpeg2000HtVlcTable InitialQuadRow = new Jpeg2000HtVlcTable(new[]
        {
            new Jpeg2000HtVlcEntry(0, 0x1, false, 0x0, 0x0, 0x06, 4),
            new Jpeg2000HtVlcEntry(0, 0x1, true, 0x1, 0x1, 0x3F, 7),
            new Jpeg2000HtVlcEntry(0, 0x2, false, 0x0, 0x0, 0x00, 3),
            new Jpeg2000HtVlcEntry(0, 0x2, true, 0x2, 0x2, 0x7F, 7),
            new Jpeg2000HtVlcEntry(0, 0x3, false, 0x0, 0x0, 0x11, 5),
            new Jpeg2000HtVlcEntry(0, 0x3, true, 0x2, 0x2, 0x5F, 7),
            new Jpeg2000HtVlcEntry(0, 0x8, false, 0x0, 0x0, 0x04, 3),
        });

        public static readonly Jpeg2000HtVlcTable NonInitialQuadRow = new Jpeg2000HtVlcTable(new[]
        {
            new Jpeg2000HtVlcEntry(0, 0x1, false, 0x0, 0x0, 0x00, 3),
            new Jpeg2000HtVlcEntry(0, 0x1, true, 0x1, 0x1, 0x27, 6),
            new Jpeg2000HtVlcEntry(0, 0x2, false, 0x0, 0x0, 0x06, 3),
            new Jpeg2000HtVlcEntry(0, 0x2, true, 0x2, 0x2, 0x17, 6),
            new Jpeg2000HtVlcEntry(0, 0x3, false, 0x0, 0x0, 0x0D, 5),
            new Jpeg2000HtVlcEntry(0, 0x8, false, 0x0, 0x0, 0x04, 3),
        });

        private readonly IReadOnlyList<Jpeg2000HtVlcEntry> _entries;

        public Jpeg2000HtVlcTable(IReadOnlyList<Jpeg2000HtVlcEntry> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            ValidatePrefixCodes(_entries);
        }

        public Jpeg2000HtVlcEntry Lookup(int context, int codeBits)
        {
            foreach (var entry in _entries)
            {
                if (entry.Context == context && (codeBits & ((1 << entry.CodewordLength) - 1)) == entry.Codeword)
                {
                    return entry;
                }
            }

            throw Jpeg2000Binary.CreateException("HTJ2K VLC codeword is not present in the Annex C table.");
        }

        public Jpeg2000HtVlcEntry Lookup(Jpeg2000HtVlcSymbol symbol)
        {
            foreach (var entry in _entries)
            {
                if (entry.Symbol.Equals(symbol))
                {
                    return entry;
                }
            }

            throw Jpeg2000Binary.CreateException("HTJ2K VLC symbol is not present in the Annex C table.");
        }

        private static void ValidatePrefixCodes(IReadOnlyList<Jpeg2000HtVlcEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                for (var j = i + 1; j < entries.Count; j++)
                {
                    var left = entries[i];
                    var right = entries[j];
                    if (left.Context != right.Context)
                    {
                        continue;
                    }

                    var length = Math.Min(left.CodewordLength, right.CodewordLength);
                    var leftPrefix = left.Codeword & ((1 << length) - 1);
                    var rightPrefix = right.Codeword & ((1 << length) - 1);
                    if (leftPrefix == rightPrefix)
                    {
                        throw Jpeg2000Binary.CreateException("HTJ2K VLC Annex C table contains an ambiguous prefix code.");
                    }
                }
            }
        }
    }

    public static class Jpeg2000HtVlcEncoder
    {
        public static byte[] Encode(IReadOnlyList<Jpeg2000HtVlcSymbol> symbols, bool initialQuadRow)
        {
            var table = initialQuadRow ? Jpeg2000HtVlcTable.InitialQuadRow : Jpeg2000HtVlcTable.NonInitialQuadRow;
            var writer = new Jpeg2000BitWriter();
            for (var i = 0; i < symbols.Count; i++)
            {
                var entry = table.Lookup(symbols[i]);
                writer.WriteBits((uint)entry.Codeword, entry.CodewordLength);
            }

            return writer.ToArray();
        }
    }

    public static class Jpeg2000HtVlcDecoder
    {
        public static Jpeg2000HtVlcSymbol[] Decode(byte[] bytes, int symbolCount, bool initialQuadRow)
        {
            var table = initialQuadRow ? Jpeg2000HtVlcTable.InitialQuadRow : Jpeg2000HtVlcTable.NonInitialQuadRow;
            var reader = new Jpeg2000BitReader(bytes);
            var symbols = new Jpeg2000HtVlcSymbol[symbolCount];
            for (var i = 0; i < symbols.Length; i++)
            {
                var codeBits = 0;
                for (var length = 1; length <= 7; length++)
                {
                    codeBits = (codeBits << 1) | (reader.ReadBit() ? 1 : 0);
                    try
                    {
                        var entry = table.Lookup(context: 0, codeBits);
                        if (entry.CodewordLength == length)
                        {
                            symbols[i] = entry.Symbol;
                            break;
                        }
                    }
                    catch
                    {
                        if (length == 7)
                        {
                            throw;
                        }
                    }
                }
            }

            return symbols;
        }
    }
}
