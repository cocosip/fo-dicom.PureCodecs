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
            UpdateCompressedPixelDataMetadata(oldPixelData, newPixelData);
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(
                        oldPixelData,
                        NormalizeFrameForEncode(oldPixelData, oldPixelData.GetFrame(frame)),
                        nearLossless,
                        GetInterleaveMode(oldPixelData));
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

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, IByteBuffer frame)
        {
            IByteBuffer normalized = new MemoryByteBuffer(ToArray(frame));
            if (pixelData.PlanarConfiguration == PlanarConfiguration.Planar && pixelData.SamplesPerPixel > 1)
            {
                if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
                {
                    throw new DicomCodecException("JPEG-LS planar YBR_FULL_422 encoding is not supported.");
                }

                if (pixelData.SamplesPerPixel != 3 || pixelData.BitsStored > 8)
                {
                    throw new DicomCodecException("JPEG-LS planar conversion supports only three-component images with BitsStored <= 8.");
                }

                normalized = PixelDataConverter.PlanarToInterleaved24(normalized);
            }

            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull)
            {
                normalized = PixelDataConverter.YbrFullToRgb(normalized);
            }
            else if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                normalized = PixelDataConverter.YbrFull422ToRgb(normalized, pixelData.Width);
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * pixelData.BytesAllocated;
            var bytes = ToArray(normalized);
            if (bytes.Length == expectedLength)
            {
                return bytes;
            }

            if (bytes.Length < expectedLength)
            {
                throw new DicomCodecException("JPEG-LS color conversion produced an incomplete RGB frame.");
            }

            var trimmed = new byte[expectedLength];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static void UpdateCompressedPixelDataMetadata(DicomPixelData source, DicomPixelData target)
        {
            if (source.SamplesPerPixel <= 1)
            {
                return;
            }

            target.PlanarConfiguration = PlanarConfiguration.Interleaved;
            if (source.PhotometricInterpretation == PhotometricInterpretation.YbrFull
                || source.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                target.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            }
        }

        private static JpegLsInterleaveMode GetInterleaveMode(DicomPixelData pixelData)
        {
            if (pixelData.SamplesPerPixel == 1)
            {
                return JpegLsInterleaveMode.None;
            }

            return pixelData.PlanarConfiguration == PlanarConfiguration.Interleaved
                ? JpegLsInterleaveMode.Sample
                : JpegLsInterleaveMode.Line;
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
