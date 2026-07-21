using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegQuantizationTable
    {
        private readonly int[] _divisors;

        public JpegQuantizationTable(int[] divisors)
        {
            if (divisors == null)
            {
                throw new ArgumentNullException(nameof(divisors));
            }

            if (divisors.Length != JpegBlock8x8.CoefficientCount)
            {
                throw JpegMarkerReader.CreateException("JPEG quantization table requires 64 entries.");
            }

            _divisors = new int[divisors.Length];
            for (var index = 0; index < divisors.Length; index++)
            {
                if (divisors[index] <= 0)
                {
                    throw JpegMarkerReader.CreateException("JPEG quantization table entries must be positive.");
                }

                _divisors[index] = divisors[index];
            }
        }

        public JpegBlock8x8 Quantize(JpegBlock8x8 block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var quantized = new JpegBlock8x8();
            for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
            {
                quantized[index] = Math.Round(block[index] / _divisors[index], MidpointRounding.AwayFromZero);
            }

            return quantized;
        }

        public JpegBlock8x8 QuantizeNativeIntegerDct(JpegBlock8x8 block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var quantized = new JpegBlock8x8();
            QuantizeNativeIntegerDctInto(block, quantized);
            return quantized;
        }

        internal void QuantizeNativeIntegerDctInto(JpegBlock8x8 block, JpegBlock8x8 quantized)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (quantized == null)
            {
                throw new ArgumentNullException(nameof(quantized));
            }

            for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
            {
                var coefficient = checked((long)block[index]);
                var divisor = checked((long)_divisors[index] << 3);
                var absolute = coefficient < 0 ? -coefficient : coefficient;
                absolute += divisor >> 1;
                var value = absolute >= divisor ? absolute / divisor : 0;
                quantized[index] = coefficient < 0 ? -value : value;
            }
        }

        public JpegBlock8x8 Dequantize(JpegBlock8x8 block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var dequantized = new JpegBlock8x8();
            for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
            {
                dequantized[index] = block[index];
            }

            DequantizeInPlace(dequantized);
            return dequantized;
        }

        internal void DequantizeInPlace(JpegBlock8x8 block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            for (var index = 0; index < JpegBlock8x8.CoefficientCount; index++)
            {
                block[index] = block[index] * _divisors[index];
            }
        }

        public JpegBlock8x8 ToBlock()
        {
            var block = new JpegBlock8x8();
            for (var index = 0; index < _divisors.Length; index++)
            {
                block[index] = _divisors[index];
            }

            return block;
        }
    }
}
