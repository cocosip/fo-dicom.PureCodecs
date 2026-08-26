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
            }

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var sourceFrame = oldPixelData.GetFrame(frame).ToArrayCopy();
                    if (oldPixelData.SamplesPerPixel == 3 && oldPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
                    {
                        sourceFrame = Jpeg2000FrameLayout.PlanarToInterleaved(
                            sourceFrame,
                            oldPixelData.Width * oldPixelData.Height,
                            oldPixelData.BitsAllocated / 8);
                    }

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
                    var decoded = _frameCodec.DecodeFrame(newPixelData, oldPixelData.GetFrame(frame).ToArrayCopy());
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
