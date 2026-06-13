using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000HtStandardCleanupPassDecoder
    {
        public static Jpeg2000ClassicCodeBlock Decode(byte[] cleanupPass, int width, int height, int missingMostSignificantBits)
        {
            if (cleanupPass == null)
            {
                throw new ArgumentNullException(nameof(cleanupPass));
            }

            if (width <= 0 || height <= 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass dimensions are invalid.");
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
            return new Jpeg2000ClassicCodeBlock(width, height, coefficients);
        }

        private static void DecodeMelVlc(byte[] cleanupPass, int width, int height, int lcup, int scup, int sstr, ushort[] scratch)
        {
            var mel = new OpenJphMelReader(cleanupPass, lcup, scup);
            var vlc = new OpenJphReverseReader(cleanupPass, lcup, scup);
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

                var uvlcEntry = Jpeg2000HtStandardTables.UvlcTable0[uvlcMode + (vlcValue & 0x3F)];
                vlcValue = vlc.Advance(uvlcEntry & 0x7u);
                uvlcEntry >>= 3;
                var suffixLength = uvlcEntry & 0xFu;
                var suffix = vlcValue & ((1u << (int)suffixLength) - 1u);
                vlc.Advance(suffixLength);
                uvlcEntry >>= 4;
                var quad0SuffixLength = uvlcEntry & 0x7u;
                uvlcEntry >>= 3;
                scratch[sp + 1] = (ushort)(1 + (uvlcEntry & 0x7u) + (suffix & ~(0xFFu << (int)quad0SuffixLength)));
                scratch[sp + 3] = (ushort)(1 + (uvlcEntry >> 3) + (suffix >> (int)quad0SuffixLength));
            }

            scratch[sp] = 0;
            scratch[sp + 1] = 0;

            if (height > 2)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass non-initial quad rows are not implemented for standard vectors.");
            }
        }

        private static int[] DecodeMagSgn(byte[] cleanupPass, int width, int height, int lcup, int scup, int sstr, ushort[] scratch, int bitPlane, int mmsbp2)
        {
            var reader = new OpenJphForwardReader(cleanupPass, lcup - scup, 0xFF);
            var coefficients = new int[width * height];
            var sp = 0;
            var dp = 0;

            for (var x = 0; x < width; sp += 2)
            {
                var inf = scratch[sp];
                var uq = scratch[sp + 1];
                if (uq > mmsbp2)
                {
                    throw Jpeg2000Binary.CreateException("HTJ2K cleanup pass has an invalid U value.");
                }

                DecodeSample(reader, coefficients, dp, inf, uq, bit: 0, bitPlane);
                if (height > 1)
                {
                    DecodeSample(reader, coefficients, dp + width, inf, uq, bit: 1, bitPlane);
                }

                dp++;
                if (++x >= width)
                {
                    break;
                }

                DecodeSample(reader, coefficients, dp, inf, uq, bit: 2, bitPlane);
                if (height > 1)
                {
                    DecodeSample(reader, coefficients, dp + width, inf, uq, bit: 3, bitPlane);
                }

                dp++;
                x++;
            }

            return coefficients;
        }

        private static void DecodeSample(OpenJphForwardReader reader, int[] coefficients, int index, int inf, int uq, int bit, int bitPlane)
        {
            if ((inf & (1 << (4 + bit))) == 0)
            {
                coefficients[index] = 0;
                return;
            }

            var magSgn = reader.Fetch();
            var mn = uq - ((inf >> (12 + bit)) & 1);
            reader.Advance(mn);
            var sign = (magSgn & 1u) != 0;
            var vn = magSgn & ((1u << mn) - 1u);
            vn |= (uint)((inf >> (8 + bit)) & 1) << mn;
            vn |= 1u;
            var magnitude = (int)((vn + 2u) << (bitPlane - 1));
            coefficients[index] = sign ? -magnitude : magnitude;
        }

        private sealed class OpenJphMelReader
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

            public OpenJphMelReader(byte[] data, int lcup, int scup)
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

        private sealed class OpenJphReverseReader
        {
            private readonly byte[] _data;
            private int _offset;
            private ulong _tmp;
            private int _bits;
            private int _size;
            private bool _unstuff;

            public OpenJphReverseReader(byte[] data, int lcup, int scup)
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
                    value = ReadBigEndianBackward32(_data, _offset - 3);
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

        private sealed class OpenJphForwardReader
        {
            private readonly byte[] _data;
            private int _offset;
            private readonly int _size;
            private readonly byte _unstuffLimit;
            private uint _tmp;
            private int _bits;
            private int _lastByte = -1;

            public OpenJphForwardReader(byte[] data, int size, byte unstuffLimit)
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

                return _tmp;
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
                _tmp |= (uint)(value & ((1 << bits) - 1)) << _bits;
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

        private static uint ReadLittleEndian32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static uint ReadBigEndianBackward32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
