using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardTier1Encoder
    {
        private const int ContextZeroCodingStart = 0;
        private const int ContextRunLength = 17;
        private const int ContextUniform = 18;
        private const uint Significant = 0x0001;
        private const uint Refined = 0x0002;
        private const uint Visited = 0x0004;
        private const uint SigN = 0x0010;
        private const uint SigS = 0x0020;
        private const uint SigW = 0x0040;
        private const uint SigE = 0x0080;
        private const uint SigNW = 0x0100;
        private const uint SigNE = 0x0200;
        private const uint SigSW = 0x0400;
        private const uint SigSE = 0x0800;
        private const uint SigNeighbors = SigN | SigS | SigW | SigE | SigNW | SigNE | SigSW | SigSE;
        private const uint Sign = 0x1000;
        private const uint SignN = 0x2000;
        private const uint SignS = 0x4000;
        private const uint SignW = 0x8000;
        private const uint SignE = 0x10000;

        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly int[] _data;
        private readonly uint[] _flags;
        private readonly int _orientation;
        private readonly byte _codeBlockStyle;
        private readonly byte[] _zeroCodingContexts = BuildZeroCodingContexts();
        private readonly byte[] _signCodingContexts = BuildSignCodingContexts();
        private readonly byte[] _signPredictions = BuildSignPredictions();
        private Jpeg2000StandardMqEncoder? _mq;
        private int _bitPlane;

        public Jpeg2000StandardTier1Encoder(int width, int height, int orientation, byte codeBlockStyle)
        {
            _width = width;
            _height = height;
            _stride = width + 2;
            _orientation = orientation;
            _codeBlockStyle = codeBlockStyle;
            _data = new int[(width + 2) * (height + 2)];
            _flags = new uint[(width + 2) * (height + 2)];
        }

        public byte[] Encode(int[] data, int passCount)
        {
            return Encode(data, passCount, out _);
        }

        public byte[] Encode(int[] data, int passCount, out int[] passLengths)
        {
            return Encode(data, passCount, out passLengths, out _);
        }

        public byte[] Encode(int[] data, int passCount, out int[] passLengths, out byte[][] passSnapshots)
        {
            var lengths = new List<int>();
            var snapshots = new List<byte[]>();
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    _data[ToIndex(x, y)] = data[(y * _width) + x];
                }
            }

            var maxBitPlane = FindMaxBitPlane();
            if (maxBitPlane < 0 || passCount <= 0)
            {
                passLengths = ArrayEmptyInt();
                passSnapshots = new byte[0][];
                return ArrayEmpty();
            }

            _mq = new Jpeg2000StandardMqEncoder(19);
            Mq.SetContextState(ContextUniform, 46);
            Mq.SetContextState(ContextRunLength, 3);
            Mq.SetContextState(ContextZeroCodingStart, 4);

            var pass = 0;
            var passType = 2;
            for (_bitPlane = maxBitPlane; _bitPlane >= 0 && pass < passCount;)
            {
                if (passType == 0 || (passType == 2 && pass == 0))
                {
                    ClearVisited();
                }

                switch (passType)
                {
                    case 0:
                        EncodeSignificancePropagation();
                        break;
                    case 1:
                        EncodeMagnitudeRefinement();
                        break;
                    default:
                        EncodeCleanup();
                        if ((_codeBlockStyle & 0x20) != 0)
                        {
                            Mq.Encode(1, ContextUniform);
                            Mq.Encode(0, ContextUniform);
                            Mq.Encode(1, ContextUniform);
                            Mq.Encode(0, ContextUniform);
                        }

                        break;
                }

                pass++;
                var snapshot = Mq.FlushEstimate();
                lengths.Add(snapshot.Length);
                snapshots.Add(snapshot);
                if (passType == 2)
                {
                    passType = 0;
                    _bitPlane--;
                }
                else
                {
                    passType++;
                }
            }

            var result = Mq.Flush();
            for (var i = 0; i < lengths.Count; i++)
            {
                if (lengths[i] > result.Length)
                {
                    lengths[i] = result.Length;
                }
            }

            passLengths = lengths.ToArray();
            passSnapshots = snapshots.ToArray();
            return result;
        }

        private void EncodeSignificancePropagation()
        {
            for (var stripe = 0; stripe < _height; stripe += 4)
            {
                for (var x = 0; x < _width; x++)
                {
                    for (var dy = 0; dy < 4 && stripe + dy < _height; dy++)
                    {
                        var y = stripe + dy;
                        var index = ToIndex(x, y);
                        var flags = _flags[index];
                        if ((flags & Significant) != 0 || (flags & SigNeighbors) == 0)
                        {
                            continue;
                        }

                        var significant = IsSignificant(index);
                        Mq.Encode(significant ? 1 : 0, GetZeroCodingContext(flags));
                        _flags[index] |= Visited;
                        if (significant)
                        {
                            SetSignificant(x, y, index);
                        }
                    }
                }
            }
        }

        private void EncodeMagnitudeRefinement()
        {
            for (var stripe = 0; stripe < _height; stripe += 4)
            {
                for (var x = 0; x < _width; x++)
                {
                    for (var dy = 0; dy < 4 && stripe + dy < _height; dy++)
                    {
                        var y = stripe + dy;
                        var index = ToIndex(x, y);
                        var flags = _flags[index];
                        if ((flags & Significant) == 0 || (flags & Visited) != 0)
                        {
                            continue;
                        }

                        Mq.Encode(MagnitudeBit(index), GetMagnitudeContext(flags));
                        _flags[index] |= Refined;
                    }
                }
            }
        }

        private void EncodeCleanup()
        {
            for (var stripe = 0; stripe < _height; stripe += 4)
            {
                for (var x = 0; x < _width; x++)
                {
                    var canRunLength = stripe + 3 < _height;
                    var runLengthPosition = -1;
                    if (canRunLength)
                    {
                        for (var dy = 0; dy < 4; dy++)
                        {
                            var index = ToIndex(x, stripe + dy);
                            var flags = _flags[index];
                            if ((flags & (Visited | Significant | SigNeighbors)) != 0)
                            {
                                canRunLength = false;
                                break;
                            }

                            if (runLengthPosition < 0 && IsSignificant(index))
                            {
                                runLengthPosition = dy;
                            }
                        }
                    }

                    if (canRunLength)
                    {
                        Mq.Encode(runLengthPosition >= 0 ? 1 : 0, ContextRunLength);
                        if (runLengthPosition < 0)
                        {
                            continue;
                        }

                        Mq.Encode((runLengthPosition >> 1) & 1, ContextUniform);
                        Mq.Encode(runLengthPosition & 1, ContextUniform);
                    }

                    for (var dy = 0; dy < 4 && stripe + dy < _height; dy++)
                    {
                        if (canRunLength && dy < runLengthPosition)
                        {
                            continue;
                        }

                        var y = stripe + dy;
                        var index = ToIndex(x, y);
                        var flags = _flags[index];
                        if ((flags & Visited) != 0 || (flags & Significant) != 0)
                        {
                            _flags[index] &= ~Visited;
                            continue;
                        }

                        var significant = canRunLength && dy == runLengthPosition;
                        if (!significant)
                        {
                            significant = IsSignificant(index);
                            Mq.Encode(significant ? 1 : 0, GetZeroCodingContext(flags));
                        }

                        if (significant)
                        {
                            SetSignificant(x, y, index);
                        }

                        _flags[index] &= ~Visited;
                    }
                }
            }
        }

        private void SetSignificant(int x, int y, int index)
        {
            var signBit = _data[index] < 0 ? 1 : 0;
            var signContext = GetSignContext(_flags[index]);
            var signPrediction = GetSignPrediction(_flags[index]);
            Mq.Encode(signBit ^ signPrediction, signContext);
            if (signBit != 0)
            {
                _flags[index] |= Sign;
            }

            _flags[index] |= Significant;
            UpdateNeighbors(x, y, index);
        }

        private bool IsSignificant(int index)
        {
            var value = _data[index];
            if (value < 0)
            {
                value = -value;
            }

            return ((value >> _bitPlane) & 1) != 0;
        }

        private int MagnitudeBit(int index)
        {
            var value = _data[index];
            if (value < 0)
            {
                value = -value;
            }

            return (value >> _bitPlane) & 1;
        }

        private int FindMaxBitPlane()
        {
            var max = 0;
            foreach (var value in _data)
            {
                var abs = value < 0 ? -value : value;
                if (abs > max)
                {
                    max = abs;
                }
            }

            if (max == 0)
            {
                return -1;
            }

            var bitPlane = 0;
            while (max > 0)
            {
                max >>= 1;
                bitPlane++;
            }

            return bitPlane - 1;
        }

        private int GetZeroCodingContext(uint flags)
        {
            var lookup = ZeroCodingLookup(flags);
            var generatorOrientation = _orientation == 1 ? 2 : _orientation == 2 ? 1 : _orientation;
            return _zeroCodingContexts[(generatorOrientation << 9) | lookup];
        }

        private static int GetMagnitudeContext(uint flags)
        {
            if ((flags & Refined) != 0)
            {
                return 16;
            }

            return (flags & SigNeighbors) != 0 ? 15 : 14;
        }

        private int GetSignContext(uint flags)
        {
            return _signCodingContexts[SignCodingLookup(flags)];
        }

        private int GetSignPrediction(uint flags)
        {
            return _signPredictions[SignCodingLookup(flags)];
        }

        private static int ZeroCodingLookup(uint flags)
        {
            var value = 0;
            if ((flags & SigNW) != 0) value |= 1 << 0;
            if ((flags & SigN) != 0) value |= 1 << 1;
            if ((flags & SigNE) != 0) value |= 1 << 2;
            if ((flags & SigW) != 0) value |= 1 << 3;
            if ((flags & SigE) != 0) value |= 1 << 5;
            if ((flags & SigSW) != 0) value |= 1 << 6;
            if ((flags & SigS) != 0) value |= 1 << 7;
            if ((flags & SigSE) != 0) value |= 1 << 8;
            return value;
        }

        private static int SignCodingLookup(uint flags)
        {
            var value = 0;
            if ((flags & SigW) != 0)
            {
                value |= 1 << 3;
                if ((flags & SignW) != 0) value |= 1 << 0;
            }

            if ((flags & SigN) != 0)
            {
                value |= 1 << 1;
                if ((flags & SignN) != 0) value |= 1 << 4;
            }

            if ((flags & SigE) != 0)
            {
                value |= 1 << 5;
                if ((flags & SignE) != 0) value |= 1 << 2;
            }

            if ((flags & SigS) != 0)
            {
                value |= 1 << 7;
                if ((flags & SignS) != 0) value |= 1 << 6;
            }

            return value;
        }

        private static byte[] BuildZeroCodingContexts()
        {
            var values = new byte[2048];
            for (var orientation = 0; orientation < 4; orientation++)
            {
                for (var lookup = 0; lookup < 512; lookup++)
                {
                    values[(orientation << 9) | lookup] = (byte)InitZeroCodingContext(lookup, orientation);
                }
            }

            return values;
        }

        private static byte[] BuildSignCodingContexts()
        {
            var values = new byte[256];
            for (var lookup = 0; lookup < values.Length; lookup++)
            {
                values[lookup] = (byte)InitSignCodingContext(lookup);
            }

            return values;
        }

        private static byte[] BuildSignPredictions()
        {
            var values = new byte[256];
            for (var lookup = 0; lookup < values.Length; lookup++)
            {
                values[lookup] = (byte)InitSignPrediction(lookup);
            }

            return values;
        }

        private static int InitZeroCodingContext(int f, int orient)
        {
            var horizontal = Bit(f, 3) + Bit(f, 5);
            var vertical = Bit(f, 1) + Bit(f, 7);
            var diagonal = Bit(f, 0) + Bit(f, 2) + Bit(f, 8) + Bit(f, 6);
            var n = 0;
            switch (orient)
            {
                case 2:
                    var temp = horizontal;
                    horizontal = vertical;
                    vertical = temp;
                    goto case 0;
                case 0:
                case 1:
                    if (horizontal == 0)
                    {
                        n = vertical == 0 ? diagonal == 0 ? 0 : diagonal == 1 ? 1 : 2 : vertical == 1 ? 3 : 4;
                    }
                    else if (horizontal == 1)
                    {
                        n = vertical == 0 ? diagonal == 0 ? 5 : 6 : 7;
                    }
                    else
                    {
                        n = 8;
                    }

                    break;
                case 3:
                    var hv = horizontal + vertical;
                    if (diagonal == 0)
                    {
                        n = hv == 0 ? 0 : hv == 1 ? 1 : 2;
                    }
                    else if (diagonal == 1)
                    {
                        n = hv == 0 ? 3 : hv == 1 ? 4 : 5;
                    }
                    else if (diagonal == 2)
                    {
                        n = hv == 0 ? 6 : 7;
                    }
                    else
                    {
                        n = 8;
                    }

                    break;
            }

            return ContextZeroCodingStart + n;
        }

        private static int InitSignCodingContext(int lookup)
        {
            var horizontal = Min(IsPositive(lookup, 5, 2) + IsPositive(lookup, 3, 0), 1)
                - Min(IsNegative(lookup, 5, 2) + IsNegative(lookup, 3, 0), 1);
            var vertical = Min(IsPositive(lookup, 1, 4) + IsPositive(lookup, 7, 6), 1)
                - Min(IsNegative(lookup, 1, 4) + IsNegative(lookup, 7, 6), 1);
            if (horizontal < 0)
            {
                horizontal = -horizontal;
                vertical = -vertical;
            }

            var n = 0;
            if (horizontal == 0)
            {
                n = vertical == 0 ? 0 : 1;
            }
            else if (horizontal == 1)
            {
                n = vertical == -1 ? 2 : vertical == 0 ? 3 : 4;
            }

            return 9 + n;
        }

        private static int InitSignPrediction(int lookup)
        {
            var horizontal = Min(IsPositive(lookup, 5, 2) + IsPositive(lookup, 3, 0), 1)
                - Min(IsNegative(lookup, 5, 2) + IsNegative(lookup, 3, 0), 1);
            var vertical = Min(IsPositive(lookup, 1, 4) + IsPositive(lookup, 7, 6), 1)
                - Min(IsNegative(lookup, 1, 4) + IsNegative(lookup, 7, 6), 1);
            if (horizontal == 0 && vertical == 0)
            {
                return 0;
            }

            return horizontal > 0 || (horizontal == 0 && vertical > 0) ? 0 : 1;
        }

        private static int Bit(int value, int bit)
        {
            return (value & (1 << bit)) != 0 ? 1 : 0;
        }

        private static int Min(int left, int right)
        {
            return left < right ? left : right;
        }

        private static int IsPositive(int lookup, int sigBit, int signBit)
        {
            return ((lookup & (1 << sigBit)) != 0 && (lookup & (1 << signBit)) == 0) ? 1 : 0;
        }

        private static int IsNegative(int lookup, int sigBit, int signBit)
        {
            return ((lookup & (1 << sigBit)) != 0 && (lookup & (1 << signBit)) != 0) ? 1 : 0;
        }

        private void UpdateNeighbors(int x, int y, int index)
        {
            var negative = (_flags[index] & Sign) != 0;
            Mark(ToIndex(x, y - 1), SigS, negative ? SignS : 0);
            Mark(ToIndex(x, y + 1), SigN, negative ? SignN : 0);
            Mark(ToIndex(x - 1, y), SigE, negative ? SignE : 0);
            Mark(ToIndex(x + 1, y), SigW, negative ? SignW : 0);
            _flags[ToIndex(x - 1, y - 1)] |= SigSE;
            _flags[ToIndex(x + 1, y - 1)] |= SigSW;
            _flags[ToIndex(x - 1, y + 1)] |= SigNE;
            _flags[ToIndex(x + 1, y + 1)] |= SigNW;
        }

        private void Mark(int index, uint significantFlag, uint signFlag)
        {
            _flags[index] |= significantFlag;
            if (signFlag != 0)
            {
                _flags[index] |= signFlag;
            }
        }

        private void ClearVisited()
        {
            for (var i = 0; i < _flags.Length; i++)
            {
                _flags[i] &= ~Visited;
            }
        }

        private int ToIndex(int x, int y)
        {
            return (y + 1) * _stride + x + 1;
        }

        private Jpeg2000StandardMqEncoder Mq
        {
            get { return _mq ?? throw Jpeg2000Binary.CreateException("JPEG 2000 MQ encoder is not initialized."); }
        }

        private static byte[] ArrayEmpty()
        {
            return new byte[0];
        }

        private static int[] ArrayEmptyInt()
        {
            return new int[0];
        }
    }

    internal sealed class Jpeg2000StandardMqEncoder
    {
        private static readonly uint[] Qe =
        {
            0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401,
            0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601, 0x5601, 0x5401,
            0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201,
            0x1C01, 0x1801, 0x1601, 0x1401, 0x1201, 0x1101, 0x0AC1, 0x09C1,
            0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085,
            0x0049, 0x0025, 0x0015, 0x0009, 0x0005, 0x0001, 0x5601
        };

        private static readonly byte[] Nmps =
        {
            1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
            33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46
        };

        private static readonly byte[] Nlps =
        {
            1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14,
            15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
            30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46
        };

        private static readonly byte[] Switch =
        {
            1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        private readonly List<byte> _buffer = new List<byte> { 0 };
        private readonly byte[] _contexts;
        private int _bp;
        private uint _a = 0x8000;
        private uint _c;
        private int _ct = 12;

        public Jpeg2000StandardMqEncoder(int contextCount)
        {
            _contexts = new byte[contextCount];
        }

        public void SetContextState(int context, byte state)
        {
            _contexts[context] = state;
        }

        public void Encode(int bit, int context)
        {
            var cx = _contexts[context];
            var state = cx & 0x7F;
            var mps = cx >> 7;
            var qe = Qe[state];
            if (bit == mps)
            {
                _a -= qe;
                if ((_a & 0x8000) == 0)
                {
                    if (_a < qe)
                    {
                        _a = qe;
                    }
                    else
                    {
                        _c += qe;
                    }

                    _contexts[context] = (byte)(Nmps[state] | (mps << 7));
                    Renorm();
                }
                else
                {
                    _c += qe;
                }
            }
            else
            {
                _a -= qe;
                if (_a < qe)
                {
                    _c += qe;
                }
                else
                {
                    _a = qe;
                }

                var nextMps = Switch[state] != 0 ? 1 - mps : mps;
                _contexts[context] = (byte)(Nlps[state] | (nextMps << 7));
                Renorm();
            }
        }

        public byte[] Flush()
        {
            var temp = _c + _a;
            _c |= 0xFFFF;
            if (_c >= temp)
            {
                _c -= 0x8000;
            }

            _c <<= _ct;
            ByteOut();
            _c <<= _ct;
            ByteOut();
            if (_buffer[_bp] != 0xFF)
            {
                _bp++;
            }

            var length = _bp - 1;
            if (length <= 0)
            {
                return new byte[0];
            }

            var result = new byte[length];
            for (var i = 0; i < length; i++)
            {
                result[i] = _buffer[i + 1];
            }

            return result;
        }

        public int LengthWithFlushEstimate()
        {
            return FlushEstimate().Length;
        }

        public byte[] FlushEstimate()
        {
            var copy = new Jpeg2000StandardMqEncoder(this);
            return copy.Flush();
        }

        private Jpeg2000StandardMqEncoder(Jpeg2000StandardMqEncoder source)
        {
            _buffer = new List<byte>(source._buffer);
            _contexts = (byte[])source._contexts.Clone();
            _bp = source._bp;
            _a = source._a;
            _c = source._c;
            _ct = source._ct;
        }

        private void Renorm()
        {
            while (_a < 0x8000)
            {
                _a <<= 1;
                _c <<= 1;
                _ct--;
                if (_ct == 0)
                {
                    ByteOut();
                }
            }
        }

        private void ByteOut()
        {
            Ensure(_bp);
            if (_buffer[_bp] == 0xFF)
            {
                _bp++;
                Ensure(_bp);
                _buffer[_bp] = (byte)(_c >> 20);
                _c &= 0xFFFFF;
                _ct = 7;
                return;
            }

            if ((_c & 0x8000000) == 0)
            {
                _bp++;
                Ensure(_bp);
                _buffer[_bp] = (byte)(_c >> 19);
                _c &= 0x7FFFF;
                _ct = 8;
                return;
            }

            _buffer[_bp]++;
            if (_buffer[_bp] == 0xFF)
            {
                _c &= 0x7FFFFFF;
                _bp++;
                Ensure(_bp);
                _buffer[_bp] = (byte)(_c >> 20);
                _c &= 0xFFFFF;
                _ct = 7;
                return;
            }

            _bp++;
            Ensure(_bp);
            _buffer[_bp] = (byte)(_c >> 19);
            _c &= 0x7FFFF;
            _ct = 8;
        }

        private void Ensure(int index)
        {
            while (_buffer.Count <= index)
            {
                _buffer.Add(0);
            }
        }
    }
}
