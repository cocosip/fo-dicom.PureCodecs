using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    internal static class JpegNativeIntegerDct
    {
        private const int ConstBits = 13;
        private const int Fix_0_298631336 = 2446;
        private const int Fix_0_390180644 = 3196;
        private const int Fix_0_541196100 = 4433;
        private const int Fix_0_765366865 = 6270;
        private const int Fix_0_899976223 = 7373;
        private const int Fix_1_175875602 = 9633;
        private const int Fix_1_501321110 = 12299;
        private const int Fix_1_847759065 = 15137;
        private const int Fix_1_961570560 = 16069;
        private const int Fix_2_053119869 = 16819;
        private const int Fix_2_562915447 = 20995;
        private const int Fix_3_072711026 = 25172;

        public static JpegBlock8x8 Forward(JpegBlock8x8 samples, int samplePrecision)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samplePrecision < 2 || samplePrecision > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(samplePrecision));
            }

            var pass1Bits = samplePrecision == 8 ? 2 : 1;
            var data = new long[JpegBlock8x8.CoefficientCount];
            for (var index = 0; index < data.Length; index++)
            {
                data[index] = checked((long)samples[index]);
            }

            for (var row = 0; row < JpegBlock8x8.Size; row++)
            {
                TransformRow(data, row * JpegBlock8x8.Size, pass1Bits);
            }

            for (var column = 0; column < JpegBlock8x8.Size; column++)
            {
                TransformColumn(data, column, pass1Bits);
            }

            var coefficients = new JpegBlock8x8();
            for (var index = 0; index < data.Length; index++)
            {
                coefficients[index] = data[index];
            }

            return coefficients;
        }

        private static void TransformRow(long[] data, int offset, int pass1Bits)
        {
            Calculate(data[offset], data[offset + 1], data[offset + 2], data[offset + 3], data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7], pass1Bits, out var output);

            data[offset] = output[0] << pass1Bits;
            data[offset + 1] = Descale(output[1], ConstBits - pass1Bits);
            data[offset + 2] = Descale(output[2], ConstBits - pass1Bits);
            data[offset + 3] = Descale(output[3], ConstBits - pass1Bits);
            data[offset + 4] = output[4] << pass1Bits;
            data[offset + 5] = Descale(output[5], ConstBits - pass1Bits);
            data[offset + 6] = Descale(output[6], ConstBits - pass1Bits);
            data[offset + 7] = Descale(output[7], ConstBits - pass1Bits);
        }

        private static void TransformColumn(long[] data, int offset, int pass1Bits)
        {
            const int stride = JpegBlock8x8.Size;
            Calculate(data[offset], data[offset + stride], data[offset + stride * 2], data[offset + stride * 3], data[offset + stride * 4], data[offset + stride * 5], data[offset + stride * 6], data[offset + stride * 7], pass1Bits, out var output);

            data[offset] = Descale(output[0], pass1Bits);
            data[offset + stride] = Descale(output[1], ConstBits + pass1Bits);
            data[offset + stride * 2] = Descale(output[2], ConstBits + pass1Bits);
            data[offset + stride * 3] = Descale(output[3], ConstBits + pass1Bits);
            data[offset + stride * 4] = Descale(output[4], pass1Bits);
            data[offset + stride * 5] = Descale(output[5], ConstBits + pass1Bits);
            data[offset + stride * 6] = Descale(output[6], ConstBits + pass1Bits);
            data[offset + stride * 7] = Descale(output[7], ConstBits + pass1Bits);
        }

        private static void Calculate(long input0, long input1, long input2, long input3, long input4, long input5, long input6, long input7, int pass1Bits, out long[] output)
        {
            var tmp0 = input0 + input7;
            var tmp7 = input0 - input7;
            var tmp1 = input1 + input6;
            var tmp6 = input1 - input6;
            var tmp2 = input2 + input5;
            var tmp5 = input2 - input5;
            var tmp3 = input3 + input4;
            var tmp4 = input3 - input4;

            var tmp10 = tmp0 + tmp3;
            var tmp13 = tmp0 - tmp3;
            var tmp11 = tmp1 + tmp2;
            var tmp12 = tmp1 - tmp2;

            output = new long[8];
            output[0] = tmp10 + tmp11;
            output[4] = tmp10 - tmp11;

            var z1 = (tmp12 + tmp13) * Fix_0_541196100;
            output[2] = z1 + tmp13 * Fix_0_765366865;
            output[6] = z1 - tmp12 * Fix_1_847759065;

            z1 = tmp4 + tmp7;
            var z2 = tmp5 + tmp6;
            var z3 = tmp4 + tmp6;
            var z4 = tmp5 + tmp7;
            var z5 = (z3 + z4) * Fix_1_175875602;

            tmp4 *= Fix_0_298631336;
            tmp5 *= Fix_2_053119869;
            tmp6 *= Fix_3_072711026;
            tmp7 *= Fix_1_501321110;
            z1 *= -Fix_0_899976223;
            z2 *= -Fix_2_562915447;
            z3 *= -Fix_1_961570560;
            z4 *= -Fix_0_390180644;
            z3 += z5;
            z4 += z5;

            output[7] = tmp4 + z1 + z3;
            output[5] = tmp5 + z2 + z4;
            output[3] = tmp6 + z2 + z3;
            output[1] = tmp7 + z1 + z4;
        }

        private static long Descale(long value, int bitCount)
        {
            return (value + (1L << (bitCount - 1))) >> bitCount;
        }
    }
}
