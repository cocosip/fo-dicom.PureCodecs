using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000ClassicFrameCodec
    {
        private static readonly byte[] PayloadMagic = { (byte)'P', (byte)'C', (byte)'J', (byte)'2', 0x01 };

        public byte[] EncodeFrame(
            DicomPixelData pixelData,
            byte[] frame,
            bool irreversible,
            int qualityTolerance,
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform,
            bool encodeSignedPixelValuesAsUnsigned)
        {
            ValidatePixelData(pixelData, frame);

            var width = pixelData.Width;
            var height = pixelData.Height;
            var bitsAllocated = pixelData.BitsAllocated;
            var bitsStored = pixelData.BitsStored;
            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed && !encodeSignedPixelValuesAsUnsigned;
            var samplesPerPixel = pixelData.SamplesPerPixel;
            var encodedFrame = irreversible && bitsAllocated == 8
                ? QuantizeLossy(frame, qualityTolerance)
                : Copy(frame);

            var payload = EscapeTilePayload(EncodePayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, irreversible, encodedFrame));
            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, CreateSizePayload(width, height, bitsStored, isSigned, samplesPerPixel));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(irreversible, progressionOrder, layerCount, usesMultipleComponentTransform));
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
            var reachedEndOfCodestream = false;

            while (!reader.EndOfData && !reachedEndOfCodestream)
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
                        if (sot == null)
                        {
                            throw Jpeg2000Binary.CreateException("JPEG 2000 SOD marker was found before SOT.");
                        }

                        payload = reader.ReadTileData(sot);
                        break;
                    case Jpeg2000Marker.EOC:
                        reachedEndOfCodestream = true;
                        break;
                }
            }

            if (siz == null || cod == null || qcd == null || sot == null || payload == null)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codestream is missing required marker data.");
            }

            ValidateComponentSampling(siz);

            if (IsManagedPayload(payload))
            {
                var decoded = DecodePayload(UnescapeManagedPayload(payload));
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
            writer.WriteBytes(frame);
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
            var frame = reader.ReadBytes(frameLength);
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

        private static byte[] EscapeTilePayload(byte[] payload)
        {
            var writer = new Jpeg2000ByteWriter();
            for (var i = 0; i < payload.Length; i++)
            {
                writer.WriteByte(payload[i]);
                if (payload[i] == 0xFF)
                {
                    writer.WriteByte(0x00);
                }
            }

            return writer.ToArray();
        }

        private static byte[] UnescapeManagedPayload(byte[] payload)
        {
            var bytes = new List<byte>(payload.Length);
            for (var i = 0; i < payload.Length; i++)
            {
                bytes.Add(payload[i]);
                if (payload[i] == 0xFF && i + 1 < payload.Length && payload[i + 1] == 0x00)
                {
                    i++;
                }
            }

            return bytes.ToArray();
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

        private static byte[] CreateCodingStylePayload(
            bool irreversible,
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform)
        {
            return new byte[]
            {
                0x00,
                (byte)progressionOrder,
                (byte)(layerCount >> 8),
                (byte)layerCount,
                (byte)(usesMultipleComponentTransform ? 1 : 0),
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
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 classic codec supports only monochrome or RGB frames.");
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

        private static void ValidateComponentSampling(Jpeg2000SizeSegment siz)
        {
            foreach (var component in siz.Components)
            {
                if (component.HorizontalSeparation != 1 || component.VerticalSeparation != 1)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 component subsampling is not supported.");
                }
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
