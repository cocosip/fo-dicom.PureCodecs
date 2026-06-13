namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardMqDecoder
    {
        private static readonly uint[] Qe =
        {
            0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401,
            0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601, 0x5601, 0x5401,
            0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201,
            0x1C01, 0x1801, 0x1601, 0x1401, 0x1201, 0x1101, 0x0AC1, 0x09C1,
            0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085,
            0x0049, 0x0025, 0x0015, 0x0009, 0x0005, 0x0001, 0x5601
        };

        private static readonly byte[] Nmps =
        {
            1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
            33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46
        };

        private static readonly byte[] Nlps =
        {
            1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14,
            15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
            30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46
        };

        private static readonly byte[] Switch =
        {
            1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        private readonly byte[] _data;
        private readonly byte[] _contexts;
        private int _position;
        private uint _a;
        private uint _c;
        private int _ct;

        public Jpeg2000StandardMqDecoder(byte[] data, int contextCount)
        {
            _data = new byte[(data?.Length ?? 0) + 2];
            if (data != null)
            {
                System.Buffer.BlockCopy(data, 0, _data, 0, data.Length);
            }

            _data[_data.Length - 2] = 0xFF;
            _data[_data.Length - 1] = 0xFF;
            _contexts = new byte[contextCount];
            _a = 0x8000;
            _c = (uint)_data[0] << 16;
            ByteIn();
            _c <<= 7;
            _ct -= 7;
            _a = 0x8000;
        }

        public void SetContextState(int context, byte state)
        {
            _contexts[context] = state;
        }

        public int Decode(int context)
        {
            var cx = _contexts[context];
            var state = cx & 0x7F;
            var mps = cx >> 7;
            var qe = Qe[state];
            _a -= qe;

            int bit;
            if ((_c >> 16) < qe)
            {
                if (_a < qe)
                {
                    _a = qe;
                    bit = mps;
                    _contexts[context] = (byte)(Nmps[state] | (mps << 7));
                }
                else
                {
                    _a = qe;
                    bit = 1 - mps;
                    var nextMps = Switch[state] != 0 ? 1 - mps : mps;
                    _contexts[context] = (byte)(Nlps[state] | (nextMps << 7));
                }

                Renorm();
                return bit;
            }

            _c -= qe << 16;
            if ((_a & 0x8000) != 0)
            {
                return mps;
            }

            if (_a < qe)
            {
                bit = 1 - mps;
                var nextMps = Switch[state] != 0 ? 1 - mps : mps;
                _contexts[context] = (byte)(Nlps[state] | (nextMps << 7));
            }
            else
            {
                bit = mps;
                _contexts[context] = (byte)(Nmps[state] | (mps << 7));
            }

            Renorm();
            return bit;
        }

        private void Renorm()
        {
            while (_a < 0x8000)
            {
                if (_ct == 0)
                {
                    ByteIn();
                }

                _a <<= 1;
                _c <<= 1;
                _ct--;
            }
        }

        private void ByteIn()
        {
            var next = _data[_position + 1];
            if (_data[_position] == 0xFF)
            {
                if (next > 0x8F)
                {
                    _c += 0xFF00;
                    _ct = 8;
                }
                else
                {
                    _position++;
                    _c += (uint)next << 9;
                    _ct = 7;
                }
            }
            else
            {
                _position++;
                _c += (uint)next << 8;
                _ct = 8;
            }
        }
    }
}
