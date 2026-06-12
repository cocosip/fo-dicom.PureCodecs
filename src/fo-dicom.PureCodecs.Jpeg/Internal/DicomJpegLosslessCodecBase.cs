using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public abstract class DicomJpegLosslessCodecBase : IDicomCodec
    {
        private readonly bool _firstOrderPrediction;
        private readonly JpegLosslessFrameCodec _frameCodec = new JpegLosslessFrameCodec();

        protected DicomJpegLosslessCodecBase(DicomTransferSyntax transferSyntax, bool firstOrderPrediction)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _firstOrderPrediction = firstOrderPrediction;
        }

        public string Name
        {
            get { return TransferSyntax.UID.Name; }
        }

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new JpegLosslessCodecParams();
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var selectionValue = JpegLosslessFrameCodec.GetDefaultSelectionValue(_firstOrderPrediction);
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(oldPixelData, ToArray(oldPixelData.GetFrame(frame)), selectionValue);
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
            var selectionValue = JpegLosslessFrameCodec.GetDefaultSelectionValue(_firstOrderPrediction);
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, ToArray(oldPixelData.GetFrame(frame)), selectionValue);
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
    }

    public sealed class JpegLosslessCodecParams : DicomCodecParams
    {
    }
}
