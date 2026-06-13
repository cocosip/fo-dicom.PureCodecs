namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000HtStandardTables
    {
        public static readonly ushort[] VlcTable0 = BuildVlcTable0();

        public static readonly ushort[] UvlcTable0 = BuildUvlcTable0();

        private static ushort[] BuildVlcTable0()
        {
            var table = new ushort[1024];
            Add(table, context: 0, rho: 0x1, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x06, codewordLength: 4);
            Add(table, context: 0, rho: 0x1, uOff: 1, eK: 0x1, e1: 0x1, codeword: 0x3F, codewordLength: 7);
            Add(table, context: 0, rho: 0x2, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x00, codewordLength: 3);
            Add(table, context: 0, rho: 0x2, uOff: 1, eK: 0x2, e1: 0x2, codeword: 0x7F, codewordLength: 7);
            Add(table, context: 0, rho: 0x3, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x11, codewordLength: 5);
            Add(table, context: 0, rho: 0x3, uOff: 1, eK: 0x2, e1: 0x2, codeword: 0x5F, codewordLength: 7);
            Add(table, context: 0, rho: 0x3, uOff: 1, eK: 0x3, e1: 0x1, codeword: 0x1F, codewordLength: 7);
            Add(table, context: 0, rho: 0x4, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x02, codewordLength: 3);
            Add(table, context: 0, rho: 0x5, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x0E, codewordLength: 5);
            Add(table, context: 0, rho: 0x8, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x04, codewordLength: 3);
            Add(table, context: 0, rho: 0x9, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x1D, codewordLength: 6);
            Add(table, context: 0, rho: 0xA, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x01, codewordLength: 5);
            Add(table, context: 1, rho: 0x1, uOff: 0, eK: 0x0, e1: 0x0, codeword: 0x0E, codewordLength: 4);
            return table;
        }

        private static void Add(ushort[] table, int context, int rho, int uOff, int eK, int e1, int codeword, int codewordLength)
        {
            for (var prefix = 0; prefix < 128; prefix++)
            {
                if ((prefix & ((1 << codewordLength) - 1)) == codeword)
                {
                    table[(context << 7) | prefix] = (ushort)((rho << 4) | (uOff << 3) | (eK << 12) | (e1 << 8) | codewordLength);
                }
            }
        }

        private static ushort[] BuildUvlcTable0()
        {
            var table = new ushort[320];
            var dec = new[]
            {
                3 | (5 << 2) | (5 << 5),
                1 | (0 << 2) | (1 << 5),
                2 | (0 << 2) | (2 << 5),
                1 | (0 << 2) | (1 << 5),
                3 | (1 << 2) | (3 << 5),
                1 | (0 << 2) | (1 << 5),
                2 | (0 << 2) | (2 << 5),
                1 | (0 << 2) | (1 << 5)
            };

            for (var i = 0; i < table.Length; i++)
            {
                var mode = i >> 6;
                var vlc = i & 0x3F;
                if (mode == 0)
                {
                    table[i] = 0;
                }
                else if (mode <= 2)
                {
                    var d = dec[vlc & 0x7];
                    var totalPrefix = d & 0x3;
                    var totalSuffix = (d >> 2) & 0x7;
                    var u0SuffixLength = mode == 1 ? totalSuffix : 0;
                    var u0 = mode == 1 ? d >> 5 : 0;
                    var u1 = mode == 1 ? 0 : d >> 5;
                    table[i] = (ushort)(totalPrefix | (totalSuffix << 3) | (u0SuffixLength << 7) | (u0 << 10) | (u1 << 13));
                }
                else if (mode == 3)
                {
                    var d0 = dec[vlc & 0x7];
                    vlc >>= d0 & 0x3;
                    var d1 = dec[vlc & 0x7];
                    int totalPrefix;
                    int u0SuffixLength;
                    int totalSuffix;
                    int u0;
                    int u1;
                    if ((d0 & 0x3) == 3)
                    {
                        totalPrefix = (d0 & 0x3) + 1;
                        u0SuffixLength = (d0 >> 2) & 0x7;
                        totalSuffix = u0SuffixLength;
                        u0 = d0 >> 5;
                        u1 = (vlc & 1) + 1;
                    }
                    else
                    {
                        totalPrefix = (d0 & 0x3) + (d1 & 0x3);
                        u0SuffixLength = (d0 >> 2) & 0x7;
                        totalSuffix = u0SuffixLength + ((d1 >> 2) & 0x7);
                        u0 = d0 >> 5;
                        u1 = d1 >> 5;
                    }

                    table[i] = (ushort)(totalPrefix | (totalSuffix << 3) | (u0SuffixLength << 7) | (u0 << 10) | (u1 << 13));
                }
                else if (mode == 4)
                {
                    var d0 = dec[vlc & 0x7];
                    vlc >>= d0 & 0x3;
                    var d1 = dec[vlc & 0x7];
                    var totalPrefix = (d0 & 0x3) + (d1 & 0x3);
                    var u0SuffixLength = (d0 >> 2) & 0x7;
                    var totalSuffix = u0SuffixLength + ((d1 >> 2) & 0x7);
                    var u0 = (d0 >> 5) + 2;
                    var u1 = (d1 >> 5) + 2;
                    table[i] = (ushort)(totalPrefix | (totalSuffix << 3) | (u0SuffixLength << 7) | (u0 << 10) | (u1 << 13));
                }
            }

            return table;
        }
    }
}
