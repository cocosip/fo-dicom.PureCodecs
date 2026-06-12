using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000IntRgb : IEquatable<Jpeg2000IntRgb>
    {
        public Jpeg2000IntRgb(int r, int g, int b)
        {
            R = r;
            G = g;
            B = b;
        }

        public int R { get; }

        public int G { get; }

        public int B { get; }

        public bool Equals(Jpeg2000IntRgb other)
        {
            return R == other.R && G == other.G && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is Jpeg2000IntRgb other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = R;
                hash = (hash * 397) ^ G;
                hash = (hash * 397) ^ B;
                return hash;
            }
        }
    }
}
