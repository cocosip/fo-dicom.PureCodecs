using System;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000HtFrameCodec
    {
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
            var decompositionLevels = EffectiveDecompositionLevels(width, height);
            var encodedSteps = lossy
                ? Jpeg2000HtIrreversibleQuantization.CreateQualityScalarExpoundedSteps(
                    decompositionLevels,
                    bitsStored,
                    CreateLossyQuality(qualityTolerance))
                : Array.Empty<ushort>();
            var payload = lossy
                ? new Jpeg2000HtTileEncoder().EncodeLossy(pixelData, frame, progressionOrder, decompositionLevels, encodedSteps)
                : new Jpeg2000HtTileEncoder().EncodeLossless(pixelData, frame, progressionOrder, decompositionLevels);

            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, Jpeg2000MarkerPayloadBuilder.CreateSize(width, height, bitsStored, isSigned, samplesPerPixel, capabilities: 0x4000));
            writer.WriteSegment(Jpeg2000Marker.CAP, Jpeg2000MarkerPayloadBuilder.CreateHighThroughputCapabilities(reversible: !lossy, guardBits: lossy ? 1 : 0));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(progressionOrder, samplesPerPixel == 3, decompositionLevels, lossy));
            writer.WriteSegment(
                Jpeg2000Marker.QCD,
                lossy
                    ? Jpeg2000MarkerPayloadBuilder.CreateScalarExpoundedIrreversibleQuantization(encodedSteps, guardBits: 1)
                    : Jpeg2000MarkerPayloadBuilder.CreateReversibleQuantizationFromExponentBits(
                        Jpeg2000ReversibleBiboGains.CreateReversibleExponentBits(
                            bitsStored,
                            samplesPerPixel == 3,
                            decompositionLevels)));
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
            return new Jpeg2000StandardFrameDecoder().Decode(
                targetPixelData,
                parsed.Size,
                parsed.CodingStyle,
                parsed.Quantization,
                parsed.TileData);
        }

        private static byte[] CreateCodingStylePayload(Jpeg2000ProgressionOrder progressionOrder, bool usesMultipleComponentTransform, int decompositionLevels, bool irreversible)
        {
            return Jpeg2000MarkerPayloadBuilder.CreateCodingStyle(
                progressionOrder,
                layerCount: 1,
                usesMultipleComponentTransform: usesMultipleComponentTransform,
                decompositionLevels: decompositionLevels,
                codeBlockStyle: 0x40,
                transformation: (byte)(irreversible ? 0 : 1));
        }

        private static int EffectiveDecompositionLevels(int width, int height)
        {
            var levels = 0;
            while (levels < 5 && (width > 1 || height > 1))
            {
                width = (width + 1) >> 1;
                height = (height + 1) >> 1;
                levels++;
            }

            return levels;
        }

        private static int CreateLossyQuality(int tolerance)
        {
            return Math.Max(30, Math.Min(95, 96 - (Math.Max(1, tolerance) * 4)));
        }

        private static void ValidatePixelData(DicomPixelData pixelData, byte[] frame)
        {
            Jpeg2000FrameMetadata.ValidateFrameShape(pixelData, frame, "HTJ2K");
        }
    }
}
