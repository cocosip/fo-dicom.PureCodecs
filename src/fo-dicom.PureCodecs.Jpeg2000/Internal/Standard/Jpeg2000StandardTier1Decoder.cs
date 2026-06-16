using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardTier1Decoder
    {
        private const int ContextZeroCodingStart = 0;
        private const int ContextSignCodingStart = 9;
        private const int ContextMagnitudeRefinementStart = 14;
        private const int ContextRunLength = 17;
        private const int ContextUniform = 18;
        private const int Tier1FractionalBits = 6;
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
        private static readonly byte[] ZeroCodingContexts = BuildZeroCodingContexts();
        private static readonly byte[] SignCodingContexts = BuildSignCodingContexts();
        private static readonly byte[] SignPredictions = BuildSignPredictions();
        private Jpeg2000StandardMqDecoder? _mq;
        private Jpeg2000RawBitReader? _raw;
        private int _bitPlane;

        public Jpeg2000StandardTier1Decoder(int width, int height, int orientation, byte codeBlockStyle)
        {
            _width = width;
            _height = height;
            _stride = width + 2;
            _orientation = orientation;
            _codeBlockStyle = codeBlockStyle;
            _data = new int[(width + 2) * (height + 2)];
            _flags = new uint[(width + 2) * (height + 2)];
        }

        public int[] Decode(byte[] bytes, int passCount, int bitPlaneCount)
        {
            return DecodeInternal(bytes, passCount, bitPlaneCount, preserveFractionalBits: false);
        }

        public int[] DecodeScaled(byte[] bytes, int passCount, int bitPlaneCount)
        {
            return DecodeInternal(bytes, passCount, bitPlaneCount, preserveFractionalBits: true);
        }

        public int[] DecodeSegments(IReadOnlyList<Jpeg2000StandardCodeBlockSegment> segments, int bitPlaneCount)
        {
            return DecodeSegmentsInternal(segments, bitPlaneCount, preserveFractionalBits: false);
        }

        public int[] DecodeSegmentsScaled(IReadOnlyList<Jpeg2000StandardCodeBlockSegment> segments, int bitPlaneCount)
        {
            return DecodeSegmentsInternal(segments, bitPlaneCount, preserveFractionalBits: true);
        }

        private int[] DecodeSegmentsInternal(IReadOnlyList<Jpeg2000StandardCodeBlockSegment> segments, int bitPlaneCount, bool preserveFractionalBits)
        {
            if (segments == null || segments.Count == 0 || bitPlaneCount <= 0)
            {
                return new int[_width * _height];
            }

            var pass = 0;
            var passType = 2;
            var maxBitPlane = bitPlaneCount + Tier1FractionalBits - 1;
            _bitPlane = maxBitPlane;
            foreach (var segment in segments)
            {
                if (segment.PassCount <= 0)
                {
                    continue;
                }

                _mq = new Jpeg2000StandardMqDecoder(segment.Data, 19);
                _mq.SetContextState(ContextUniform, 46);
                _mq.SetContextState(ContextRunLength, 3);
                _mq.SetContextState(ContextZeroCodingStart, 4);
                _raw = new Jpeg2000RawBitReader(segment.Data);
                for (var segmentPass = 0; segmentPass < segment.PassCount && _bitPlane >= 0; segmentPass++)
                {
                    if (passType == 0 || (passType == 2 && pass == 0))
                    {
                        ClearVisited();
                    }

                    var raw = IsLazyPass(_bitPlane, maxBitPlane, passType);
                    switch (passType)
                    {
                        case 0:
                            DecodeSignificancePropagation(raw);
                            break;
                        case 1:
                            DecodeMagnitudeRefinement(raw);
                            break;
                        default:
                            DecodeCleanup();
                            if ((_codeBlockStyle & 0x20) != 0)
                            {
                                Mq.Decode(ContextUniform);
                                Mq.Decode(ContextUniform);
                                Mq.Decode(ContextUniform);
                                Mq.Decode(ContextUniform);
                            }

                            break;
                    }

                    pass++;
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
            }

            return GetResult(preserveFractionalBits);
        }

        private int[] DecodeInternal(byte[] bytes, int passCount, int bitPlaneCount, bool preserveFractionalBits)
        {
            if (bytes == null || bytes.Length == 0 || passCount <= 0 || bitPlaneCount <= 0)
            {
                return new int[_width * _height];
            }

            _mq = new Jpeg2000StandardMqDecoder(bytes, 19);
            _mq.SetContextState(ContextUniform, 46);
            _mq.SetContextState(ContextRunLength, 3);
            _mq.SetContextState(ContextZeroCodingStart, 4);
            _raw = new Jpeg2000RawBitReader(bytes);

            var pass = 0;
            var passType = 2;
            var maxBitPlane = bitPlaneCount + Tier1FractionalBits - 1;
            for (_bitPlane = maxBitPlane; _bitPlane >= 0 && pass < passCount;)
            {
                if (passType == 0 || (passType == 2 && pass == 0))
                {
                    ClearVisited();
                }

                var raw = IsLazyPass(_bitPlane, maxBitPlane, passType);
                switch (passType)
                {
                    case 0:
                        DecodeSignificancePropagation(raw);
                        break;
                    case 1:
                        DecodeMagnitudeRefinement(raw);
                        break;
                    default:
                        DecodeCleanup();
                        if ((_codeBlockStyle & 0x20) != 0)
                        {
                        Mq.Decode(ContextUniform);
                        Mq.Decode(ContextUniform);
                        Mq.Decode(ContextUniform);
                        Mq.Decode(ContextUniform);
                        }

                        break;
                }

                pass++;
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

            return GetResult(preserveFractionalBits);
        }

        private void DecodeSignificancePropagation(bool raw)
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

                        var bit = DecodeBit(GetZeroCodingContext(flags), raw);
                        _flags[index] |= Visited;
                        if (bit != 0)
                        {
                            SetSignificant(x, y, index, raw);
                        }
                    }
                }
            }
        }

        private void DecodeMagnitudeRefinement(bool raw)
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

                        var bit = DecodeBit(GetMagnitudeContext(flags), raw);
                        var value = HalfStep();
                        if (_data[index] < 0)
                        {
                            _data[index] += bit != 0 ? -value : value;
                        }
                        else
                        {
                            _data[index] += bit != 0 ? value : -value;
                        }

                        _flags[index] |= Refined;
                    }
                }
            }
        }

        private void DecodeCleanup()
        {
            for (var stripe = 0; stripe < _height; stripe += 4)
            {
                for (var x = 0; x < _width; x++)
                {
                    var canRunLength = stripe + 3 < _height;
                    if (canRunLength)
                    {
                        for (var dy = 0; dy < 4; dy++)
                        {
                            var flags = _flags[ToIndex(x, stripe + dy)];
                            if ((flags & (Visited | Significant | SigNeighbors)) != 0)
                            {
                                canRunLength = false;
                                break;
                            }
                        }
                    }

                    if (canRunLength && Mq.Decode(ContextRunLength) == 0)
                    {
                        continue;
                    }

                    var runLength = -1;
                    if (canRunLength)
                    {
                        runLength = (Mq.Decode(ContextUniform) << 1) | Mq.Decode(ContextUniform);
                    }

                    for (var dy = 0; dy < 4 && stripe + dy < _height; dy++)
                    {
                        if (canRunLength && dy < runLength)
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

                        var significant = canRunLength && dy == runLength
                            ? 1
                            : Mq.Decode(GetZeroCodingContext(flags));
                        if (significant != 0)
                        {
                            SetSignificant(x, y, index, raw: false);
                        }

                        _flags[index] &= ~Visited;
                    }
                }
            }
        }

        private void SetSignificant(int x, int y, int index, bool raw)
        {
            var sign = raw ? Raw.ReadBit() : Mq.Decode(GetSignContext(_flags[index])) ^ GetSignPrediction(_flags[index]);
            var value = OnePlusHalfStep();
            if (sign != 0)
            {
                _flags[index] |= Sign;
                value = -value;
            }

            _data[index] = value;
            _flags[index] |= Significant;
            UpdateNeighbors(x, y, index);
        }

        private int DecodeBit(int context, bool raw)
        {
            return raw ? Raw.ReadBit() : Mq.Decode(context);
        }

        private int GetZeroCodingContext(uint flags)
        {
            var lookup = ZeroCodingLookup(flags);
            var generatorOrientation = _orientation == 1 ? 2 : _orientation == 2 ? 1 : _orientation;
            return ZeroCodingContexts[(generatorOrientation << 9) | lookup];
        }

        private static int GetMagnitudeContext(uint flags)
        {
            if ((flags & Refined) != 0)
            {
                return ContextMagnitudeRefinementStart + 2;
            }

            return (flags & SigNeighbors) != 0 ? ContextMagnitudeRefinementStart + 1 : ContextMagnitudeRefinementStart;
        }

        private static int GetSignContext(uint flags)
        {
            return SignCodingContexts[SignCodingLookup(flags)];
        }

        private static int GetSignPrediction(uint flags)
        {
            return SignPredictions[SignCodingLookup(flags)];
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
                        if (vertical == 0)
                        {
                            n = diagonal == 0 ? 0 : diagonal == 1 ? 1 : 2;
                        }
                        else if (vertical == 1)
                        {
                            n = 3;
                        }
                        else
                        {
                            n = 4;
                        }
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

        private int OnePlusHalfStep()
        {
            var one = 1 << _bitPlane;
            return one | (one >> 1);
        }

        private static byte[] BuildZeroCodingContexts()
        {
            var values = new byte[2048];
            for (var generatorOrientation = 0; generatorOrientation < 4; generatorOrientation++)
            {
                for (var lookup = 0; lookup < 512; lookup++)
                {
                    values[(generatorOrientation << 9) | lookup] = (byte)InitZeroCodingContext(lookup, generatorOrientation);
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
                if (vertical == -1)
                {
                    n = 1;
                }
                else if (vertical == 0)
                {
                    n = 0;
                }
                else
                {
                    n = 1;
                }
            }
            else if (horizontal == 1)
            {
                if (vertical == -1)
                {
                    n = 2;
                }
                else if (vertical == 0)
                {
                    n = 3;
                }
                else
                {
                    n = 4;
                }
            }

            return ContextSignCodingStart + n;
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

        private int HalfStep()
        {
            return (1 << _bitPlane) >> 1;
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

        private bool IsLazyPass(int bitPlane, int maxBitPlane, int passType)
        {
            if ((_codeBlockStyle & 0x01) == 0)
            {
                return false;
            }

            var passIndexFromMsb = ((maxBitPlane - bitPlane) * 3) + passType;
            return passIndexFromMsb >= 10 && passType != 2;
        }

        private int[] GetResult(bool preserveFractionalBits)
        {
            var result = new int[_width * _height];
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var value = _data[ToIndex(x, y)];
                    result[(y * _width) + x] = preserveFractionalBits
                        ? value
                        : value >= 0
                        ? value >> Tier1FractionalBits
                        : -((-value) >> Tier1FractionalBits);
                }
            }

            return result;
        }

        private int ToIndex(int x, int y)
        {
            return (y + 1) * _stride + (x + 1);
        }

        private Jpeg2000StandardMqDecoder Mq
        {
            get { return _mq ?? throw Jpeg2000Binary.CreateException("JPEG 2000 MQ decoder is not initialized."); }
        }

        private Jpeg2000RawBitReader Raw
        {
            get { return _raw ?? throw Jpeg2000Binary.CreateException("JPEG 2000 RAW decoder is not initialized."); }
        }
    }
}
