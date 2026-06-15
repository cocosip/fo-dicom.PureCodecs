using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000ByteWriter
    {
        private readonly List<byte> _bytes = new List<byte>();

        public void WriteByte(byte value)
        {
            _bytes.Add(value);
        }

        public void WriteBytes(byte[] values)
        {
            _bytes.AddRange(values);
        }

        public void WriteUInt16(ushort value)
        {
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)value);
        }

        public void WriteUInt32(uint value)
        {
            _bytes.Add((byte)(value >> 24));
            _bytes.Add((byte)(value >> 16));
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)value);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }

    internal sealed class Jpeg2000ByteReader
    {
        private readonly byte[] _bytes;
        private int _offset;

        public Jpeg2000ByteReader(byte[] bytes)
        {
            _bytes = bytes ?? new byte[0];
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _bytes[_offset++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = (ushort)Jpeg2000Binary.ReadUInt16(_bytes, _offset);
            _offset += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = Jpeg2000Binary.ReadUInt32(_bytes, _offset);
            _offset += 4;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            Ensure(count);
            var values = new byte[count];
            Buffer.BlockCopy(_bytes, _offset, values, 0, count);
            _offset += count;
            return values;
        }

        private void Ensure(int count)
        {
            if (_offset + count > _bytes.Length)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 managed payload ended unexpectedly.");
            }
        }
    }
}
