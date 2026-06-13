using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000MqState
    {
        public Jpeg2000MqState(ushort probabilityEstimate, int mostProbableSymbolNextIndex, int leastProbableSymbolNextIndex, bool switchesMostProbableSymbol)
        {
            ProbabilityEstimate = probabilityEstimate;
            MostProbableSymbolNextIndex = mostProbableSymbolNextIndex;
            LeastProbableSymbolNextIndex = leastProbableSymbolNextIndex;
            SwitchesMostProbableSymbol = switchesMostProbableSymbol;
        }

        public ushort ProbabilityEstimate { get; }

        public int MostProbableSymbolNextIndex { get; }

        public int LeastProbableSymbolNextIndex { get; }

        public bool SwitchesMostProbableSymbol { get; }
    }

    public sealed class Jpeg2000MqDecoderStateTable
    {
        private readonly Jpeg2000MqState[] _states;

        private Jpeg2000MqDecoderStateTable(Jpeg2000MqState[] states)
        {
            _states = states;
        }

        public static Jpeg2000MqDecoderStateTable Default { get; } = new Jpeg2000MqDecoderStateTable(CreateStates());

        public int Count => _states.Length;

        public Jpeg2000MqState this[int index] => _states[index];

        internal static Jpeg2000MqState[] CreateStates()
        {
            ushort[] qe =
            {
                0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401,
                0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601, 0x5601, 0x5401,
                0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201,
                0x1C01, 0x1801, 0x1601, 0x1401, 0x1201, 0x1101, 0x0AC1, 0x09C1,
                0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085,
                0x0049, 0x0025, 0x0015, 0x0009, 0x0005, 0x0001, 0x5601
            };
            byte[] nmps =
            {
                1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16,
                17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46
            };
            byte[] nlps =
            {
                1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14,
                15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
                30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46
            };
            bool[] sw =
            {
                true, false, false, false, false, false, true, false,
                false, false, false, false, false, false, true, false,
                false, false, false, false, false, false, false, false,
                false, false, false, false, false, false, false, false,
                false, false, false, false, false, false, false, false,
                false, false, false, false, false, false, false
            };

            var states = new Jpeg2000MqState[qe.Length];
            for (var i = 0; i < states.Length; i++)
            {
                states[i] = new Jpeg2000MqState(qe[i], nmps[i], nlps[i], sw[i]);
            }

            return states;
        }
    }

    public sealed class Jpeg2000MqEncoderStateTable
    {
        private readonly Jpeg2000MqState[] _states;

        private Jpeg2000MqEncoderStateTable(Jpeg2000MqState[] states)
        {
            _states = states;
        }

        public static Jpeg2000MqEncoderStateTable Default { get; } = new Jpeg2000MqEncoderStateTable(Jpeg2000MqDecoderStateTable.CreateStates());

        public int Count => _states.Length;

        public Jpeg2000MqState this[int index] => _states[index];
    }

    public sealed class Jpeg2000MqEncoder
    {
        private readonly Jpeg2000BitWriter _writer = new Jpeg2000BitWriter();
        private readonly Dictionary<int, int> _contextStates = new Dictionary<int, int>();

        public void Encode(int contextIndex, bool symbol)
        {
            var stateIndex = GetState(contextIndex);
            var state = Jpeg2000MqEncoderStateTable.Default[stateIndex];
            _writer.WriteBit(symbol);
            _contextStates[contextIndex] = symbol ? state.MostProbableSymbolNextIndex : state.LeastProbableSymbolNextIndex;
        }

        public byte[] ToArray()
        {
            return _writer.ToArray();
        }

        private int GetState(int contextIndex)
        {
            return _contextStates.TryGetValue(contextIndex, out var state) ? state : 0;
        }
    }

    public sealed class Jpeg2000MqDecoder
    {
        private readonly Jpeg2000BitReader _reader;
        private readonly Dictionary<int, int> _contextStates = new Dictionary<int, int>();

        public Jpeg2000MqDecoder(byte[] bytes)
        {
            _reader = new Jpeg2000BitReader(bytes);
        }

        public bool Decode(int contextIndex)
        {
            var stateIndex = GetState(contextIndex);
            var state = Jpeg2000MqDecoderStateTable.Default[stateIndex];
            var symbol = _reader.ReadBit();
            _contextStates[contextIndex] = symbol ? state.MostProbableSymbolNextIndex : state.LeastProbableSymbolNextIndex;
            return symbol;
        }

        private int GetState(int contextIndex)
        {
            return _contextStates.TryGetValue(contextIndex, out var state) ? state : 0;
        }
    }
}
