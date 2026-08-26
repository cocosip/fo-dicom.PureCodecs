using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000ClassicFrameCodec
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
            double rate = 20,
            double[]? layerRates = null)
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
                rate,
                layerRates);
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] codestream)
        {
            var parsed = Jpeg2000CodestreamParser.ParseTiles(
                codestream,
                sodFamilyName: "JPEG 2000",
                codestreamName: "JPEG 2000 classic");
            ValidateComponentSampling(parsed.Size);

            if (parsed.Tiles.Count == 1 && IsManagedPayload(parsed.Tiles[0].TileData))
            {
                var decoded = DecodePayload(UnescapeManagedPayload(parsed.Tiles[0].TileData));
                ValidateDecodedMetadata(targetPixelData, parsed.Size, decoded);
                return decoded.Frame;
            }

            var image = Jpeg2000ImageModel.FromSizeSegment(parsed.Size);
            var bytesPerPixel = targetPixelData.SamplesPerPixel * targetPixelData.BytesAllocated;
            var frame = new byte[targetPixelData.Width * targetPixelData.Height * bytesPerPixel];
            var decoder = new Jpeg2000StandardFrameDecoder(Jpeg2000DecodeProfile.ClassicOpenJpeg);
            for (var tileIndex = 0; tileIndex < parsed.Tiles.Count; tileIndex++)
            {
                var parsedTile = parsed.Tiles[tileIndex];
                if (IsManagedPayload(parsedTile.TileData))
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 managed payloads cannot be combined as multiple tiles.");
                }

                var tile = image.Tiles[tileIndex];
                var tileFrame = decoder.DecodeTile(
                    targetPixelData,
                    parsed.Size,
                    parsedTile.CodingStyle,
                    parsedTile.Quantization,
                    parsedTile.ComponentCodingStyles,
                    parsedTile.ComponentQuantizations,
                    parsedTile.RegionOfInterestShifts,
                    parsedTile.ProgressionChanges,
                    parsedTile.PackedPacketHeaders,
                    parsedTile.TileData,
                    tile);
                CopyTileToFrame(frame, tileFrame, targetPixelData.Width, bytesPerPixel, parsed.Size, tile);
            }

            return frame;
        }

        private static void CopyTileToFrame(
            byte[] frame,
            byte[] tileFrame,
            int frameWidth,
            int bytesPerPixel,
            Jpeg2000SizeSegment siz,
            Jpeg2000TileModel tile)
        {
            var tileWidth = checked((int)tile.Width);
            var tileHeight = checked((int)tile.Height);
            var destinationX = checked((int)(tile.X0 - siz.ImageOffsetX));
            var destinationY = checked((int)(tile.Y0 - siz.ImageOffsetY));
            var rowBytes = checked(tileWidth * bytesPerPixel);
            if (tileFrame.Length != checked(rowBytes * tileHeight))
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 decoded tile length does not match its SIZ geometry.");
            }

            for (var row = 0; row < tileHeight; row++)
            {
                Buffer.BlockCopy(
                    tileFrame,
                    row * rowBytes,
                    frame,
                    ((destinationY + row) * frameWidth + destinationX) * bytesPerPixel,
                    rowBytes);
            }
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
