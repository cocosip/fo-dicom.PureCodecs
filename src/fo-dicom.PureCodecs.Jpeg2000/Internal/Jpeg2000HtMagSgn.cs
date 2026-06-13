using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000HtMagSgnEncoder
    {
        public static byte[] Encode(IReadOnlyList<int> coefficients)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }

            var writer = new Jpeg2000BitWriter();
            for (var i = 0; i < coefficients.Count; i++)
            {
                var value = coefficients[i];
                if (value == 0)
                {
                    writer.WriteBit(false);
                    continue;
                }

                var magnitude = Math.Abs(value);
                writer.WriteBit(true);
                writer.WriteBits((uint)magnitude, 31);
                writer.WriteBit(value < 0);
            }

            return writer.ToArray();
        }
    }

    public static class Jpeg2000HtMagSgnDecoder
    {
        public static int[] Decode(byte[] bytes, int coefficientCount)
        {
            if (coefficientCount < 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K MagSgn coefficient count is invalid.");
            }

            var reader = new Jpeg2000BitReader(bytes);
            var coefficients = new int[coefficientCount];
            for (var i = 0; i < coefficients.Length; i++)
            {
                if (!reader.ReadBit())
                {
                    coefficients[i] = 0;
                    continue;
                }

                var magnitude = (int)reader.ReadBits(31);
                var negative = reader.ReadBit();
                coefficients[i] = negative ? -magnitude : magnitude;
            }

            return coefficients;
        }
    }
}
