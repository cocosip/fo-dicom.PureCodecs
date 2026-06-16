using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000HtStandardCleanupPassDecoder
    {
        public static Jpeg2000ClassicCodeBlock Decode(byte[] cleanupPass, int width, int height, int missingMostSignificantBits)
        {
            return Decode(cleanupPass, Array.Empty<byte>(), passCount: 1, width, height, missingMostSignificantBits);
        }

        public static Jpeg2000ClassicCodeBlock Decode(byte[] cleanupPass, byte[] refinementPasses, int passCount, int width, int height, int missingMostSignificantBits)
        {
            if (cleanupPass == null)
            {
                throw new ArgumentNullException(nameof(cleanupPass));
            }

            refinementPasses = refinementPasses ?? Array.Empty<byte>();
            if (width <= 0 || height <= 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass dimensions are invalid.");
            }

            if (passCount > 1 && refinementPasses.Length == 0)
            {
                passCount = 1;
            }

            if (passCount > 3)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K pure decoder currently supports up to three HT coding passes.");
            }

            if (cleanupPass.Length < 2)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass is too short.");
            }

            var lcup = cleanupPass.Length;
            var scup = (cleanupPass[lcup - 1] << 4) + (cleanupPass[lcup - 2] & 0x0F);
            if (scup < 2 || scup > lcup || scup > 4079)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass has an invalid Scup locator.");
            }

            var p = 30 - missingMostSignificantBits;
            if (p <= 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass requires a positive CUP bit-plane.");
            }

            var sstr = ((width + 2) + 7) & ~7;
            var scratch = new ushort[sstr * (((height + 1) / 2) + 1)];
            DecodeMelVlc(cleanupPass, width, height, lcup, scup, sstr, scratch);
            var coefficients = DecodeMagSgn(cleanupPass, width, height, lcup, scup, sstr, scratch, p, missingMostSignificantBits + 2);
            if (passCount > 1)
            {
                DecodeRefinementPasses(cleanupPass, refinementPasses, passCount, coefficients, width, height, p, sstr, scratch);
            }

            return new Jpeg2000ClassicCodeBlock(width, height, coefficients);
        }

        private static void DecodeRefinementPasses(
            byte[] cleanupPass,
            byte[] refinementPasses,
            int passCount,
            int[] coefficients,
            int width,
            int height,
            int bitPlane,
            int sstr,
            ushort[] scratch)
        {
            if (bitPlane < 2)
            {
                return;
            }

            var mstr = (((width + 3) >> 2) + 2 + 7) & ~7;
            var sigma = new ushort[mstr * (((height + 3) >> 2) + 1)];
            BuildSigmaFromCleanup(scratch, sstr, sigma, mstr, width, height);
            DecodeSignificancePropagation(refinementPasses, coefficients, sigma, mstr, width, height, bitPlane);
            if (passCount > 2)
            {
                DecodeMagnitudeRefinement(cleanupPass, refinementPasses, coefficients, sigma, mstr, width, height, bitPlane);
            }
        }

        private static void BuildSigmaFromCleanup(ushort[] scratch, int sstr, ushort[] sigma, int mstr, int width, int height)
        {
            int y;
            for (y = 0; y < height; y += 4)
            {
                var sp = (y >> 1) * sstr;
                var dp = (y >> 2) * mstr;
                for (var x = 0; x < width; x += 4, sp += 4, dp++)
                {
                    var t0 = ((scratch[sp] & 0x30u) >> 4) | ((scratch[sp] & 0xC0u) >> 2);
                    t0 |= ((scratch[sp + 2] & 0x30u) << 4) | ((scratch[sp + 2] & 0xC0u) << 6);
                    uint t1 = 0;
                    if (y + 2 < height)
                    {
                        t1 = ((scratch[sp + sstr] & 0x30u) >> 2) | (scratch[sp + sstr] & 0xC0u);
                        t1 |= ((scratch[sp + sstr + 2] & 0x30u) << 6) | ((scratch[sp + sstr + 2] & 0xC0u) << 8);
                    }

                    sigma[dp] = (ushort)(t0 | t1);
                }

                sigma[dp] = 0;
            }

            var reset = (y >> 2) * mstr;
            for (var x = 0; x < width; x += 4, reset++)
            {
                sigma[reset] = 0;
            }

            sigma[reset] = 0;
        }

        private static void DecodeSignificancePropagation(byte[] refinementPasses, int[] coefficients, ushort[] sigma, int mstr, int width, int height, int bitPlane)
        {
            var prevRowSig = new ushort[((width + 3) >> 2) + 8];
            var reader = new HtForwardSegmentReader(refinementPasses, refinementPasses.Length, 0x00);
            for (var y = 0; y < height; y += 4)
            {
                var pattern = 0xFFFFu;
                if (height - y < 4)
                {
                    pattern = 0x7777u;
                    if (height - y < 3)
                    {
                        pattern = 0x3333u;
                        if (height - y < 2)
                        {
                            pattern = 0x1111u;
                        }
                    }
                }

                var prev = 0u;
                for (var x = 0; x < width; x += 4)
                {
                    var currentPattern = pattern;
                    var outside = x + 4 - width;
                    if (outside > 0)
                    {
                        currentPattern >>= outside * 4;
                    }

                    var sigmaIndex = (y >> 2) * mstr + (x >> 2);
                    var ps = ReadTwoUShorts(prevRowSig, x >> 2);
                    var ns = ReadTwoUShorts(sigma, sigmaIndex + mstr);
                    var u = (ps & 0x88888888u) >> 3;
                    u |= (ns & 0x11111111u) << 3;

                    var cs = ReadTwoUShorts(sigma, sigmaIndex);
                    var mbr = cs;
                    mbr |= (cs & 0x77777777u) << 1;
                    mbr |= (cs & 0xEEEEEEEEu) >> 1;
                    mbr |= u;
                    var t = mbr;
                    mbr |= t << 4;
                    mbr |= t >> 4;
                    mbr |= prev >> 12;
                    mbr &= currentPattern;
                    mbr &= ~cs;

                    var newSig = mbr;
                    if (newSig != 0)
                    {
                        var codeword = reader.Fetch();
                        var consumed = 0;
                        var colMask = 0xFu;
                        var invSig = ~cs & currentPattern;
                        for (var i = 0; i < 16; i += 4, colMask <<= 4)
                        {
                            if ((colMask & newSig) == 0)
                            {
                                continue;
                            }

                            var sampleMask = 0x1111u & colMask;
                            if ((newSig & sampleMask) != 0)
                            {
                                newSig &= ~sampleMask;
                                if ((codeword & 1) != 0)
                                {
                                    newSig |= (0x33u << i) & invSig;
                                }

                                codeword >>= 1;
                                consumed++;
                            }

                            sampleMask <<= 1;
                            if ((newSig & sampleMask) != 0)
                            {
                                newSig &= ~sampleMask;
                                if ((codeword & 1) != 0)
                                {
                                    newSig |= (0x76u << i) & invSig;
                                }

                                codeword >>= 1;
                                consumed++;
                            }

                            sampleMask <<= 1;
                            if ((newSig & sampleMask) != 0)
                            {
                                newSig &= ~sampleMask;
                                if ((codeword & 1) != 0)
                                {
                                    newSig |= (0xECu << i) & invSig;
                                }

                                codeword >>= 1;
                                consumed++;
                            }

                            sampleMask <<= 1;
                            if ((newSig & sampleMask) != 0)
                            {
                                newSig &= ~sampleMask;
                                if ((codeword & 1) != 0)
                                {
                                    newSig |= (0xC8u << i) & invSig;
                                }

                                codeword >>= 1;
                                consumed++;
                            }
                        }

                        if (newSig != 0)
                        {
                            var value = 3u << (bitPlane - 2);
                            colMask = 0xFu;
                            for (var i = 0; i < 4; i++, colMask <<= 4)
                            {
                                if ((colMask & newSig) == 0)
                                {
                                    continue;
                                }

                                var column = x + i;
                                if (column >= width)
                                {
                                    break;
                                }

                                var sampleMask = 0x1111u & colMask;
                                if ((newSig & sampleMask) != 0 && y < height)
                                {
                                    coefficients[(y * width) + column] = unchecked((int)(((codeword & 1) << 31) | value));
                                    codeword >>= 1;
                                    consumed++;
                                }

                                sampleMask <<= 1;
                                if ((newSig & sampleMask) != 0 && y + 1 < height)
                                {
                                    coefficients[((y + 1) * width) + column] = unchecked((int)(((codeword & 1) << 31) | value));
                                    codeword >>= 1;
                                    consumed++;
                                }

                                sampleMask <<= 1;
                                if ((newSig & sampleMask) != 0 && y + 2 < height)
                                {
                                    coefficients[((y + 2) * width) + column] = unchecked((int)(((codeword & 1) << 31) | value));
                                    codeword >>= 1;
                                    consumed++;
                                }

                                sampleMask <<= 1;
                                if ((newSig & sampleMask) != 0 && y + 3 < height)
                                {
                                    coefficients[((y + 3) * width) + column] = unchecked((int)(((codeword & 1) << 31) | value));
                                    codeword >>= 1;
                                    consumed++;
                                }
                            }
                        }

                        reader.Advance(consumed);
                    }

                    newSig |= cs;
                    WriteLowUShort(prevRowSig, x >> 2, newSig);
                    var integrated = newSig;
                    integrated |= (integrated & 0x7777u) << 1;
                    integrated |= (integrated & 0xEEEEu) >> 1;
                    prev = (integrated | u) & 0xF000u;
                }
            }
        }

        private static void DecodeMagnitudeRefinement(byte[] cleanupPass, byte[] refinementPasses, int[] coefficients, ushort[] sigma, int mstr, int width, int height, int bitPlane)
        {
            var reader = new HtReverseMrpReader(refinementPasses);
            var half = 1u << (bitPlane - 2);
            for (var y = 0; y < height; y += 4)
            {
                var sigmaIndex = (y >> 2) * mstr;
                for (var x = 0; x < width; x += 8, sigmaIndex += 2)
                {
                    var codeword = reader.Fetch();
                    var sig = ReadTwoUShorts(sigma, sigmaIndex);
                    var colMask = 0xFu;
                    if (sig != 0)
                    {
                        for (var columnOffset = 0; columnOffset < 8; columnOffset++, colMask <<= 4)
                        {
                            if ((sig & colMask) == 0)
                            {
                                continue;
                            }

                            var column = x + columnOffset;
                            if (column >= width)
                            {
                                break;
                            }

                            var sampleMask = 0x11111111u & colMask;
                            for (var rowOffset = 0; rowOffset < 4; rowOffset++)
                            {
                                if ((sig & sampleMask) != 0 && y + rowOffset < height)
                                {
                                    var symbol = codeword & 1u;
                                    symbol = (1u - symbol) << (bitPlane - 1);
                                    symbol |= half;
                                    var index = ((y + rowOffset) * width) + column;
                                    coefficients[index] = unchecked((int)((uint)coefficients[index] ^ symbol));
                                    codeword >>= 1;
                                }

                                sampleMask <<= 1;
                            }
                        }
                    }

                    reader.Advance(PopCount(sig));
                }
            }
        }

        private static void DecodeMelVlc(byte[] cleanupPass, int width, int height, int lcup, int scup, int sstr, ushort[] scratch)
        {
            var mel = new HtMelSegmentReader(cleanupPass, lcup, scup);
            var vlc = new HtReverseSegmentReader(cleanupPass, lcup, scup);
            var run = mel.GetRun();
            var context = 0u;
            var sp = 0;

            for (var x = 0; x < width; sp += 4)
            {
                var vlcValue = vlc.Fetch();
                var t0 = Jpeg2000HtStandardTables.VlcTable0[context + (vlcValue & 0x7F)];
                if (context == 0)
                {
                    run -= 2;
                    t0 = run == -1 ? t0 : (ushort)0;
                    if (run < 0)
                    {
                        run = mel.GetRun();
                    }
                }

                scratch[sp] = t0;
                x += 2;
                context = ((uint)(t0 & 0x10) << 3) | ((uint)(t0 & 0xE0) << 2);
                vlcValue = vlc.Advance(t0 & 0x7u);

                ushort t1 = 0;
                t1 = Jpeg2000HtStandardTables.VlcTable0[context + (vlcValue & 0x7F)];
                if (context == 0 && x < width)
                {
                    run -= 2;
                    t1 = run == -1 ? t1 : (ushort)0;
                    if (run < 0)
                    {
                        run = mel.GetRun();
                    }
                }

                t1 = x < width ? t1 : (ushort)0;
                scratch[sp + 2] = t1;
                x += 2;
                context = ((uint)(t1 & 0x10) << 3) | ((uint)(t1 & 0xE0) << 2);
                vlcValue = vlc.Advance(t1 & 0x7u);

                var uvlcMode = ((uint)(t0 & 0x8) << 3) | ((uint)(t1 & 0x8) << 4);
                if (uvlcMode == 0xC0)
                {
                    run -= 2;
                    uvlcMode += run == -1 ? 0x40u : 0u;
                    if (run < 0)
                    {
                        run = mel.GetRun();
                    }
                }

                var uvlcIndex = uvlcMode + (vlcValue & 0x3F);
                var uvlcEntry = Jpeg2000HtStandardTables.UvlcTable0[uvlcIndex];
                var uvlcBias = Jpeg2000HtStandardTables.UvlcBias[uvlcIndex];
                vlcValue = vlc.Advance(uvlcEntry & 0x7u);
                uvlcEntry >>= 3;
                var suffixLength = uvlcEntry & 0xFu;
                var suffix = vlcValue & ((1u << (int)suffixLength) - 1u);
                vlcValue = vlc.Advance(suffixLength);
                uvlcEntry >>= 4;
                var quad0SuffixLength = uvlcEntry & 0x7u;
                uvlcEntry >>= 3;
                var uQ0 = (uvlcEntry & 0x7u) + (suffix & ~(0xFFu << (int)quad0SuffixLength));
                var uQ1 = (uvlcEntry >> 3) + (suffix >> (int)quad0SuffixLength);
                if (uQ0 - (uvlcBias & 0x3u) > 32)
                {
                    uQ0 += (vlcValue & 0xFu) << 2;
                    vlcValue = vlc.Advance(4);
                }

                if (uQ1 - (uvlcBias >> 2) > 32)
                {
                    uQ1 += (vlcValue & 0xFu) << 2;
                    vlc.Advance(4);
                }

                scratch[sp + 1] = (ushort)(uQ0 + 1);
                scratch[sp + 3] = (ushort)(uQ1 + 1);
            }

            scratch[sp] = 0;
            scratch[sp + 1] = 0;

            for (var y = 2; y < height; y += 2)
            {
                context = 0;
                sp = (y >> 1) * sstr;
                for (var x = 0; x < width; sp += 4)
                {
                    context |= ((uint)(scratch[sp - sstr] & 0xA0) << 2);
                    context |= ((uint)(scratch[sp + 2 - sstr] & 0x20) << 4);

                    var vlcValue = vlc.Fetch();
                    var t0 = Jpeg2000HtStandardTables.VlcTable1[context + (vlcValue & 0x7F)];
                    if (context == 0)
                    {
                        run -= 2;
                        t0 = run == -1 ? t0 : (ushort)0;
                        if (run < 0)
                        {
                            run = mel.GetRun();
                        }
                    }

                    scratch[sp] = t0;
                    x += 2;
                    context = ((uint)(t0 & 0x40) << 2) | ((uint)(t0 & 0x80) << 1);
                    context |= (uint)(scratch[sp - sstr] & 0x80);
                    context |= ((uint)(scratch[sp + 2 - sstr] & 0xA0) << 2);
                    context |= ((uint)(scratch[sp + 4 - sstr] & 0x20) << 4);
                    vlcValue = vlc.Advance(t0 & 0x7u);

                    ushort t1 = 0;
                    t1 = Jpeg2000HtStandardTables.VlcTable1[context + (vlcValue & 0x7F)];
                    if (context == 0 && x < width)
                    {
                        run -= 2;
                        t1 = run == -1 ? t1 : (ushort)0;
                        if (run < 0)
                        {
                            run = mel.GetRun();
                        }
                    }

                    t1 = x < width ? t1 : (ushort)0;
                    scratch[sp + 2] = t1;
                    x += 2;
                    context = ((uint)(t1 & 0x40) << 2) | ((uint)(t1 & 0x80) << 1);
                    context |= (uint)(scratch[sp + 2 - sstr] & 0x80);
                    vlcValue = vlc.Advance(t1 & 0x7u);

                    var uvlcMode = ((uint)(t0 & 0x8) << 3) | ((uint)(t1 & 0x8) << 4);
                    var uvlcEntry = Jpeg2000HtStandardTables.UvlcTable1[uvlcMode + (vlcValue & 0x3F)];
                    vlcValue = vlc.Advance(uvlcEntry & 0x7u);
                    uvlcEntry >>= 3;
                    var suffixLength = uvlcEntry & 0xFu;
                    var suffix = vlcValue & ((1u << (int)suffixLength) - 1u);
                    vlc.Advance(suffixLength);
                    uvlcEntry >>= 4;
                    var quad0SuffixLength = uvlcEntry & 0x7u;
                    uvlcEntry >>= 3;
                    var uQ0 = (uvlcEntry & 0x7u) + (suffix & ~(0xFFu << (int)quad0SuffixLength));
                    var uQ1 = (uvlcEntry >> 3) + (suffix >> (int)quad0SuffixLength);
                    if (uQ0 > 32)
                    {
                        uQ0 += (vlcValue & 0xFu) << 2;
                        vlcValue = vlc.Advance(4);
                    }

                    if (uQ1 > 32)
                    {
                        uQ1 += (vlcValue & 0xFu) << 2;
                        vlc.Advance(4);
                    }

                    scratch[sp + 1] = (ushort)uQ0;
                    scratch[sp + 3] = (ushort)uQ1;
                }

                scratch[sp] = 0;
                scratch[sp + 1] = 0;
            }
        }

        private static int[] DecodeMagSgn(byte[] cleanupPass, int width, int height, int lcup, int scup, int sstr, ushort[] scratch, int bitPlane, int mmsbp2)
        {
            var reader = new HtForwardSegmentReader(cleanupPass, lcup - scup, 0xFF);
            var coefficients = new int[width * height];
            var vScratch = new uint[width + 4];
            DecodeMagSgnRow(reader, coefficients, width, height, sstr, scratch, vScratch, y: 0, bitPlane, mmsbp2, initial: true);

            for (var y = 2; y < height; y += 2)
            {
                DecodeMagSgnRow(reader, coefficients, width, height, sstr, scratch, vScratch, y, bitPlane, mmsbp2, initial: false);
            }

            return coefficients;
        }

        private static void DecodeMagSgnRow(
            HtForwardSegmentReader reader,
            int[] coefficients,
            int width,
            int height,
            int sstr,
            ushort[] scratch,
            uint[] vScratch,
            int y,
            int bitPlane,
            int mmsbp2,
            bool initial)
        {
            var sp = (y >> 1) * sstr;
            var dp = y * width;
            var prevVn = 0u;
            var vp = 0;
            for (var x = 0; x < width; sp += 2, vp++)
            {
                var inf = scratch[sp];
                var uq = scratch[sp + 1];
                var u = initial ? uq : uq + Kappa(inf, vScratch[vp], vScratch[vp + 1]);
                if (u > mmsbp2)
                {
                    throw Jpeg2000Binary.CreateException($"HTJ2K cleanup pass has an invalid U value. U={u}, max={mmsbp2}, x={x}, y={y}, inf=0x{inf:X4}, uq={uq}.");
                }

                DecodeSample(reader, coefficients, dp, inf, u, bit: 0, bitPlane, out _);
                uint vn;
                if (y + 1 < height)
                {
                    DecodeSample(reader, coefficients, dp + width, inf, u, bit: 1, bitPlane, out vn);
                }
                else
                {
                    vn = 0;
                }

                vScratch[vp] = prevVn | vn;
                prevVn = 0;
                dp++;
                if (++x >= width)
                {
                    vp++;
                    vScratch[vp] = 0;
                    break;
                }

                DecodeSample(reader, coefficients, dp, inf, u, bit: 2, bitPlane, out _);
                if (y + 1 < height)
                {
                    DecodeSample(reader, coefficients, dp + width, inf, u, bit: 3, bitPlane, out prevVn);
                }
                else
                {
                    prevVn = 0;
                }

                dp++;
                x++;
            }

            vScratch[Math.Min(vp, vScratch.Length - 1)] = prevVn;
        }

        private static int Kappa(int inf, uint currentVn, uint nextVn)
        {
            var gamma = inf & 0xF0;
            gamma &= gamma - 0x10;
            if (gamma == 0)
            {
                return 1;
            }

            var emax = 31 - CountLeadingZeros((currentVn | nextVn) | 2u);
            return emax;
        }

        private static void DecodeSample(HtForwardSegmentReader reader, int[] coefficients, int index, int inf, int uq, int bit, int bitPlane, out uint vn)
        {
            if ((inf & (1 << (4 + bit))) == 0)
            {
                coefficients[index] = 0;
                vn = 0;
                return;
            }

            var magSgn = reader.Fetch();
            var mn = uq - ((inf >> (12 + bit)) & 1);
            reader.Advance(mn);
            var sign = (magSgn & 1u) != 0;
            vn = magSgn & MaskLowBits(mn);
            vn |= (uint)((inf >> (8 + bit)) & 1) << mn;
            vn |= 1u;
            var magnitude = (vn + 2u) << (bitPlane - 1);
            coefficients[index] = unchecked((int)(sign ? 0x80000000u | magnitude : magnitude));
        }

        private static uint MaskLowBits(int bitCount)
        {
            return bitCount >= 32 ? uint.MaxValue : (1u << bitCount) - 1u;
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

        private static uint ReadTwoUShorts(ushort[] data, int index)
        {
            var low = index >= 0 && index < data.Length ? data[index] : 0;
            var high = index + 1 >= 0 && index + 1 < data.Length ? data[index + 1] : 0;
            return (uint)(low | (high << 16));
        }

        private static void WriteLowUShort(ushort[] data, int index, uint value)
        {
            if (index >= 0 && index < data.Length)
            {
                data[index] = (ushort)value;
            }
        }

        private static int PopCount(uint value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        private sealed class HtMelSegmentReader
        {
            private static readonly int[] MelExponents = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5 };

            private readonly byte[] _data;
            private int _offset;
            private ulong _tmp;
            private int _bits;
            private int _size;
            private bool _unstuff;
            private int _k;
            private int _numRuns;
            private ulong _runs;

            public HtMelSegmentReader(byte[] data, int lcup, int scup)
            {
                _data = data;
                _offset = lcup - scup;
                _size = scup - 1;
                var num = Math.Min(4, _size + 1);
                for (var i = 0; i < num; i++)
                {
                    var d = _size > 0 ? _data[_offset] : 0xFF;
                    if (_size == 1)
                    {
                        d |= 0x0F;
                    }

                    if (_size > 0)
                    {
                        _offset++;
                        _size--;
                    }

                    var dBits = 8 - (_unstuff ? 1 : 0);
                    _tmp = (_tmp << dBits) | (uint)d;
                    _bits += dBits;
                    _unstuff = (d & 0xFF) == 0xFF;
                }

                _tmp <<= 64 - _bits;
            }

            public int GetRun()
            {
                if (_numRuns == 0)
                {
                    Decode();
                }

                var run = (int)(_runs & 0x7F);
                _runs >>= 7;
                _numRuns--;
                return run;
            }

            private void Decode()
            {
                if (_bits < 6)
                {
                    Read();
                }

                while (_bits >= 6 && _numRuns < 8)
                {
                    var exponent = MelExponents[_k];
                    int run;
                    if ((_tmp & (1ul << 63)) != 0)
                    {
                        run = (1 << exponent) - 1;
                        _k = Math.Min(12, _k + 1);
                        _tmp <<= 1;
                        _bits--;
                        run <<= 1;
                    }
                    else
                    {
                        run = (int)((_tmp >> (63 - exponent)) & (uint)((1 << exponent) - 1));
                        _k = Math.Max(0, _k - 1);
                        _tmp <<= exponent + 1;
                        _bits -= exponent + 1;
                        run = (run << 1) + 1;
                    }

                    var shift = _numRuns * 7;
                    _runs &= ~(0x3Ful << shift);
                    _runs |= (ulong)run << shift;
                    _numRuns++;
                }
            }

            private void Read()
            {
                if (_bits > 32)
                {
                    return;
                }

                uint value = 0xFFFFFFFF;
                if (_size > 4)
                {
                    value = ReadLittleEndian32(_data, _offset);
                    _offset += 4;
                    _size -= 4;
                }
                else if (_size > 0)
                {
                    var shift = 0;
                    while (_size > 1)
                    {
                        value = (value & ~(0xFFu << shift)) | ((uint)_data[_offset++] << shift);
                        _size--;
                        shift += 8;
                    }

                    var v = (uint)(_data[_offset++] | 0x0F);
                    value = (value & ~(0xFFu << shift)) | (v << shift);
                    _size--;
                }

                var bits = 32 - (_unstuff ? 1 : 0);
                var t = value & 0xFF;
                var unstuff = (value & 0xFF) == 0xFF;
                bits -= unstuff ? 1 : 0;
                t <<= 8 - (unstuff ? 1 : 0);
                t |= (value >> 8) & 0xFF;
                unstuff = ((value >> 8) & 0xFF) == 0xFF;
                bits -= unstuff ? 1 : 0;
                t <<= 8 - (unstuff ? 1 : 0);
                t |= (value >> 16) & 0xFF;
                unstuff = ((value >> 16) & 0xFF) == 0xFF;
                bits -= unstuff ? 1 : 0;
                t <<= 8 - (unstuff ? 1 : 0);
                t |= (value >> 24) & 0xFF;
                _unstuff = ((value >> 24) & 0xFF) == 0xFF;

                _tmp |= (ulong)t << (64 - bits - _bits);
                _bits += bits;
            }
        }

        private sealed class HtReverseSegmentReader
        {
            private readonly byte[] _data;
            private int _offset;
            private ulong _tmp;
            private int _bits;
            private int _size;
            private bool _unstuff;

            public HtReverseSegmentReader(byte[] data, int lcup, int scup)
            {
                _data = data;
                _offset = lcup - 2;
                _size = scup - 2;
                var d = _data[_offset--];
                _tmp = (uint)d >> 4;
                _bits = 4 - (((_tmp & 7) == 7) ? 1 : 0);
                _unstuff = (d | 0x0F) > 0x8F;
                Read();
            }

            public uint Fetch()
            {
                if (_bits < 32)
                {
                    Read();
                    if (_bits < 32)
                    {
                        Read();
                    }
                }

                return (uint)_tmp;
            }

            public uint Advance(uint bits)
            {
                _tmp >>= (int)bits;
                _bits -= (int)bits;
                return (uint)_tmp;
            }

            private void Read()
            {
                if (_bits > 32)
                {
                    return;
                }

                uint value = 0;
                if (_size > 3)
                {
                    value = ReadLittleEndian32(_data, _offset - 3);
                    _offset -= 4;
                    _size -= 4;
                }
                else if (_size > 0)
                {
                    var shift = 24;
                    while (_size > 0)
                    {
                        value |= (uint)_data[_offset--] << shift;
                        _size--;
                        shift -= 8;
                    }
                }

                var tmp = value >> 24;
                var bits = 8 - ((_unstuff && ((value >> 24) & 0x7F) == 0x7F) ? 1 : 0);
                var unstuff = (value >> 24) > 0x8F;
                tmp |= ((value >> 16) & 0xFF) << bits;
                bits += 8 - ((unstuff && ((value >> 16) & 0x7F) == 0x7F) ? 1 : 0);
                unstuff = ((value >> 16) & 0xFF) > 0x8F;
                tmp |= ((value >> 8) & 0xFF) << bits;
                bits += 8 - ((unstuff && ((value >> 8) & 0x7F) == 0x7F) ? 1 : 0);
                unstuff = ((value >> 8) & 0xFF) > 0x8F;
                tmp |= (value & 0xFF) << bits;
                bits += 8 - ((unstuff && (value & 0x7F) == 0x7F) ? 1 : 0);
                _unstuff = (value & 0xFF) > 0x8F;
                _tmp |= (ulong)tmp << _bits;
                _bits += bits;
            }
        }

        private sealed class HtForwardSegmentReader
        {
            private readonly byte[] _data;
            private int _offset;
            private readonly int _size;
            private readonly byte _unstuffLimit;
            private ulong _tmp;
            private int _bits;
            private int _lastByte = -1;

            public HtForwardSegmentReader(byte[] data, int size, byte unstuffLimit)
            {
                _data = data;
                _size = size;
                _unstuffLimit = unstuffLimit;
                for (var i = 0; i < 4; i++)
                {
                    ReadByte();
                }

                ReadWord();
            }

            public uint Fetch()
            {
                while (_bits < 32)
                {
                    ReadWord();
                }

                return (uint)_tmp;
            }

            public void Advance(int bits)
            {
                _tmp >>= bits;
                _bits -= bits;
            }

            private void ReadByte()
            {
                var value = _offset < _size ? _data[_offset++] : _unstuffLimit;
                var bits = _lastByte == _unstuffLimit ? 7 : 8;
                _tmp |= (ulong)(value & ((1 << bits) - 1)) << _bits;
                _bits += bits;
                _lastByte = value;
            }

            private void ReadWord()
            {
                if (_bits > 32)
                {
                    return;
                }

                for (var i = 0; i < 4; i++)
                {
                    ReadByte();
                }
            }
        }

        private sealed class HtReverseMrpReader
        {
            private readonly byte[] _data;
            private int _offset;
            private int _size;
            private ulong _tmp;
            private int _bits;
            private bool _unstuff;

            public HtReverseMrpReader(byte[] data)
            {
                _data = data ?? Array.Empty<byte>();
                _offset = _data.Length - 1;
                _size = _data.Length;
                _unstuff = true;
                var num = Math.Min(4, _size);
                for (var i = 0; i < num; i++)
                {
                    var d = _size-- > 0 ? _data[_offset--] : 0;
                    var bits = 8 - ((_unstuff && (d & 0x7F) == 0x7F) ? 1 : 0);
                    _tmp |= (ulong)d << _bits;
                    _bits += bits;
                    _unstuff = d > 0x8F;
                }

                Read();
            }

            public uint Fetch()
            {
                if (_bits < 32)
                {
                    Read();
                    if (_bits < 32)
                    {
                        Read();
                    }
                }

                return (uint)_tmp;
            }

            public void Advance(int bits)
            {
                _tmp >>= bits;
                _bits -= bits;
            }

            private void Read()
            {
                if (_bits > 32)
                {
                    return;
                }

                uint value = 0;
                if (_size > 3)
                {
                    value = ReadLittleEndian32(_data, _offset - 3);
                    _offset -= 4;
                    _size -= 4;
                }
                else if (_size > 0)
                {
                    var shift = 24;
                    while (_size > 0)
                    {
                        value |= (uint)_data[_offset--] << shift;
                        _size--;
                        shift -= 8;
                    }
                }

                var tmp = value >> 24;
                var bits = 8 - ((_unstuff && ((value >> 24) & 0x7F) == 0x7F) ? 1 : 0);
                var unstuff = (value >> 24) > 0x8F;
                tmp |= ((value >> 16) & 0xFF) << bits;
                bits += 8 - ((unstuff && ((value >> 16) & 0x7F) == 0x7F) ? 1 : 0);
                unstuff = ((value >> 16) & 0xFF) > 0x8F;
                tmp |= ((value >> 8) & 0xFF) << bits;
                bits += 8 - ((unstuff && ((value >> 8) & 0x7F) == 0x7F) ? 1 : 0);
                unstuff = ((value >> 8) & 0xFF) > 0x8F;
                tmp |= (value & 0xFF) << bits;
                bits += 8 - ((unstuff && (value & 0x7F) == 0x7F) ? 1 : 0);
                _unstuff = (value & 0xFF) > 0x8F;
                _tmp |= (ulong)tmp << _bits;
                _bits += bits;
            }
        }

        private static uint ReadLittleEndian32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

    }
}
