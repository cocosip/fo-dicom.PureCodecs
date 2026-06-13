using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ClassicCodeBlock
    {
        public Jpeg2000ClassicCodeBlock(int width, int height, IReadOnlyList<int> coefficients)
        {
            if (width <= 0 || height <= 0 || coefficients == null || coefficients.Count != width * height)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 code-block dimensions are invalid.");
            }

            Width = width;
            Height = height;
            var copy = new int[coefficients.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = coefficients[i];
            }

            Coefficients = copy;
        }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<int> Coefficients { get; }
    }
}
