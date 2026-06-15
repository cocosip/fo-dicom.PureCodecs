using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public abstract class DicomJpegLsCodecBase : IDicomCodec
    {
        private readonly JpegLsFrameCodec _frameCodec = new JpegLsFrameCodec();

        protected DicomJpegLsCodecBase(DicomTransferSyntax transferSyntax)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
        }

        public string Name
        {
            get { return TransferSyntax.UID.Name; }
        }

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DicomJpegLsParams();
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var jpegLsParameters = parameters as DicomJpegLsParams ?? new DicomJpegLsParams();
            var nearLossless = TransferSyntax == DicomTransferSyntax.JPEGLSNearLossless ? jpegLsParameters.AllowedError : 0;
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(oldPixelData, ToArray(oldPixelData.GetFrame(frame)), nearLossless);
                    newPixelData.AddFrame(new MemoryByteBuffer(PadToEvenLength(encoded)));
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

        private static byte[] PadToEvenLength(byte[] frame)
        {
            if ((frame.Length & 1) == 0)
            {
                return frame;
            }

            var padded = new byte[frame.Length + 1];
            Buffer.BlockCopy(frame, 0, padded, 0, frame.Length);
            return padded;
        }
    }
}
