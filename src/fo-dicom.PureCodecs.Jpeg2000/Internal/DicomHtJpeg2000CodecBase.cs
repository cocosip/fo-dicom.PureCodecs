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

        protected DicomHtJpeg2000CodecBase(DicomTransferSyntax transferSyntax, bool lossy, Jpeg2000ProgressionOrder defaultProgressionOrder)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _lossy = lossy;
            _defaultProgressionOrder = defaultProgressionOrder;
        }

        public string Name => TransferSyntax.UID.Name;

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DicomHtJpeg2000Params { Jpeg2000ProgressionOrder = _defaultProgressionOrder };
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var htParameters = DicomHtJpeg2000Params.From(parameters ?? GetDefaultParameters());
            var progressionOrder = ResolveProgressionOrder(htParameters);
            var tolerance = ResolveTolerance(htParameters);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(oldPixelData, oldPixelData.GetFrame(frame).ToArrayCopy(), _lossy, tolerance, progressionOrder);
                    newPixelData.AddFrame(new MemoryByteBuffer(encoded));
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
                return Jpeg2000ProgressionOrder.RPCL;
            }

            if (parameters.Jpeg2000ProgressionOrder == Jpeg2000ProgressionOrder.RPCL)
            {
                return _defaultProgressionOrder;
            }

            return parameters.Jpeg2000ProgressionOrder;
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, oldPixelData.GetFrame(frame).ToArrayCopy());
                    newPixelData.AddFrame(new MemoryByteBuffer(decoded));
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

            return 1;
        }

    }
}
