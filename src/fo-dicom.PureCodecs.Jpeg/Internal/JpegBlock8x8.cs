using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegBlock8x8
    {
        public const int Size = 8;
        public const int CoefficientCount = Size * Size;

        private readonly double[] _coefficients;

        public JpegBlock8x8()
            : this(new double[CoefficientCount])
        {
        }

        private JpegBlock8x8(double[] coefficients)
        {
            _coefficients = coefficients;
        }

        public double this[int index]
        {
            get
            {
                ValidateIndex(index);
                return _coefficients[index];
            }
            set
            {
                ValidateIndex(index);
                _coefficients[index] = value;
            }
        }

        public double this[int y, int x]
        {
            get
            {
                return this[ToIndex(y, x)];
            }
            set
            {
                this[ToIndex(y, x)] = value;
            }
        }

        public double[] ToArray()
        {
            var copy = new double[CoefficientCount];
            Array.Copy(_coefficients, copy, copy.Length);
            return copy;
        }

        public static JpegBlock8x8 FromArray(double[] coefficients)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }

            if (coefficients.Length != CoefficientCount)
            {
                throw JpegMarkerReader.CreateException($"JPEG 8x8 block requires {CoefficientCount} coefficients.");
            }

            var copy = new double[CoefficientCount];
            Array.Copy(coefficients, copy, copy.Length);
            return new JpegBlock8x8(copy);
        }

        private static int ToIndex(int y, int x)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size)
            {
                throw JpegMarkerReader.CreateException("JPEG 8x8 block coordinate is outside the valid range.");
            }

            return y * Size + x;
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= CoefficientCount)
            {
                throw JpegMarkerReader.CreateException("JPEG 8x8 block index is outside the valid range.");
            }
        }
    }
}
