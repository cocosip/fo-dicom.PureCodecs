using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000HtStandardCleanupPassEncoder
    {
        public static byte[] Encode(Jpeg2000ClassicCodeBlock block, int missingMostSignificantBits)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (block.Width <= 0 || block.Height <= 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass dimensions are invalid.");
            }

            var bitPlane = 30 - missingMostSignificantBits;
            if (bitPlane <= 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass requires a positive CUP bit-plane.");
            }

            var mel = new MelWriter();
            var vlc = new ReverseVlcWriter();
            var ms = new MagSgnWriter();

            var eValues = new int[((block.Width + 1) / 2) + 2];
            var contextValues = new int[((block.Width + 1) / 2) + 2];
            EncodeInitialQuadRow(block, bitPlane, eValues, contextValues, mel, vlc, ms);
            EncodeNonInitialQuadRows(block, bitPlane, eValues, contextValues, mel, vlc, ms);

            mel.TerminateWith(vlc);
            ms.Terminate();
            return Assemble(ms.ToArray(), mel.ToArray(), vlc.ToArray());
        }

        private static void EncodeInitialQuadRow(
            Jpeg2000ClassicCodeBlock block,
            int bitPlane,
            int[] eValues,
            int[] contextValues,
            MelWriter mel,
            ReverseVlcWriter vlc,
            MagSgnWriter ms)
        {
            var context = 0;
            for (var x = 0; x < block.Width; x += 4)
            {
                var first = ReadQuad(block, x, 0, bitPlane);
                var tuple0 = LookupVlc(initial: true, context, first.Rho, first.Eps);
                vlc.Encode(tuple0.Codeword, tuple0.CodewordLength);
                if (context == 0)
                {
                    mel.Encode(first.Rho != 0);
                }

                WriteMagSgn(ms, first, tuple0);
                var uQ0 = first.U - 1;
                StoreInitialState(first, eValues, contextValues, x / 2);

                var uQ1 = 0;
                QuadInfo second = default;
                if (x + 2 < block.Width)
                {
                    var context1 = (first.Rho >> 1) | (first.Rho & 1);
                    second = ReadQuad(block, x + 2, 0, bitPlane);
                    var tuple1 = LookupVlc(initial: true, context1, second.Rho, second.Eps);
                    vlc.Encode(tuple1.Codeword, tuple1.CodewordLength);
                    if (context1 == 0)
                    {
                        mel.Encode(second.Rho != 0);
                    }

                    WriteMagSgn(ms, second, tuple1);
                    uQ1 = second.U - 1;
                    StoreInitialState(second, eValues, contextValues, (x + 2) / 2);
                    context = (second.Rho >> 1) | (second.Rho & 1);
                }
                else
                {
                    context = 0;
                }

                if (uQ0 > 0 && uQ1 > 0)
                {
                    mel.Encode(Math.Min(uQ0, uQ1) > 2);
                }

                WriteInitialUvlc(vlc, uQ0, uQ1);
            }
        }

        private static void EncodeNonInitialQuadRows(
            Jpeg2000ClassicCodeBlock block,
            int bitPlane,
            int[] eValues,
            int[] contextValues,
            MelWriter mel,
            ReverseVlcWriter vlc,
            MagSgnWriter ms)
        {
            for (var y = 2; y < block.Height; y += 2)
            {
                var eIndex = 0;
                var maxE = Math.Max(eValues[0], eValues[1]) - 1;
                eValues[0] = 0;
                var contextIndex = 0;
                var context0 = contextValues[0] + (contextValues[1] << 2);
                contextValues[0] = 0;

                for (var x = 0; x < block.Width; x += 4)
                {
                    var first = ReadQuad(block, x, y, bitPlane);
                    var kappa0 = (first.Rho & (first.Rho - 1)) != 0 ? Math.Max(1, maxE) : 1;
                    first = first.WithNonInitialU(Math.Max(first.EMax, kappa0), kappa0);
                    var uQ0 = first.U - kappa0;
                    var tuple0 = LookupVlc(initial: false, context0, first.Rho, first.Eps);
                    vlc.Encode(tuple0.Codeword, tuple0.CodewordLength);
                    if (context0 == 0)
                    {
                        mel.Encode(first.Rho != 0);
                    }

                    WriteMagSgn(ms, first, tuple0);
                    eValues[eIndex] = Math.Max(eValues[eIndex], first.E[1]);
                    eIndex++;
                    maxE = Math.Max(eValues[eIndex], eValues[eIndex + 1]) - 1;
                    eValues[eIndex] = first.E[3];
                    contextValues[contextIndex] |= (first.Rho & 2) >> 1;
                    contextIndex++;
                    var context1 = contextValues[contextIndex] + (contextValues[contextIndex + 1] << 2);
                    contextValues[contextIndex] = (first.Rho & 8) >> 3;

                    var uQ1 = 0;
                    QuadInfo second = default;
                    if (x + 2 < block.Width)
                    {
                        second = ReadQuad(block, x + 2, y, bitPlane);
                        var kappa1 = (second.Rho & (second.Rho - 1)) != 0 ? Math.Max(1, maxE) : 1;
                        context1 |= ((first.Rho & 4) >> 1) | ((first.Rho & 8) >> 2);
                        second = second.WithNonInitialU(Math.Max(second.EMax, kappa1), kappa1);
                        uQ1 = second.U - kappa1;
                        var tuple1 = LookupVlc(initial: false, context1, second.Rho, second.Eps);
                        vlc.Encode(tuple1.Codeword, tuple1.CodewordLength);
                        if (context1 == 0)
                        {
                            mel.Encode(second.Rho != 0);
                        }

                        WriteMagSgn(ms, second, tuple1);
                        eValues[eIndex] = Math.Max(eValues[eIndex], second.E[1]);
                        eIndex++;
                        maxE = Math.Max(eValues[eIndex], eValues[eIndex + 1]) - 1;
                        eValues[eIndex] = second.E[3];
                        contextValues[contextIndex] |= (second.Rho & 2) >> 1;
                        contextIndex++;
                        context0 = contextValues[contextIndex] + (contextValues[contextIndex + 1] << 2);
                        contextValues[contextIndex] = (second.Rho & 8) >> 3;
                    }

                    WriteUvlc(vlc, uQ0, uQ1);
                    context0 |= ((second.Rho & 4) >> 1) | ((second.Rho & 8) >> 2);
                }
            }
        }

        private static void StoreInitialState(QuadInfo quad, int[] eValues, int[] contextValues, int index)
        {
            eValues[index] = Math.Max(eValues[index], quad.E[1]);
            eValues[index + 1] = quad.E[3];
            contextValues[index] |= (quad.Rho & 2) >> 1;
            contextValues[index + 1] = (quad.Rho & 8) >> 3;
        }

        private static QuadInfo ReadQuad(Jpeg2000ClassicCodeBlock block, int x, int y, int bitPlane)
        {
            var rho = 0;
            var e = new int[4];
            var s = new uint[4];
            ReadSample(block, x, y, bitPlane, bit: 0, e, s, ref rho);
            ReadSample(block, x, y + 1, bitPlane, bit: 1, e, s, ref rho);
            ReadSample(block, x + 1, y, bitPlane, bit: 2, e, s, ref rho);
            ReadSample(block, x + 1, y + 1, bitPlane, bit: 3, e, s, ref rho);

            var eMax = Math.Max(Math.Max(e[0], e[1]), Math.Max(e[2], e[3]));
            return new QuadInfo(rho, Math.Max(eMax, 1), eMax, BuildEps(rho, e, eMax), e, s);
        }

        private static void ReadSample(
            Jpeg2000ClassicCodeBlock block,
            int x,
            int y,
            int bitPlane,
            int bit,
            int[] e,
            uint[] s,
            ref int rho)
        {
            if (x >= block.Width || y >= block.Height)
            {
                return;
            }

            var sample = unchecked((uint)block.Coefficients[y * block.Width + x]);
            var val = (sample + sample) >> bitPlane;
            val &= ~1u;
            if (val == 0)
            {
                return;
            }

            rho |= 1 << bit;
            var first = val - 1;
            e[bit] = 32 - CountLeadingZeros(first);
            var second = first - 1;
            s[bit] = second + (sample >> 31);
        }

        private static int BuildEps(int rho, int[] e, int eMax)
        {
            if (eMax <= 1)
            {
                return 0;
            }

            var eps = 0;
            for (var bit = 0; bit < 4; bit++)
            {
                if ((rho & (1 << bit)) != 0 && e[bit] == eMax)
                {
                    eps |= 1 << bit;
                }
            }

            return eps;
        }

        private static void WriteMagSgn(MagSgnWriter writer, QuadInfo quad, VlcTuple tuple)
        {
            for (var bit = 0; bit < 4; bit++)
            {
                if ((quad.Rho & (1 << bit)) == 0)
                {
                    continue;
                }

                var m = quad.U - ((tuple.Embedding >> bit) & 1);
                writer.Encode(quad.MagSgn[bit] & ((1u << m) - 1u), m);
            }
        }

        private static void WriteInitialUvlc(ReverseVlcWriter writer, int uQ0, int uQ1)
        {
            if (uQ0 > 2 && uQ1 > 2)
            {
                var first = UvlcTuple.Create(uQ0 - 2);
                var second = UvlcTuple.Create(uQ1 - 2);
                writer.Encode(first.Prefix, first.PrefixLength);
                writer.Encode(second.Prefix, second.PrefixLength);
                writer.Encode(first.Suffix, first.SuffixLength);
                writer.Encode(second.Suffix, second.SuffixLength);
                return;
            }

            if (uQ0 > 2 && uQ1 > 0)
            {
                var first = UvlcTuple.Create(uQ0);
                writer.Encode(first.Prefix, first.PrefixLength);
                writer.Encode(uQ1 - 1, 1);
                writer.Encode(first.Suffix, first.SuffixLength);
                return;
            }

            WriteUvlc(writer, uQ0, uQ1);
        }

        private static void WriteUvlc(ReverseVlcWriter writer, int uQ0, int uQ1)
        {
            var first = UvlcTuple.Create(uQ0);
            var second = UvlcTuple.Create(uQ1);
            writer.Encode(first.Prefix, first.PrefixLength);
            writer.Encode(second.Prefix, second.PrefixLength);
            writer.Encode(first.Suffix, first.SuffixLength);
            writer.Encode(second.Suffix, second.SuffixLength);
        }

        private static byte[] Assemble(byte[] magSgn, byte[] mel, byte[] vlc)
        {
            var result = new byte[magSgn.Length + mel.Length + vlc.Length];
            Buffer.BlockCopy(magSgn, 0, result, 0, magSgn.Length);
            Buffer.BlockCopy(mel, 0, result, magSgn.Length, mel.Length);
            Buffer.BlockCopy(vlc, 0, result, magSgn.Length + mel.Length, vlc.Length);
            var scup = mel.Length + vlc.Length;
            result[result.Length - 1] = (byte)(scup >> 4);
            result[result.Length - 2] = (byte)((result[result.Length - 2] & 0xF0) | (scup & 0x0F));
            return result;
        }

        private static VlcTuple LookupVlc(bool initial, int context, int rho, int eps)
        {
            var packed = initial
                ? Jpeg2000HtStandardEncodeTables.VlcTable0[(context << 8) | (rho << 4) | eps]
                : Jpeg2000HtStandardEncodeTables.VlcTable1[(context << 8) | (rho << 4) | eps];
            if (packed == 0 && !(context == 0 && rho == 0))
            {
                throw Jpeg2000Binary.CreateException($"HTJ2K standard VLC encode tuple is not implemented for context {context}, rho 0x{rho:X}, eps 0x{eps:X}.");
            }

            return new VlcTuple(
                packed >> 8,
                (packed >> 4) & 0x7,
                packed & 0xF);
        }

        private static int CountLeadingZeros(uint value)
        {
            if (value == 0)
            {
                return 32;
            }

            var count = 0;
            for (var bit = 31; bit >= 0 && ((value >> bit) & 1u) == 0; bit--)
            {
                count++;
            }

            return count;
        }

        private readonly struct QuadInfo
        {
            public QuadInfo(int rho, int u, int eMax, int eps, int[] e, uint[] magSgn)
            {
                Rho = rho;
                U = u;
                EMax = eMax;
                Eps = eps;
                E = e;
                MagSgn = magSgn;
            }

            public int Rho { get; }

            public int U { get; }

            public int EMax { get; }

            public int Eps { get; }

            public int[] E { get; }

            public uint[] MagSgn { get; }

            public QuadInfo WithNonInitialU(int u, int kappa)
            {
                return new QuadInfo(Rho, u, EMax, u > kappa ? BuildEps(Rho, E, EMax) : 0, E, MagSgn);
            }
        }

        private readonly struct VlcTuple
        {
            public VlcTuple(int codeword, int codewordLength, int embedding)
            {
                Codeword = codeword;
                CodewordLength = codewordLength;
                Embedding = embedding;
            }

            public int Codeword { get; }

            public int CodewordLength { get; }

            public int Embedding { get; }
        }

        private readonly struct UvlcTuple
        {
            private UvlcTuple(int prefix, int prefixLength, int suffix, int suffixLength)
            {
                Prefix = prefix;
                PrefixLength = prefixLength;
                Suffix = suffix;
                SuffixLength = suffixLength;
            }

            public int Prefix { get; }

            public int PrefixLength { get; }

            public int Suffix { get; }

            public int SuffixLength { get; }

            public static UvlcTuple Create(int value)
            {
                switch (value)
                {
                    case 0:
                        return new UvlcTuple(0, 0, 0, 0);
                    case 1:
                        return new UvlcTuple(1, 1, 0, 0);
                    case 2:
                        return new UvlcTuple(2, 2, 0, 0);
                    case 3:
                        return new UvlcTuple(4, 3, 0, 1);
                    case 4:
                        return new UvlcTuple(4, 3, 1, 1);
                    default:
                        if (value < 33)
                        {
                            return new UvlcTuple(0, 3, value - 5, 5);
                        }

                        throw Jpeg2000Binary.CreateException("HTJ2K UVLC value is outside the implemented encoder range.");
                }
            }
        }

        private sealed class MelWriter
        {
            private static readonly int[] Exponents = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5 };
            private readonly List<byte> _bytes = new List<byte>();
            private int _remainingBits = 8;
            private int _tmp;
            private int _run;
            private int _k;
            private int _threshold = 1;

            public void Encode(bool bit)
            {
                if (!bit)
                {
                    _run++;
                    if (_run < _threshold)
                    {
                        return;
                    }

                    EmitBit(1);
                    _run = 0;
                    _k = Math.Min(12, _k + 1);
                    _threshold = 1 << Exponents[_k];
                    return;
                }

                EmitBit(0);
                for (var t = Exponents[_k]; t > 0;)
                {
                    EmitBit((_run >> --t) & 1);
                }

                _run = 0;
                _k = Math.Max(0, _k - 1);
                _threshold = 1 << Exponents[_k];
            }

            public void TerminateWith(ReverseVlcWriter vlc)
            {
                if (_run > 0)
                {
                    EmitBit(1);
                }

                _tmp <<= _remainingBits;
                var melMask = (0xFF << _remainingBits) & 0xFF;
                var vlcMask = 0xFF >> (8 - vlc.UsedBits);
                if ((melMask | vlcMask) == 0)
                {
                    return;
                }

                var fuse = _tmp | vlc.Tmp;
                if ((((fuse ^ _tmp) & melMask) | ((fuse ^ vlc.Tmp) & vlcMask)) == 0 && fuse != 0xFF && vlc.Position > 1)
                {
                    _bytes.Add((byte)fuse);
                }
                else
                {
                    _bytes.Add((byte)_tmp);
                    vlc.FlushTmp();
                }
            }

            public byte[] ToArray()
            {
                return _bytes.ToArray();
            }

            private void EmitBit(int bit)
            {
                _tmp = (_tmp << 1) + bit;
                _remainingBits--;
                if (_remainingBits != 0)
                {
                    return;
                }

                _bytes.Add((byte)_tmp);
                _remainingBits = _tmp == 0xFF ? 7 : 8;
                _tmp = 0;
            }
        }

        private sealed class ReverseVlcWriter
        {
            private readonly List<byte> _tail = new List<byte>();
            private bool _lastGreaterThan8F = true;

            public ReverseVlcWriter()
            {
                Tmp = 0xF;
                UsedBits = 4;
                Position = 1;
            }

            public int Tmp { get; private set; }

            public int UsedBits { get; private set; }

            public int Position { get; private set; }

            public void Encode(int codeword, int codewordLength)
            {
                while (codewordLength > 0)
                {
                    var available = 8 - (_lastGreaterThan8F ? 1 : 0) - UsedBits;
                    var take = Math.Min(available, codewordLength);
                    Tmp |= (codeword & ((1 << take) - 1)) << UsedBits;
                    UsedBits += take;
                    available -= take;
                    codewordLength -= take;
                    codeword >>= take;
                    if (available != 0)
                    {
                        continue;
                    }

                    if (_lastGreaterThan8F && Tmp != 0x7F)
                    {
                        _lastGreaterThan8F = false;
                        continue;
                    }

                    FlushTmp();
                }
            }

            public void FlushTmp()
            {
                _tail.Add((byte)Tmp);
                Position++;
                _lastGreaterThan8F = Tmp > 0x8F;
                Tmp = 0;
                UsedBits = 0;
            }

            public byte[] ToArray()
            {
                var result = new byte[_tail.Count + 1];
                for (var i = 0; i < _tail.Count; i++)
                {
                    result[i] = _tail[_tail.Count - 1 - i];
                }

                result[result.Length - 1] = 0xFF;
                return result;
            }
        }

        private sealed class MagSgnWriter
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _maxBits = 8;
            private int _usedBits;
            private uint _tmp;

            public void Encode(uint codeword, int codewordLength)
            {
                while (codewordLength > 0)
                {
                    var take = Math.Min(_maxBits - _usedBits, codewordLength);
                    _tmp |= (codeword & ((1u << take) - 1u)) << _usedBits;
                    _usedBits += take;
                    codeword >>= take;
                    codewordLength -= take;
                    if (_usedBits < _maxBits)
                    {
                        continue;
                    }

                    _bytes.Add((byte)_tmp);
                    _maxBits = _tmp == 0xFF ? 7 : 8;
                    _tmp = 0;
                    _usedBits = 0;
                }
            }

            public void Terminate()
            {
                if (_usedBits != 0)
                {
                    var unused = _maxBits - _usedBits;
                    _tmp |= (uint)((0xFF & ((1 << unused) - 1)) << _usedBits);
                    if (_tmp != 0xFF)
                    {
                        _bytes.Add((byte)_tmp);
                    }
                }
                else if (_maxBits == 7 && _bytes.Count > 0)
                {
                    _bytes.RemoveAt(_bytes.Count - 1);
                }
            }

            public byte[] ToArray()
            {
                return _bytes.ToArray();
            }
        }
    }
}
