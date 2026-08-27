using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Internal;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public abstract class DicomHtJpeg2000CodecBase : IDicomCodec
    {
        private readonly bool _lossy;
        private readonly Jpeg2000ProgressionOrder _defaultProgressionOrder;
        private readonly Jpeg2000HtFrameCodec _frameCodec = new Jpeg2000HtFrameCodec();

        private protected DicomHtJpeg2000CodecBase(DicomTransferSyntax transferSyntax, bool lossy, Jpeg2000ProgressionOrder defaultProgressionOrder)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _lossy = lossy;
            _defaultProgressionOrder = defaultProgressionOrder;
        }

        public string Name => TransferSyntax.UID.Name;

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DicomHtJpeg2000Params { Jpeg2000ProgressionOrder = Jpeg2000ProgressionOrder.RPCL };
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var htParameters = DicomHtJpeg2000Params.From(parameters ?? GetDefaultParameters());
            ValidateParameters(htParameters);
            var progressionOrder = ResolveProgressionOrder(htParameters);
            var tolerance = ResolveTolerance(htParameters);

            if (oldPixelData.SamplesPerPixel == 3)
            {
                newPixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
                newPixelData.PhotometricInterpretation = _lossy
                    ? PhotometricInterpretation.YbrIct
                    : PhotometricInterpretation.YbrRct;
            }

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var sourceFrame = NormalizeFrameForEncode(
                        oldPixelData,
                        oldPixelData.GetFrame(frame).ToArrayCopy());

                    var encoded = _frameCodec.EncodeFrame(oldPixelData, sourceFrame, _lossy, tolerance, progressionOrder);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(encoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw CodecFailure.Wrap(TransferSyntax, "encode", frame, exception);
                }
            }
        }

        private Jpeg2000ProgressionOrder ResolveProgressionOrder(DicomHtJpeg2000Params parameters)
        {
            if (TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL)
            {
                return parameters.Jpeg2000ProgressionOrder;
            }

            return _defaultProgressionOrder;
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(
                        newPixelData,
                        oldPixelData.GetFrame(frame).ToArrayCopy(),
                        out var usesMultipleComponentTransform);
                    if (usesMultipleComponentTransform && newPixelData.SamplesPerPixel == 3)
                    {
                        newPixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
                        newPixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
                    }

                    if (newPixelData.SamplesPerPixel == 3 && newPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
                    {
                        decoded = Jpeg2000FrameLayout.InterleavedToPlanar(
                            decoded,
                            newPixelData.Width * newPixelData.Height,
                            newPixelData.BitsAllocated / 8);
                    }

                    newPixelData.AddFrame(CodecOutputBuffer.Create(decoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw CodecFailure.Wrap(TransferSyntax, "decode", frame, exception);
                }
            }
        }

        private static int ResolveTolerance(DicomHtJpeg2000Params parameters)
        {
            if (parameters.TargetRatio > 1)
            {
                return Math.Max(1, (int)Math.Ceiling(parameters.TargetRatio - 1));
            }

            return 0;
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            IByteBuffer normalized = new MemoryByteBuffer(frame);
            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
                {
                    throw new DicomCodecException("HTJ2K planar YBR_FULL_422 encoding is not supported.");
                }

                normalized = new MemoryByteBuffer(Jpeg2000FrameLayout.PlanarToInterleaved(
                    normalized.Data,
                    pixelData.Width * pixelData.Height,
                    pixelData.BitsAllocated / 8));
            }

            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull)
            {
                if (pixelData.BitsAllocated != 8)
                {
                    throw new DicomCodecException("HTJ2K YBR_FULL encoding supports only 8-bit allocated samples.");
                }

                normalized = PixelDataConverter.YbrFullToRgb(normalized);
            }
            else if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                if (pixelData.BitsAllocated != 8)
                {
                    throw new DicomCodecException("HTJ2K YBR_FULL_422 encoding supports only 8-bit allocated samples.");
                }

                normalized = PixelDataConverter.YbrFull422ToRgb(normalized, pixelData.Width);
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * pixelData.BytesAllocated;
            if (normalized.Data.Length < expectedLength)
            {
                throw new DicomCodecException("HTJ2K color conversion produced an incomplete RGB frame.");
            }

            if (normalized.Data.Length == expectedLength)
            {
                return normalized.Data;
            }

            var trimmed = new byte[expectedLength];
            Buffer.BlockCopy(normalized.Data, 0, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static void ValidateParameters(DicomHtJpeg2000Params parameters)
        {
            if (parameters.NumLayers != 1)
            {
                throw new DicomCodecException("HTJ2K NumLayers must equal 1 until HT packet-layer contributions are supported.");
            }

            if (parameters.TargetRatio != 0
                && (double.IsNaN(parameters.TargetRatio)
                    || double.IsInfinity(parameters.TargetRatio)
                    || parameters.TargetRatio <= 1))
            {
                throw new DicomCodecException("HTJ2K TargetRatio must be 0 or a finite value greater than 1.");
            }
        }

    }
}
