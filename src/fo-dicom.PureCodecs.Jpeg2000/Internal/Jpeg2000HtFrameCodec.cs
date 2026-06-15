using System;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000HtFrameCodec
    {
        private static readonly byte[] PayloadMagicV1 = { (byte)'P', (byte)'H', (byte)'T', (byte)'J', 0x01 };
        private static readonly byte[] PayloadMagicV2 = { (byte)'P', (byte)'H', (byte)'T', (byte)'J', 0x02 };

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
            writer.WriteSegment(Jpeg2000Marker.SIZ, Jpeg2000MarkerPayloadBuilder.CreateSize(width, height, bitsStored, isSigned, samplesPerPixel));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(lossy, progressionOrder));
            writer.WriteSegment(
                Jpeg2000Marker.QCD,
                lossy
                    ? Jpeg2000MarkerPayloadBuilder.CreateScalarDerivedIrreversibleQuantization(0x5000)
                    : Jpeg2000MarkerPayloadBuilder.CreateNoQuantization(0x00, 0x08));
            writer.WriteSegment(Jpeg2000Marker.SOT, Jpeg2000MarkerPayloadBuilder.CreateStartOfTile(payload.Length));
            writer.WriteStandalone(Jpeg2000Marker.SOD);
            writer.WriteRaw(payload);
            writer.WriteStandalone(Jpeg2000Marker.EOC);
            return writer.ToArray();
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] codestream)
        {
            var parsed = Jpeg2000CodestreamParser.ParseSingleTilePart(
                codestream,
                sodFamilyName: "HTJ2K",
                codestreamName: "HTJ2K");
            var decoded = DecodePayload(parsed.TileData);
            ValidateDecodedMetadata(targetPixelData, parsed.Size, decoded);
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
            writer.WriteBytes(PayloadMagicV2);
            writer.WriteUInt16((ushort)width);
            writer.WriteUInt16((ushort)height);
            writer.WriteByte((byte)bitsAllocated);
            writer.WriteByte((byte)bitsStored);
            writer.WriteByte((byte)(isSigned ? 1 : 0));
            writer.WriteByte((byte)samplesPerPixel);
            writer.WriteByte((byte)(lossy ? 1 : 0));
            writer.WriteUInt32((uint)frame.Length);
            writer.WriteUInt32((uint)block.Width);
            writer.WriteUInt32((uint)block.Height);
            writer.WriteUInt32((uint)block.MagSgnLength);
            writer.WriteUInt32((uint)block.MelLength);
            writer.WriteUInt32((uint)block.VlcLength);
            writer.WriteBytes(block.MagSgn);
            writer.WriteBytes(block.Mel);
            writer.WriteBytes(block.Vlc);
            return writer.ToArray();
        }

        private static Jpeg2000DecodedFramePayload DecodePayload(byte[] payload)
        {
            var reader = new Jpeg2000ByteReader(payload);
            var version = ReadPayloadVersion(reader);
            if (version != 1 && version != 2)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K managed payload signature is invalid.");
            }

            var width = reader.ReadUInt16();
            var height = reader.ReadUInt16();
            var bitsAllocated = reader.ReadByte();
            var bitsStored = reader.ReadByte();
            var isSigned = reader.ReadByte() != 0;
            var samplesPerPixel = reader.ReadByte();
            reader.ReadByte();
            var frameLength = (int)reader.ReadUInt32();
            var blockWidth = version == 1
                ? reader.ReadUInt16()
                : checked((int)reader.ReadUInt32());
            var blockHeight = version == 1
                ? reader.ReadUInt16()
                : checked((int)reader.ReadUInt32());
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
            if (block.Coefficients.Count < frame.Length)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K managed payload code-block data is shorter than the declared frame length.");
            }

            for (var i = 0; i < frame.Length; i++)
            {
                frame[i] = (byte)block.Coefficients[i];
            }

            return new Jpeg2000DecodedFramePayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, frame);
        }

        private static int ReadPayloadVersion(Jpeg2000ByteReader reader)
        {
            for (var i = 0; i < PayloadMagicV2.Length - 1; i++)
            {
                if (reader.ReadByte() != PayloadMagicV2[i])
                {
                    return -1;
                }
            }

            var version = reader.ReadByte();
            return version == PayloadMagicV1[PayloadMagicV1.Length - 1] ||
                version == PayloadMagicV2[PayloadMagicV2.Length - 1]
                ? version
                : -1;
        }

        private static byte[] CreateCodingStylePayload(bool lossy, Jpeg2000ProgressionOrder progressionOrder)
        {
            return Jpeg2000MarkerPayloadBuilder.CreateCodingStyle(
                progressionOrder,
                layerCount: 1,
                usesMultipleComponentTransform: false,
                decompositionLevels: 0,
                codeBlockStyle: 0x40,
                transformation: (byte)(lossy ? 1 : 0));
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
            Jpeg2000FrameMetadata.ValidateFrameShape(pixelData, frame, "HTJ2K");
        }

        private static void ValidateDecodedMetadata(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000DecodedFramePayload decoded)
        {
            Jpeg2000FrameMetadata.ValidateDecodedMetadata(targetPixelData, siz, decoded, "HTJ2K");
        }
    }
}
