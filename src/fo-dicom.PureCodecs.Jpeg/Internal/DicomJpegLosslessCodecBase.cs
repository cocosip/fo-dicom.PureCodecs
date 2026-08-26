using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Internal;

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
            var jpegParameters = JpegCodecParams.From(parameters);
            var selectionValue = _firstOrderPrediction ? 1 : jpegParameters.Predictor;
            UpdateCompressedColorMetadata(oldPixelData, newPixelData);
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(
                        oldPixelData,
                        NormalizeFrameForEncode(oldPixelData, ToArray(oldPixelData.GetFrame(frame))),
                        selectionValue,
                        jpegParameters.PointTransform);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(encoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw Wrap("encode", frame, exception);
                }
            }
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var jpegParameters = JpegCodecParams.From(parameters);
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, ToArray(oldPixelData.GetFrame(frame)));
                    decoded = NormalizeFrameForDecode(oldPixelData, decoded, jpegParameters);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(decoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw Wrap("decode", frame, exception);
                }
            }

            UpdateDecodedColorMetadata(oldPixelData, newPixelData, jpegParameters);
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            IByteBuffer normalized = new MemoryByteBuffer(frame);
            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                if (pixelData.PlanarConfiguration == PlanarConfiguration.Planar || pixelData.BitsStored > 8)
                {
                    throw new DicomCodecException("JPEG Lossless YBR_FULL_422 encoding requires interleaved 8-bit samples.");
                }

                normalized = PixelDataConverter.YbrFull422ToRgb(normalized, pixelData.Width);
            }

            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                if (pixelData.BitsStored > 8)
                {
                    throw new DicomCodecException("JPEG Lossless planar conversion supports only 8-bit color samples.");
                }

                normalized = PixelDataConverter.PlanarToInterleaved24(normalized);
            }

            var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel * pixelData.BytesAllocated;
            var bytes = normalized.Data;
            if (bytes.Length < expectedLength)
            {
                throw new DicomCodecException("JPEG Lossless color conversion produced an incomplete frame.");
            }

            if (bytes.Length == expectedLength)
            {
                return bytes;
            }

            var trimmed = new byte[expectedLength];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static byte[] NormalizeFrameForDecode(
            DicomPixelData source,
            byte[] frame,
            JpegCodecParams parameters)
        {
            var photometric = source.PhotometricInterpretation?.Value;
            var convertToRgb = parameters.ConvertColorspaceToRGB
                || photometric != PhotometricInterpretation.Rgb.Value;
            if (!convertToRgb
                || source.SamplesPerPixel != 3
                || (photometric != PhotometricInterpretation.YbrFull.Value
                    && photometric != PhotometricInterpretation.YbrFull422.Value))
            {
                return frame;
            }

            if (source.BitsAllocated == 8)
            {
                return JpegColorConverter.YbrFullToRgb(frame);
            }

            var samples = ToUInt16Samples(frame);
            return ToLittleEndianBytes(JpegColorConverter.YbrFullToRgb(samples, source.BitsStored));
        }

        private static void UpdateCompressedColorMetadata(DicomPixelData source, DicomPixelData target)
        {
            if (source.SamplesPerPixel != 3)
            {
                return;
            }

            target.PlanarConfiguration = PlanarConfiguration.Interleaved;
            if (source.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                target.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            }
        }

        private static void UpdateDecodedColorMetadata(
            DicomPixelData source,
            DicomPixelData target,
            JpegCodecParams parameters)
        {
            var photometric = source.PhotometricInterpretation?.Value;
            if (source.SamplesPerPixel != 3
                || (!parameters.ConvertColorspaceToRGB
                    && photometric == PhotometricInterpretation.Rgb.Value)
                || (photometric != PhotometricInterpretation.YbrFull.Value
                    && photometric != PhotometricInterpretation.YbrFull422.Value))
            {
                return;
            }

            target.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            target.PlanarConfiguration = PlanarConfiguration.Interleaved;
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

        private static ushort[] ToUInt16Samples(byte[] bytes)
        {
            var samples = new ushort[bytes.Length / 2];
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = (ushort)(bytes[index * 2] | (bytes[index * 2 + 1] << 8));
            }

            return samples;
        }

        private static byte[] ToLittleEndianBytes(ushort[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (var index = 0; index < samples.Length; index++)
            {
                bytes[index * 2] = (byte)samples[index];
                bytes[index * 2 + 1] = (byte)(samples[index] >> 8);
            }

            return bytes;
        }
    }

    internal sealed class JpegLosslessCodecParams : JpegCodecParams
    {
    }
}
