using System;
using System.Collections.Generic;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Internal;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public abstract class DicomJpeg2000ClassicCodecBase : IDicomCodec
    {
        private readonly bool _defaultIrreversible;
        private readonly Jpeg2000ClassicFrameCodec _frameCodec = new Jpeg2000ClassicFrameCodec();

        protected DicomJpeg2000ClassicCodecBase(DicomTransferSyntax transferSyntax, bool defaultIrreversible)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _defaultIrreversible = defaultIrreversible;
        }

        public string Name
        {
            get { return TransferSyntax.UID.Name; }
        }

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DicomJpeg2000Params
            {
                Irreversible = _defaultIrreversible,
                IncludeFinalLosslessLayer = TransferSyntax == DicomTransferSyntax.JPEG2000Lossless
            };
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var jpeg2000Parameters = DicomJpeg2000Params.From(parameters ?? GetDefaultParameters());
            var irreversible = TransferSyntax == DicomTransferSyntax.JPEG2000Lossy
                && jpeg2000Parameters.Irreversible;
            var tolerance = ResolveTolerance(jpeg2000Parameters);
            var layerRates = ResolveLayerRates(jpeg2000Parameters, oldPixelData.BitsStored, oldPixelData.BitsAllocated);
            var layerCount = layerRates.Length;
            var usesMct = oldPixelData.SamplesPerPixel == 3 && jpeg2000Parameters.AllowMCT;

            ValidateSupportedPixelData(oldPixelData);
            UpdateCompressedPixelDataMetadata(oldPixelData, newPixelData, jpeg2000Parameters, irreversible, usesMct);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(
                        oldPixelData,
                        NormalizeFrameForEncode(oldPixelData, oldPixelData.GetFrame(frame).ToArrayCopy()),
                        irreversible,
                        tolerance,
                        jpeg2000Parameters.ProgressionOrder,
                        layerCount,
                        usesMct,
                        jpeg2000Parameters.EncodeSignedPixelValuesAsUnsigned,
                        jpeg2000Parameters.Rate,
                        layerRates);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(encoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw CodecFailure.Wrap(TransferSyntax, "encode", frame, exception);
                }
            }
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, oldPixelData.GetFrame(frame).ToArrayCopy());
                    decoded = NormalizeFrameForDecode(newPixelData, decoded);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(decoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw CodecFailure.Wrap(TransferSyntax, "decode", frame, exception);
                }
            }

            UpdateDecodedPixelDataMetadata(oldPixelData, newPixelData);
        }

        private static void UpdateDecodedPixelDataMetadata(DicomPixelData source, DicomPixelData target)
        {
            if (source.SamplesPerPixel != 3
                || (source.PhotometricInterpretation != PhotometricInterpretation.YbrIct
                    && source.PhotometricInterpretation != PhotometricInterpretation.YbrRct
                    && source.PhotometricInterpretation != PhotometricInterpretation.YbrFull
                    && source.PhotometricInterpretation != PhotometricInterpretation.YbrFull422
                    && source.PhotometricInterpretation != PhotometricInterpretation.YbrPartial422))
            {
                return;
            }

            target.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            target.PlanarConfiguration = PlanarConfiguration.Interleaved;
        }

        private static int ResolveTolerance(DicomJpeg2000Params parameters)
        {
            if (parameters.TargetRatio > 1)
            {
                return Math.Max(1, (int)Math.Ceiling(parameters.TargetRatio - 1));
            }

            if (parameters.Rate > 0 && parameters.Rate < 1)
            {
                return Math.Max(1, (int)Math.Ceiling(1d / parameters.Rate));
            }

            if (parameters.Rate > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(20d / parameters.Rate));
            }

            return 1;
        }

        private double[] ResolveLayerRates(DicomJpeg2000Params parameters, int bitsStored, int bitsAllocated)
        {
            if (parameters.TargetRatio != 0)
            {
                return ResolveTargetRatioLayerRates(parameters);
            }

            var layerRates = new List<double>();
            var rateLevels = parameters.RateLevels ?? new int[0];
            foreach (var rateLevel in rateLevels)
            {
                if (rateLevel <= parameters.Rate)
                {
                    break;
                }

                layerRates.Add(rateLevel);
            }

            if (parameters.Rate > 0)
            {
                layerRates.Add((double)parameters.Rate * bitsStored / bitsAllocated);
            }

            if (TransferSyntax == DicomTransferSyntax.JPEG2000Lossless && parameters.Rate > 0)
            {
                layerRates.Add(0);
            }

            if (layerRates.Count == 0)
            {
                layerRates.Add(0);
            }

            return layerRates.ToArray();
        }

        private double[] ResolveTargetRatioLayerRates(DicomJpeg2000Params parameters)
        {
            if (double.IsNaN(parameters.TargetRatio)
                || double.IsInfinity(parameters.TargetRatio)
                || parameters.TargetRatio <= 1)
            {
                throw new DicomCodecException("JPEG 2000 TargetRatio must be a finite value greater than 1.");
            }

            if (parameters.NumLayers < 1)
            {
                throw new DicomCodecException("JPEG 2000 NumLayers must be at least 1 when TargetRatio is specified.");
            }

            var lossless = TransferSyntax == DicomTransferSyntax.JPEG2000Lossless;
            if (lossless && !parameters.IncludeFinalLosslessLayer)
            {
                throw new DicomCodecException("JPEG 2000 Lossless TargetRatio encoding requires IncludeFinalLosslessLayer.");
            }

            if (!lossless && parameters.IncludeFinalLosslessLayer)
            {
                throw new DicomCodecException("JPEG 2000 Lossy encoding cannot include a final lossless layer.");
            }

            var totalLayerCount = (long)parameters.NumLayers + (parameters.IncludeFinalLosslessLayer ? 1 : 0);
            if (totalLayerCount > ushort.MaxValue)
            {
                throw new DicomCodecException("JPEG 2000 total layer count cannot exceed 65535.");
            }

            var layerRates = new double[(int)totalLayerCount];
            for (var layer = 0; layer < parameters.NumLayers; layer++)
            {
                layerRates[layer] = parameters.TargetRatio * Math.Pow(2, parameters.NumLayers - layer - 1);
            }

            if (parameters.IncludeFinalLosslessLayer)
            {
                layerRates[layerRates.Length - 1] = 0;
            }

            return layerRates;
        }

        private static void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw new DicomCodecException($"JPEG 2000 classic codec does not support SamplesPerPixel {pixelData.SamplesPerPixel}.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw new DicomCodecException("JPEG 2000 classic codec supports only 8-bit and 16-bit allocated samples.");
            }

            var photometric = pixelData.PhotometricInterpretation?.Value;
            if (photometric != PhotometricInterpretation.Monochrome1.Value
                && photometric != PhotometricInterpretation.Monochrome2.Value
                && photometric != PhotometricInterpretation.Rgb.Value
                && photometric != PhotometricInterpretation.PaletteColor.Value
                && photometric != PhotometricInterpretation.YbrIct.Value
                && photometric != PhotometricInterpretation.YbrRct.Value
                && photometric != "YBR_FULL"
                && photometric != "YBR_FULL_422")
            {
                throw new DicomCodecException($"JPEG 2000 classic codec does not support photometric interpretation {photometric ?? "<missing>"}.");
            }
        }

        private static void UpdateCompressedPixelDataMetadata(
            DicomPixelData oldPixelData,
            DicomPixelData newPixelData,
            DicomJpeg2000Params parameters,
            bool irreversible,
            bool usesMct)
        {
            if (oldPixelData.SamplesPerPixel != 3)
            {
                return;
            }

            newPixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
            if (!usesMct)
            {
                if (oldPixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull
                    || oldPixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
                {
                    newPixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
                }

                return;
            }

            var normalizedYbrInput = oldPixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull
                || oldPixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422;
            if (!parameters.UpdatePhotometricInterpretation && !normalizedYbrInput)
            {
                return;
            }

            newPixelData.PhotometricInterpretation = irreversible
                ? PhotometricInterpretation.YbrIct
                : PhotometricInterpretation.YbrRct;
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            IByteBuffer normalized = new MemoryByteBuffer(frame);
            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                normalized = new MemoryByteBuffer(Jpeg2000FrameLayout.PlanarToInterleaved(
                    frame,
                    pixelData.Width * pixelData.Height,
                    pixelData.BitsAllocated / 8));
            }

            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull)
            {
                normalized = PixelDataConverter.YbrFullToRgb(normalized);
            }
            else if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                normalized = PixelDataConverter.YbrFull422ToRgb(normalized, pixelData.Width);
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * (pixelData.BitsAllocated / 8);
            var bytes = normalized.Data;
            if (bytes.Length == expectedLength)
            {
                return bytes;
            }

            if (bytes.Length < expectedLength)
            {
                throw new DicomCodecException("JPEG 2000 color conversion produced an incomplete RGB frame.");
            }

            var trimmed = new byte[expectedLength];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static byte[] NormalizeFrameForDecode(DicomPixelData targetPixelData, byte[] frame)
        {
            if (targetPixelData.SamplesPerPixel == 3 && targetPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                return Jpeg2000FrameLayout.InterleavedToPlanar(
                    frame,
                    targetPixelData.Width * targetPixelData.Height,
                    targetPixelData.BitsAllocated / 8);
            }

            return frame;
        }

    }
}
