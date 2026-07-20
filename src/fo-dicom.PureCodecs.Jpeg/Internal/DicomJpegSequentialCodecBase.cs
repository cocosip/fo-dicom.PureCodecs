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
                    if (UsesTwelveBitMonochromePath(oldPixelData))
                    {
                        var encoded12Bit = _frameCodec.Encode12Bit(
                            ToUInt16Samples(ToArray(oldPixelData.GetFrame(frame))),
                            oldPixelData.Width,
                            oldPixelData.Height,
                            jpegParameters.Quality);
                        newPixelData.AddFrame(new MemoryByteBuffer(encoded12Bit));
                        continue;
                    }

                    var convertRgbToYbrFull = oldPixelData.SamplesPerPixel == 3
                        && oldPixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb;
                    var sourceFrame = NormalizeFrameForEncode(oldPixelData, ToArray(oldPixelData.GetFrame(frame)));
                    if (convertRgbToYbrFull)
                    {
                        sourceFrame = JpegColorConverter.RgbToYbrFull(sourceFrame);
                    }

                    var encoded = _frameCodec.Encode(
                        sourceFrame,
                        oldPixelData.Width,
                        oldPixelData.Height,
                        oldPixelData.SamplesPerPixel,
                        jpegParameters.Quality,
                        useYbrFull422: false);
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
                    if (UsesTwelveBitMonochromePath(newPixelData))
                    {
                        var decoded12Bit = _frameCodec.Decode12Bit(
                            ToArray(oldPixelData.GetFrame(frame)),
                            newPixelData.Width,
                            newPixelData.Height);
                        newPixelData.AddFrame(new MemoryByteBuffer(ToLittleEndianBytes(decoded12Bit)));
                        continue;
                    }

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

        private void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (UsesTwelveBitMonochromePath(pixelData))
            {
                return;
            }

            if (TransferSyntax == DicomTransferSyntax.JPEGProcess1 && pixelData.BitsStored != 8)
            {
                throw new DicomCodecException($"Unable to create JPEG Process 1 codec for bits stored == {pixelData.BitsStored}");
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
                && value != PhotometricInterpretation.PaletteColor.Value
                && value != PhotometricInterpretation.Rgb.Value
                && value != "YBR_FULL"
                && value != "YBR_FULL_422")
            {
                throw new DicomCodecException($"JPEG sequential DCT does not support photometric interpretation {value ?? "<missing>"}.");
            }
        }

        private bool UsesTwelveBitMonochromePath(DicomPixelData pixelData)
        {
            var photometric = pixelData.PhotometricInterpretation?.Value;
            return TransferSyntax == DicomTransferSyntax.JPEGProcess2_4 &&
                   pixelData.BitsAllocated == 16 &&
                   pixelData.BitsStored == 12 &&
                   pixelData.SamplesPerPixel == 1 &&
                   (photometric == PhotometricInterpretation.Monochrome1.Value ||
                    photometric == PhotometricInterpretation.Monochrome2.Value);
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
                normalized = JpegColorConverter.YbrFullToRgb(normalized);
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

        private static ushort[] ToUInt16Samples(byte[] bytes)
        {
            if ((bytes.Length & 1) != 0)
            {
                throw new DicomCodecException("JPEG Process 2/4 12-bit input must use a 16-bit DICOM sample container.");
            }

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

    public sealed class JpegCodecParams : DicomCodecParams
    {
        public int Quality { get; set; } = 90;

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
