using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000FloatRgb : IEquatable<Jpeg2000FloatRgb>
    {
        public Jpeg2000FloatRgb(double r, double g, double b)
        {
            R = r;
            G = g;
            B = b;
        }

        public double R { get; }

        public double G { get; }

        public double B { get; }

        public bool Equals(Jpeg2000FloatRgb other)
        {
            return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);
        }

        public override bool Equals(object obj)
        {
            return obj is Jpeg2000FloatRgb other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = R.GetHashCode();
                hash = (hash * 397) ^ G.GetHashCode();
                hash = (hash * 397) ^ B.GetHashCode();
                return hash;
            }
        }
    }
}
