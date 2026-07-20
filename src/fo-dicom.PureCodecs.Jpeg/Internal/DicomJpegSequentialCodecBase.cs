using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public abstract class DicomJpegSequentialCodecBase : IDicomCodec
    {
        private readonly JpegSequentialDctCodec _frameCodec;

        protected DicomJpegSequentialCodecBase(DicomTransferSyntax transferSyntax, JpegSequentialProcess process)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _frameCodec = new JpegSequentialDctCodec(process);
        }

        public string Name
        {
            get { return TransferSyntax.UID.Name; }
        }

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new JpegCodecParams();
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var jpegParameters = JpegCodecParams.From(parameters);
            ValidateSupportedPixelData(oldPixelData);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encodeAsYbrFull422 = oldPixelData.SamplesPerPixel == 3
                        && oldPixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb;
                    var sourceFrame = NormalizeFrameForEncode(oldPixelData, ToArray(oldPixelData.GetFrame(frame)));
                    if (encodeAsYbrFull422)
                    {
                        sourceFrame = JpegColorConverter.RgbToYbrFull(sourceFrame);
                    }

                    var encoded = _frameCodec.Encode(
                        sourceFrame,
                        oldPixelData.Width,
                        oldPixelData.Height,
                        oldPixelData.SamplesPerPixel,
                        jpegParameters.Quality,
                        encodeAsYbrFull422);
                    newPixelData.AddFrame(new MemoryByteBuffer(encoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("encode", frame, exception);
                }
            }

            if (oldPixelData.SamplesPerPixel == 3
                && oldPixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb)
            {
                newPixelData.Dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.YbrFull422.Value);
                newPixelData.Dataset.AddOrUpdate(DicomTag.PlanarConfiguration, (ushort)PlanarConfiguration.Interleaved);
            }
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            ValidateSupportedPixelData(newPixelData);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.Decode(
                        ToArray(oldPixelData.GetFrame(frame)),
                        newPixelData.Width,
                        newPixelData.Height,
                        newPixelData.SamplesPerPixel);
                    decoded = NormalizeFrameForDecode(oldPixelData, newPixelData, decoded, JpegCodecParams.From(parameters));
                    newPixelData.AddFrame(new MemoryByteBuffer(decoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("decode", frame, exception);
                }
            }
        }

        private static void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (pixelData.BitsAllocated != 8 || pixelData.BitsStored != 8)
            {
                throw new DicomCodecException($"JPEG sequential DCT currently supports only BitsAllocated 8 and BitsStored 8.");
            }

            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw new DicomCodecException("JPEG sequential DCT currently supports only SamplesPerPixel 1 or 3.");
            }

            var photometric = pixelData.PhotometricInterpretation;
            var value = photometric?.Value;
            if (value != PhotometricInterpretation.Monochrome1.Value
                && value != PhotometricInterpretation.Monochrome2.Value
                && value != PhotometricInterpretation.Rgb.Value
                && value != "YBR_FULL"
                && value != "YBR_FULL_422")
            {
                throw new DicomCodecException($"JPEG sequential DCT does not support photometric interpretation {value ?? "<missing>"}.");
            }
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                return JpegColorConverter.PlanarRgbToInterleaved(frame, pixelData.Width * pixelData.Height);
            }

            return frame;
        }

        private static byte[] NormalizeFrameForDecode(DicomPixelData sourcePixelData, DicomPixelData targetPixelData, byte[] frame, JpegCodecParams parameters)
        {
            var photometric = sourcePixelData.PhotometricInterpretation?.Value;
            var normalized = frame;

            if (parameters.ConvertColorspaceToRGB && photometric == "YBR_FULL")
            {
                normalized = JpegColorConverter.YbrFullToRgb(normalized);
            }
            else if (parameters.ConvertColorspaceToRGB && photometric == "YBR_FULL_422")
            {
                normalized = JpegColorConverter.YbrFull422ToRgb(normalized);
            }

            if (targetPixelData.SamplesPerPixel == 3 && targetPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                normalized = JpegColorConverter.InterleavedRgbToPlanar(normalized, targetPixelData.Width * targetPixelData.Height);
            }

            return normalized;
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

    public sealed class JpegCodecParams : DicomCodecParams
    {
        public int Quality { get; set; } = 95;

        public bool ConvertColorspaceToRGB { get; set; } = true;

        public int Predictor { get; set; } = 1;

        public int PointTransform { get; set; }

        public static JpegCodecParams From(DicomCodecParams parameters)
        {
            if (parameters is JpegCodecParams jpegParameters)
            {
                return jpegParameters;
            }

            return new JpegCodecParams();
        }
    }
}
