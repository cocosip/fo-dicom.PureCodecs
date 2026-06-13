using System;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardTagTree
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _levels;
        private readonly int[][] _values;
        private readonly int[][] _low;
        private readonly int[] _levelWidths;
        private readonly int[] _levelHeights;

        public Jpeg2000StandardTagTree(int width, int height)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);

            var levels = 1;
            var w = _width;
            var h = _height;
            while (w > 1 || h > 1)
            {
                levels++;
                w = (w + 1) / 2;
                h = (h + 1) / 2;
            }

            _levels = levels;
            _levelWidths = new int[levels];
            _levelHeights = new int[levels];
            _values = new int[levels][];
            _low = new int[levels][];

            w = _width;
            h = _height;
            for (var level = 0; level < levels; level++)
            {
                _levelWidths[level] = w;
                _levelHeights[level] = h;
                var count = w * h;
                _values[level] = new int[count];
                _low[level] = new int[count];
                for (var i = 0; i < count; i++)
                {
                    _values[level][i] = 999;
                }

                w = (w + 1) / 2;
                h = (h + 1) / 2;
            }
        }

        public int Width
        {
            get { return _width; }
        }

        public int Height
        {
            get { return _height; }
        }

        public bool Decode(Jpeg2000BioReader reader, int x, int y, int threshold)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 tag-tree leaf is outside the tree.");
            }

            var levels = new int[_levels];
            var indices = new int[_levels];
            var px = x;
            var py = y;
            for (var level = 0; level < _levels; level++)
            {
                levels[level] = level;
                indices[level] = py * _levelWidths[level] + px;
                px /= 2;
                py /= 2;
            }

            var low = 0;
            var leafValue = 999;
            for (var stack = _levels - 1; stack >= 0; stack--)
            {
                var level = levels[stack];
                var index = indices[stack];

                if (low > _low[level][index])
                {
                    _low[level][index] = low;
                }
                else
                {
                    low = _low[level][index];
                }

                while (low < threshold && low < _values[level][index])
                {
                    if (reader.ReadBit() != 0)
                    {
                        _values[level][index] = low;
                    }
                    else
                    {
                        low++;
                    }
                }

                _low[level][index] = low;
                if (stack == 0)
                {
                    leafValue = _values[level][index];
                }
            }

            return leafValue < threshold;
        }

        public int DecodeValue(Jpeg2000BioReader reader, int x, int y)
        {
            for (var threshold = 1; threshold < 64; threshold++)
            {
                if (Decode(reader, x, y, threshold))
                {
                    return threshold - 1;
                }
            }

            throw Jpeg2000Binary.CreateException("JPEG 2000 tag-tree value is too large.");
        }
    }
}
