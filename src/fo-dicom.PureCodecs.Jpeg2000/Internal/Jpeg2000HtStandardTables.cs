namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000HtStandardTables
    {
        public static readonly ushort[] VlcTable0 = BuildVlcDecodeTable(Jpeg2000HtStandardEncodeTables.VlcTable0Source);

        public static readonly ushort[] UvlcTable0 = BuildUvlcTable0();

        public static readonly byte[] UvlcBias = BuildUvlcBias();

        public static readonly ushort[] VlcTable1 = BuildVlcDecodeTable(Jpeg2000HtStandardEncodeTables.VlcTable1Source);

        public static readonly ushort[] UvlcTable1 = BuildUvlcTable1();

        private static ushort[] BuildVlcDecodeTable(int[] source)
        {
            var table = new ushort[1024];
            for (var i = 0; i < table.Length; i++)
            {
                var codeword = i & 0x7F;
                var context = i >> 7;
                for (var offset = 0; offset < source.Length; offset += 7)
                {
                    if (source[offset] == context &&
                        source[offset + 5] == (codeword & ((1 << source[offset + 6]) - 1)))
                    {
                        table[i] = (ushort)((source[offset + 1] << 4) |
                            (source[offset + 2] << 3) |
                            (source[offset + 3] << 12) |
                            (source[offset + 4] << 8) |
                            source[offset + 6]);
                    }
                }
            }

            return table;
        }

        private static ushort[] BuildUvlcTable0()
        {
            var table = new ushort[320];
            var dec = UvlcDecodePrefixes;
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

        private static byte[] BuildUvlcBias()
        {
            var table = new byte[320];
            var dec = UvlcDecodePrefixes;
            for (var i = 0; i < table.Length; i++)
            {
                var mode = i >> 6;
                var vlc = i & 0x3F;
                if (mode == 3)
                {
                    var d0 = dec[vlc & 0x7];
                    vlc >>= d0 & 0x3;
                    table[i] = (d0 & 0x3) == 3 ? (byte)4 : (byte)0;
                }
                else if (mode == 4)
                {
                    table[i] = 10;
                }
            }

            return table;
        }

        private static ushort[] BuildUvlcTable1()
        {
            var table = new ushort[256];
            var dec = UvlcDecodePrefixes;
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
                    var totalPrefix = (d0 & 0x3) + (d1 & 0x3);
                    var u0SuffixLength = (d0 >> 2) & 0x7;
                    var totalSuffix = u0SuffixLength + ((d1 >> 2) & 0x7);
                    var u0 = d0 >> 5;
                    var u1 = d1 >> 5;
                    table[i] = (ushort)(totalPrefix | (totalSuffix << 3) | (u0SuffixLength << 7) | (u0 << 10) | (u1 << 13));
                }
            }

            return table;
        }

        private static int[] UvlcDecodePrefixes
        {
            get
            {
                return new[]
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
            }
        }
    }
}
