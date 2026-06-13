using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
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
            return new DicomHtJpeg2000Params { ProgressionOrder = _defaultProgressionOrder };
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var htParameters = DicomHtJpeg2000Params.From(parameters);
            var progressionOrder = TransferSyntax == DicomTransferSyntax.HTJ2KLosslessRPCL
                ? Jpeg2000ProgressionOrder.RPCL
                : htParameters.ProgressionOrder;
            var tolerance = ResolveTolerance(htParameters);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(oldPixelData, ToArray(oldPixelData.GetFrame(frame)), _lossy, tolerance, progressionOrder);
                    newPixelData.AddFrame(new MemoryByteBuffer(encoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("encode", frame, exception);
                }
            }
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, ToArray(oldPixelData.GetFrame(frame)));
                    newPixelData.AddFrame(new MemoryByteBuffer(decoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("decode", frame, exception);
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

        private DicomCodecException Wrap(string operation, int frame, Exception exception)
        {
            if (exception is DicomCodecException codecException)
            {
                return codecException;
            }

            return new DicomCodecException($"{TransferSyntax.UID.Name} {operation} frame {frame} failed.", exception);
        }

        private static byte[] ToArray(IByteBuffer buffer)
        {
            var bytes = new byte[buffer.Size];
            Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
