using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public static class Jpeg2000HtMelEncoder
    {
        private static readonly int[] MelExponents = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5 };

        public static byte[] EncodeEvents(IReadOnlyList<bool> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var writer = new MelBitWriter();
            var state = 0;
            var run = 0;
            var threshold = 1;

            for (var i = 0; i < events.Count; i++)
            {
                if (!events[i])
                {
                    run++;
                    if (run >= threshold)
                    {
                        writer.WriteBit(true);
                        run = 0;
                        state = Math.Min(12, state + 1);
                        threshold = 1 << MelExponents[state];
                    }
                }
                else
                {
                    writer.WriteBit(false);
                    for (var bit = MelExponents[state] - 1; bit >= 0; bit--)
                    {
                        writer.WriteBit(((run >> bit) & 1) != 0);
                    }

                    run = 0;
                    state = Math.Max(0, state - 1);
                    threshold = 1 << MelExponents[state];
                }
            }

            if (run > 0)
            {
                writer.WriteBit(true);
            }

            return writer.ToArray();
        }
    }

    public static class Jpeg2000HtMelDecoder
    {
        private static readonly int[] MelExponents = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5 };

        public static bool[] DecodeEvents(byte[] bytes, int eventCount)
        {
            if (eventCount < 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K MEL event count is invalid.");
            }

            var events = new List<bool>(eventCount);
            var reader = new MelBitReader(bytes);
            var state = 0;

            while (events.Count < eventCount)
            {
                var exponent = MelExponents[state];
                if (reader.ReadBit())
                {
                    var zeroEvents = (1 << exponent);
                    AppendZeros(events, eventCount, zeroEvents);
                    state = Math.Min(12, state + 1);
                }
                else
                {
                    var zeroEvents = exponent == 0 ? 0 : (int)reader.ReadBits(exponent);
                    AppendZeros(events, eventCount, zeroEvents);
                    if (events.Count < eventCount)
                    {
                        events.Add(true);
                    }

                    state = Math.Max(0, state - 1);
                }
            }

            return events.ToArray();
        }

        private static void AppendZeros(List<bool> events, int eventCount, int count)
        {
            for (var i = 0; i < count && events.Count < eventCount; i++)
            {
                events.Add(false);
            }
        }
    }

    internal sealed class MelBitWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private int _current;
        private int _remainingBits = 8;

        public void WriteBit(bool bit)
        {
            _current = (_current << 1) | (bit ? 1 : 0);
            _remainingBits--;
            if (_remainingBits == 0)
            {
                FlushByte();
            }
        }

        public byte[] ToArray()
        {
            if (_remainingBits < 8)
            {
                _current <<= _remainingBits;
                FlushByte();
            }

            return _bytes.ToArray();
        }

        private void FlushByte()
        {
            var value = (byte)_current;
            _bytes.Add(value);
            _remainingBits = value == 0xFF ? 7 : 8;
            _current = 0;
        }
    }

    internal sealed class MelBitReader
    {
        private readonly byte[] _bytes;
        private int _offset;
        private int _current;
        private int _remainingBits;
        private bool _unstuffNext;

        public MelBitReader(byte[] bytes)
        {
            _bytes = bytes ?? Array.Empty<byte>();
        }

        public bool ReadBit()
        {
            if (_remainingBits == 0)
            {
                FillByte();
            }

            _remainingBits--;
            return ((_current >> _remainingBits) & 1) != 0;
        }

        public uint ReadBits(int bitCount)
        {
            uint value = 0;
            for (var i = 0; i < bitCount; i++)
            {
                value = (value << 1) | (ReadBit() ? 1u : 0u);
            }

            return value;
        }

        private void FillByte()
        {
            if (_offset >= _bytes.Length)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K MEL bitstream ended unexpectedly.");
            }

            _current = _bytes[_offset++];
            _remainingBits = _unstuffNext ? 7 : 8;
            _unstuffNext = _current == 0xFF;
        }
    }
}
