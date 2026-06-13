using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ClassicFrameCodec
    {
        private static readonly byte[] PayloadMagic = { (byte)'P', (byte)'C', (byte)'J', (byte)'2', 0x01 };

        public byte[] EncodeFrame(DicomPixelData pixelData, byte[] frame, bool irreversible, int qualityTolerance)
        {
            ValidatePixelData(pixelData, frame);

            var width = pixelData.Width;
            var height = pixelData.Height;
            var bitsAllocated = pixelData.BitsAllocated;
            var bitsStored = pixelData.BitsStored;
            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
            var samplesPerPixel = pixelData.SamplesPerPixel;
            var encodedFrame = irreversible && bitsAllocated == 8
                ? QuantizeLossy(frame, qualityTolerance)
                : Copy(frame);

            var payload = EncodePayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, irreversible, encodedFrame);
            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, CreateSizePayload(width, height, bitsStored, isSigned, samplesPerPixel));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(irreversible));
            writer.WriteSegment(Jpeg2000Marker.QCD, irreversible ? new byte[] { 0x22, 0x50, 0x00 } : new byte[] { 0x00, 0x08 });
            writer.WriteSegment(Jpeg2000Marker.SOT, CreateStartOfTilePayload(payload.Length));
            writer.WriteStandalone(Jpeg2000Marker.SOD);
            writer.WriteRaw(payload);
            writer.WriteStandalone(Jpeg2000Marker.EOC);
            return writer.ToArray();
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] codestream)
        {
            Jpeg2000CodestreamReader.EnsureRawCodestream(codestream);
            var reader = new Jpeg2000CodestreamReader(codestream);
            Jpeg2000SizeSegment? siz = null;
            Jpeg2000CodingStyleDefault? cod = null;
            Jpeg2000QuantizationDefault? qcd = null;
            Jpeg2000StartOfTilePart? sot = null;
            byte[]? payload = null;

            while (!reader.EndOfData)
            {
                var segment = reader.ReadNext();
                switch (segment.Code)
                {
                    case Jpeg2000Marker.SOC:
                        break;
                    case Jpeg2000Marker.SIZ:
                        siz = Jpeg2000SizeSegment.Parse(segment);
                        break;
                    case Jpeg2000Marker.COD:
                        cod = Jpeg2000CodingStyleDefault.Parse(segment);
                        break;
                    case Jpeg2000Marker.QCD:
                        qcd = Jpeg2000QuantizationDefault.Parse(segment);
                        break;
                    case Jpeg2000Marker.SOT:
                        sot = Jpeg2000StartOfTilePart.Parse(segment, tileCount: 1);
                        break;
                    case Jpeg2000Marker.SOD:
                        payload = reader.ReadTileDataUntilEoc();
                        break;
                    case Jpeg2000Marker.EOC:
                        break;
                }
            }

            if (siz == null || cod == null || qcd == null || sot == null || payload == null)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codestream is missing required marker data.");
            }

            if (IsManagedPayload(payload))
            {
                var decoded = DecodePayload(payload);
                ValidateDecodedMetadata(targetPixelData, siz, decoded);
                return decoded.Frame;
            }

            return new Jpeg2000StandardFrameDecoder().Decode(targetPixelData, siz, cod, qcd, payload);
        }

        private static byte[] EncodePayload(
            int width,
            int height,
            int bitsAllocated,
            int bitsStored,
            bool isSigned,
            int samplesPerPixel,
            bool irreversible,
            byte[] frame)
        {
            var coefficients = new int[frame.Length];
            for (var i = 0; i < frame.Length; i++)
            {
                coefficients[i] = frame[i];
            }

            var block = new Jpeg2000ClassicCodeBlock(frame.Length, 1, coefficients);
            var encoded = Jpeg2000ClassicCodeBlockEncoder.Encode(block);
            var writer = new Jpeg2000ByteWriter();
            writer.WriteBytes(PayloadMagic);
            writer.WriteUInt16((ushort)width);
            writer.WriteUInt16((ushort)height);
            writer.WriteByte((byte)bitsAllocated);
            writer.WriteByte((byte)bitsStored);
            writer.WriteByte((byte)(isSigned ? 1 : 0));
            writer.WriteByte((byte)samplesPerPixel);
            writer.WriteByte((byte)(irreversible ? 1 : 0));
            writer.WriteUInt32((uint)frame.Length);
            writer.WriteUInt16((ushort)encoded.Width);
            writer.WriteUInt16((ushort)encoded.Height);
            writer.WriteUInt16((ushort)encoded.CodingPasses.Count);
            foreach (var pass in encoded.CodingPasses)
            {
                writer.WriteByte((byte)pass.Type);
                writer.WriteByte((byte)pass.BitPlane);
                writer.WriteUInt32((uint)pass.ByteLength);
            }

            writer.WriteUInt32((uint)encoded.Data.Length);
            writer.WriteBytes(encoded.Data);
            return writer.ToArray();
        }

        private static Jpeg2000DecodedPayload DecodePayload(byte[] payload)
        {
            var reader = new Jpeg2000ByteReader(payload);
            foreach (var expected in PayloadMagic)
            {
                if (reader.ReadByte() != expected)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 classic managed payload signature is invalid.");
                }
            }

            var width = reader.ReadUInt16();
            var height = reader.ReadUInt16();
            var bitsAllocated = reader.ReadByte();
            var bitsStored = reader.ReadByte();
            var isSigned = reader.ReadByte() != 0;
            var samplesPerPixel = reader.ReadByte();
            reader.ReadByte();
            var frameLength = (int)reader.ReadUInt32();
            var blockWidth = reader.ReadUInt16();
            var blockHeight = reader.ReadUInt16();
            var passCount = reader.ReadUInt16();
            var passes = new List<Jpeg2000Tier1Pass>();
            for (var i = 0; i < passCount; i++)
            {
                passes.Add(new Jpeg2000Tier1Pass(
                    (Jpeg2000Tier1PassType)reader.ReadByte(),
                    reader.ReadByte(),
                    (int)reader.ReadUInt32()));
            }

            var dataLength = (int)reader.ReadUInt32();
            var data = reader.ReadBytes(dataLength);
            var block = Jpeg2000ClassicCodeBlockDecoder.Decode(new Jpeg2000ClassicEncodedCodeBlock(blockWidth, blockHeight, data, passes));
            var frame = new byte[frameLength];
            for (var i = 0; i < frame.Length; i++)
            {
                frame[i] = (byte)block.Coefficients[i];
            }

            return new Jpeg2000DecodedPayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, frame);
        }

        private static bool IsManagedPayload(byte[] payload)
        {
            if (payload == null || payload.Length < PayloadMagic.Length)
            {
                return false;
            }

            for (var i = 0; i < PayloadMagic.Length; i++)
            {
                if (payload[i] != PayloadMagic[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] CreateSizePayload(int width, int height, int bitsStored, bool isSigned, int samplesPerPixel)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt32((uint)width);
            writer.WriteUInt32((uint)height);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32((uint)width);
            writer.WriteUInt32((uint)height);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt16((ushort)samplesPerPixel);
            for (var i = 0; i < samplesPerPixel; i++)
            {
                writer.WriteByte((byte)Jpeg2000SamplePrecision.ToSsiz(bitsStored, isSigned));
                writer.WriteByte(1);
                writer.WriteByte(1);
            }

            return writer.ToArray();
        }

        private static byte[] CreateCodingStylePayload(bool irreversible)
        {
            return new byte[]
            {
                0x00,
                (byte)Jpeg2000ProgressionOrder.LRCP,
                0x00,
                0x01,
                0x00,
                0x00,
                0x04,
                0x04,
                0x00,
                (byte)(irreversible ? 1 : 0)
            };
        }

        private static byte[] CreateStartOfTilePayload(int payloadLength)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt32((uint)(payloadLength + 14));
            writer.WriteByte(0);
            writer.WriteByte(1);
            return writer.ToArray();
        }

        private static byte[] QuantizeLossy(byte[] frame, int tolerance)
        {
            var quantized = new byte[frame.Length];
            var step = Math.Max(1, tolerance);
            for (var i = 0; i < frame.Length; i++)
            {
                quantized[i] = (byte)(Math.Round(frame[i] / (double)step) * step);
            }

            return quantized;
        }

        private static byte[] Copy(byte[] frame)
        {
            var copy = new byte[frame.Length];
            Buffer.BlockCopy(frame, 0, copy, 0, frame.Length);
            return copy;
        }

        private static void ValidatePixelData(DicomPixelData pixelData, byte[] frame)
        {
            if (pixelData.SamplesPerPixel != 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codec currently supports only monochrome frames.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codec supports only 8-bit and 16-bit allocated samples.");
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * (pixelData.BitsAllocated / 8);
            if (frame == null || frame.Length != expectedLength)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codec frame length does not match DICOM metadata.");
            }
        }

        private static void ValidateDecodedMetadata(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000DecodedPayload decoded)
        {
            if (targetPixelData.Width != decoded.Width
                || targetPixelData.Height != decoded.Height
                || targetPixelData.BitsAllocated != decoded.BitsAllocated
                || targetPixelData.BitsStored != decoded.BitsStored
                || targetPixelData.SamplesPerPixel != decoded.SamplesPerPixel
                || siz.Components.Count != decoded.SamplesPerPixel)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codestream metadata conflicts with DICOM pixel metadata.");
            }
        }

        private sealed class Jpeg2000DecodedPayload
        {
            public Jpeg2000DecodedPayload(int width, int height, int bitsAllocated, int bitsStored, bool isSigned, int samplesPerPixel, byte[] frame)
            {
                Width = width;
                Height = height;
                BitsAllocated = bitsAllocated;
                BitsStored = bitsStored;
                IsSigned = isSigned;
                SamplesPerPixel = samplesPerPixel;
                Frame = frame;
            }

            public int Width { get; }

            public int Height { get; }

            public int BitsAllocated { get; }

            public int BitsStored { get; }

            public bool IsSigned { get; }

            public int SamplesPerPixel { get; }

            public byte[] Frame { get; }
        }
    }

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
