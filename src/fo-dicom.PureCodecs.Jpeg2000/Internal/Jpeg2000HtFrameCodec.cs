using System;
using System.Text;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000HtFrameCodec
    {
        private const int OpenJphDecompositionLevels = 5;

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
            var decompositionLevels = OpenJphDecompositionLevels;
            var usesNativeDefaultLossyParameters = lossy && qualityTolerance == 0;
            var codingBitDepth = !lossy || usesNativeDefaultLossyParameters ? bitsAllocated : bitsStored;
            var encodedSteps = lossy
                ? usesNativeDefaultLossyParameters
                    ? Jpeg2000HtIrreversibleQuantization.CreateOpenJphScalarExpoundedSteps(
                        decompositionLevels,
                        codingBitDepth,
                        baseDelta: -1.0f)
                    : Jpeg2000HtIrreversibleQuantization.CreateQualityScalarExpoundedSteps(
                        decompositionLevels,
                        bitsStored,
                        CreateLossyQuality(qualityTolerance))
                : Array.Empty<ushort>();
            var encoder = new Jpeg2000HtTileEncoder();
            var tileParts = lossy
                ? encoder.EncodeLossyTileParts(pixelData, frame, progressionOrder, decompositionLevels, encodedSteps, codingBitDepth)
                : encoder.EncodeLosslessTileParts(pixelData, frame, progressionOrder, decompositionLevels, codingBitDepth);
            byte[] qcdPayload;
            if (lossy)
            {
                qcdPayload = Jpeg2000MarkerPayloadBuilder.CreateScalarExpoundedIrreversibleQuantization(encodedSteps, guardBits: 1);
            }
            else
            {
                var reversibleExponentBits = Jpeg2000ReversibleBiboGains.CreateReversibleExponentBits(
                    codingBitDepth,
                    samplesPerPixel == 3,
                    decompositionLevels);
                qcdPayload = Jpeg2000MarkerPayloadBuilder.CreateReversibleQuantizationFromExponentBits(reversibleExponentBits);
            }

            var magnitudeBound = lossy
                ? Jpeg2000MarkerPayloadBuilder.ComputeScalarExpoundedMagnitudeBound(encodedSteps, guardBits: 1, decompositionLevels)
                : Jpeg2000MarkerPayloadBuilder.ComputeReversibleMagnitudeBound(qcdPayload);

            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, Jpeg2000MarkerPayloadBuilder.CreateSize(width, height, codingBitDepth, isSigned, samplesPerPixel, capabilities: 0x4000));
            writer.WriteSegment(Jpeg2000Marker.CAP, Jpeg2000MarkerPayloadBuilder.CreateHighThroughputCapabilities(reversible: !lossy, magnitudeBound));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(progressionOrder, samplesPerPixel == 3, decompositionLevels, lossy));
            writer.WriteSegment(Jpeg2000Marker.QCD, qcdPayload);
            writer.WriteSegment(Jpeg2000Marker.COM, CreateOpenJphCommentPayload());
            var writtenTileParts = ApplyOpenJphTilePartDivision(tileParts, progressionOrder);
            writer.WriteSegment(Jpeg2000Marker.TLM, Jpeg2000MarkerPayloadBuilder.CreateTilePartLengths(TilePartLengths(writtenTileParts)));
            for (var i = 0; i < writtenTileParts.Length; i++)
            {
                writer.WriteSegment(Jpeg2000Marker.SOT, Jpeg2000MarkerPayloadBuilder.CreateStartOfTile(writtenTileParts[i].Length, i, writtenTileParts.Length));
                writer.WriteStandalone(Jpeg2000Marker.SOD);
                writer.WriteRaw(writtenTileParts[i]);
            }

            writer.WriteStandalone(Jpeg2000Marker.EOC);
            return writer.ToArray();
        }

        private static byte[][] ApplyOpenJphTilePartDivision(byte[][] tileParts, Jpeg2000ProgressionOrder progressionOrder)
        {
            if (progressionOrder == Jpeg2000ProgressionOrder.PCRL || progressionOrder == Jpeg2000ProgressionOrder.CPRL)
            {
                return new[] { Concat(tileParts) };
            }

            return tileParts;
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

        private static int[] TilePartLengths(byte[][] tileParts)
        {
            var lengths = new int[tileParts.Length];
            for (var i = 0; i < tileParts.Length; i++)
            {
                lengths[i] = tileParts[i].Length;
            }

            return lengths;
        }

        private static byte[] Concat(byte[][] parts)
        {
            var length = 0;
            foreach (var part in parts)
            {
                length += part.Length;
            }

            var result = new byte[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static int CreateLossyQuality(int tolerance)
        {
            return Math.Max(30, Math.Min(95, 96 - (Math.Max(1, tolerance) * 4)));
        }

        private static byte[] CreateOpenJphCommentPayload()
        {
            var text = Encoding.ASCII.GetBytes("OpenJPH Ver 0.21.2.");
            var payload = new byte[text.Length + 2];
            payload[1] = 1;
            Buffer.BlockCopy(text, 0, payload, 2, text.Length);
            return payload;
        }

        private static void ValidatePixelData(DicomPixelData pixelData, byte[] frame)
        {
            Jpeg2000FrameMetadata.ValidateFrameShape(pixelData, frame, "HTJ2K");
        }
    }
}
