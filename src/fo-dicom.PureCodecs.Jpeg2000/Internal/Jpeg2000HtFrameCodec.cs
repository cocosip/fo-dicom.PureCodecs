using System;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000HtFrameCodec
    {
        private static readonly byte[] PayloadMagic = { (byte)'P', (byte)'H', (byte)'T', (byte)'J', 0x01 };

        public byte[] EncodeFrame(
            DicomPixelData pixelData,
            byte[] frame,
            bool lossy,
            int qualityTolerance,
            Jpeg2000ProgressionOrder progressionOrder)
        {
            ValidatePixelData(pixelData, frame);

            var width = pixelData.Width;
            var height = pixelData.Height;
            var bitsAllocated = pixelData.BitsAllocated;
            var bitsStored = pixelData.BitsStored;
            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
            var samplesPerPixel = pixelData.SamplesPerPixel;
            var encodedFrame = lossy && bitsAllocated == 8 ? QuantizeLossy(frame, qualityTolerance) : Copy(frame);
            var payload = EncodePayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, lossy, encodedFrame);

            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, CreateSizePayload(width, height, bitsStored, isSigned, samplesPerPixel));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(lossy, progressionOrder));
            writer.WriteSegment(Jpeg2000Marker.QCD, lossy ? new byte[] { 0x22, 0x50, 0x00 } : new byte[] { 0x00, 0x08 });
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
                            throw Jpeg2000Binary.CreateException("HTJ2K SOD marker was found before SOT.");
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
                throw Jpeg2000Binary.CreateException("HTJ2K codestream is missing required marker data.");
            }

            var decoded = DecodePayload(payload);
            ValidateDecodedMetadata(targetPixelData, siz, decoded);
            return decoded.Frame;
        }

        private static byte[] EncodePayload(
            int width,
            int height,
            int bitsAllocated,
            int bitsStored,
            bool isSigned,
            int samplesPerPixel,
            bool lossy,
            byte[] frame)
        {
            var coefficients = new int[frame.Length];
            for (var i = 0; i < frame.Length; i++)
            {
                coefficients[i] = frame[i];
            }

            var block = Jpeg2000HtCodeBlockEncoder.Encode(new Jpeg2000ClassicCodeBlock(frame.Length, 1, coefficients));
            var writer = new Jpeg2000ByteWriter();
            writer.WriteBytes(PayloadMagic);
            writer.WriteUInt16((ushort)width);
            writer.WriteUInt16((ushort)height);
            writer.WriteByte((byte)bitsAllocated);
            writer.WriteByte((byte)bitsStored);
            writer.WriteByte((byte)(isSigned ? 1 : 0));
            writer.WriteByte((byte)samplesPerPixel);
            writer.WriteByte((byte)(lossy ? 1 : 0));
            writer.WriteUInt32((uint)frame.Length);
            writer.WriteUInt16((ushort)block.Width);
            writer.WriteUInt16((ushort)block.Height);
            writer.WriteUInt32((uint)block.MagSgnLength);
            writer.WriteUInt32((uint)block.MelLength);
            writer.WriteUInt32((uint)block.VlcLength);
            writer.WriteBytes(block.MagSgn);
            writer.WriteBytes(block.Mel);
            writer.WriteBytes(block.Vlc);
            return writer.ToArray();
        }

        private static Jpeg2000DecodedPayload DecodePayload(byte[] payload)
        {
            var reader = new Jpeg2000ByteReader(payload);
            foreach (var expected in PayloadMagic)
            {
                if (reader.ReadByte() != expected)
                {
                    throw Jpeg2000Binary.CreateException("HTJ2K managed payload signature is invalid.");
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
            var magSgnLength = (int)reader.ReadUInt32();
            var melLength = (int)reader.ReadUInt32();
            var vlcLength = (int)reader.ReadUInt32();
            var block = Jpeg2000HtCodeBlockDecoder.Decode(new Jpeg2000HtEncodedCodeBlock(
                blockWidth,
                blockHeight,
                reader.ReadBytes(magSgnLength),
                reader.ReadBytes(melLength),
                reader.ReadBytes(vlcLength)));

            var frame = new byte[frameLength];
            for (var i = 0; i < frame.Length; i++)
            {
                frame[i] = (byte)block.Coefficients[i];
            }

            return new Jpeg2000DecodedPayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, frame);
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

        private static byte[] CreateCodingStylePayload(bool lossy, Jpeg2000ProgressionOrder progressionOrder)
        {
            return new byte[]
            {
                0x00,
                (byte)progressionOrder,
                0x00,
                0x01,
                0x00,
                0x00,
                0x04,
                0x04,
                0x40,
                (byte)(lossy ? 1 : 0)
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
                throw Jpeg2000Binary.CreateException("HTJ2K codec currently supports only monochrome frames.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K codec supports only 8-bit and 16-bit allocated samples.");
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * (pixelData.BitsAllocated / 8);
            if (frame == null || frame.Length != expectedLength)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K codec frame length does not match DICOM metadata.");
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
                throw Jpeg2000Binary.CreateException("HTJ2K codestream metadata conflicts with DICOM pixel metadata.");
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
}
