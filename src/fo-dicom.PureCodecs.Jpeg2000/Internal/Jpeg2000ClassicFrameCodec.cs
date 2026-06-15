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
            bool encodeSignedPixelValuesAsUnsigned,
            double rate = 20)
        {
            ValidatePixelData(pixelData, frame);

            return new Jpeg2000StandardFrameEncoder().Encode(
                pixelData,
                frame,
                irreversible,
                progressionOrder,
                layerCount,
                usesMultipleComponentTransform,
                encodeSignedPixelValuesAsUnsigned,
                rate);
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] codestream)
        {
            var parsed = Jpeg2000CodestreamParser.ParseSingleTilePart(
                codestream,
                sodFamilyName: "JPEG 2000",
                codestreamName: "JPEG 2000 classic");
            ValidateComponentSampling(parsed.Size);

            if (IsManagedPayload(parsed.TileData))
            {
                var decoded = DecodePayload(UnescapeManagedPayload(parsed.TileData));
                ValidateDecodedMetadata(targetPixelData, parsed.Size, decoded);
                return decoded.Frame;
            }

            return new Jpeg2000StandardFrameDecoder().Decode(
                targetPixelData,
                parsed.Size,
                parsed.CodingStyle,
                parsed.Quantization,
                parsed.TileData);
        }

        private static Jpeg2000DecodedFramePayload DecodePayload(byte[] payload)
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
            return new Jpeg2000DecodedFramePayload(width, height, bitsAllocated, bitsStored, isSigned, samplesPerPixel, frame);
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

        private static void ValidatePixelData(DicomPixelData pixelData, byte[] frame)
        {
            Jpeg2000FrameMetadata.ValidateFrameShape(pixelData, frame, "JPEG 2000 classic");
        }

        private static void ValidateDecodedMetadata(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000DecodedFramePayload decoded)
        {
            Jpeg2000FrameMetadata.ValidateDecodedMetadata(targetPixelData, siz, decoded, "JPEG 2000 classic");
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

    }
}
