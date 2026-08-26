using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public abstract class DicomJpegSequentialCodecBase : IDicomCodec
    {
        private readonly JpegSequentialDctCodec _frameCodec;

        private protected DicomJpegSequentialCodecBase(DicomTransferSyntax transferSyntax, JpegSequentialProcess process)
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
            ValidateEncodingParameters(jpegParameters);
            ValidateSupportedPixelData(oldPixelData);
            NormalizeEightBitContainerMetadata(oldPixelData, newPixelData);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    if (UsesTwelveBitPath(oldPixelData))
                    {
                        if (jpegParameters.SampleFactor == DicomJpegSampleFactor.SF422)
                        {
                            throw new DicomCodecException("JPEG Process 2/4 12-bit color currently supports only SF444 sampling.");
                        }

                        var encoded12Bit = _frameCodec.Encode12Bit(
                            NormalizeTwelveBitFrameForEncode(oldPixelData, ToArray(oldPixelData.GetFrame(frame))),
                            oldPixelData.Width,
                            oldPixelData.Height,
                            oldPixelData.SamplesPerPixel,
                            jpegParameters.Quality,
                            useYbrFull422: false,
                            smoothingFactor: jpegParameters.SmoothingFactor);
                        newPixelData.AddFrame(CodecOutputBuffer.Create(encoded12Bit, oldPixelData.NumberOfFrames));
                        continue;
                    }

                    var convertRgbToYbrFull = oldPixelData.SamplesPerPixel == 3
                        && (oldPixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb
                            || oldPixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422);
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
                        useYbrFull422: oldPixelData.SamplesPerPixel == 3
                            && jpegParameters.SampleFactor == DicomJpegSampleFactor.SF422,
                        smoothingFactor: jpegParameters.SmoothingFactor);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(encoded, oldPixelData.NumberOfFrames));
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
            var jpegParameters = JpegCodecParams.From(parameters);
            NormalizeEightBitContainerMetadata(newPixelData, newPixelData);
            ValidateSupportedPixelData(newPixelData);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    if (UsesTwelveBitPath(newPixelData))
                    {
                        var decoded12Bit = _frameCodec.Decode12Bit(
                            NormalizeCompressedFrameForDecode(ToArray(oldPixelData.GetFrame(frame))),
                            newPixelData.Width,
                            newPixelData.Height,
                            newPixelData.SamplesPerPixel);
                        decoded12Bit = NormalizeTwelveBitFrameForDecode(
                            oldPixelData,
                            newPixelData,
                            decoded12Bit,
                            jpegParameters);
                        newPixelData.AddFrame(CodecOutputBuffer.Create(
                            ToLittleEndianBytes(decoded12Bit),
                            oldPixelData.NumberOfFrames));
                        continue;
                    }

                    var decoded = _frameCodec.Decode(
                        NormalizeCompressedFrameForDecode(ToArray(oldPixelData.GetFrame(frame))),
                        newPixelData.Width,
                        newPixelData.Height,
                        newPixelData.SamplesPerPixel);
                    decoded = NormalizeFrameForDecode(oldPixelData, newPixelData, decoded, jpegParameters);
                    newPixelData.AddFrame(CodecOutputBuffer.Create(decoded, oldPixelData.NumberOfFrames));
                }
                catch (Exception exception)
                {
                    throw Wrap("decode", frame, exception);
                }
            }

            UpdateDecodedColorMetadata(oldPixelData, newPixelData, jpegParameters);
        }

        private void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (UsesTwelveBitPath(pixelData))
            {
                return;
            }

            if (TransferSyntax == DicomTransferSyntax.JPEGProcess1 && pixelData.BitsStored != 8)
            {
                throw new DicomCodecException($"Unable to create JPEG Process 1 codec for bits stored == {pixelData.BitsStored}");
            }

            if ((pixelData.BitsAllocated != 8 && !UsesSixteenBitContainerForEightBitSamples(pixelData))
                || pixelData.BitsStored != 8)
            {
                throw new DicomCodecException(
                    "JPEG sequential DCT currently supports BitsStored 8 in an 8- or 16-bit container, " +
                    "or Process 2/4 12-bit samples in a 16-bit container.");
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

        private static void ValidateEncodingParameters(JpegCodecParams parameters)
        {
            if (parameters.SmoothingFactor < 0 || parameters.SmoothingFactor > 100)
            {
                throw new DicomCodecException("JPEG smoothing factor must be between 0 and 100.");
            }

            if (parameters.SampleFactor == DicomJpegSampleFactor.Unknown)
            {
                throw new DicomCodecException("JPEG sample factor must be SF444 or SF422.");
            }
        }

        private bool UsesTwelveBitPath(DicomPixelData pixelData)
        {
            var photometric = pixelData.PhotometricInterpretation?.Value;
            return TransferSyntax == DicomTransferSyntax.JPEGProcess2_4 &&
                   pixelData.BitsAllocated == 16 &&
                   pixelData.BitsStored == 12 &&
                   ((pixelData.SamplesPerPixel == 1 &&
                     (photometric == PhotometricInterpretation.Monochrome1.Value ||
                      photometric == PhotometricInterpretation.Monochrome2.Value)) ||
                    (pixelData.SamplesPerPixel == 3 &&
                     (photometric == PhotometricInterpretation.Rgb.Value ||
                      photometric == PhotometricInterpretation.YbrFull.Value ||
                      photometric == PhotometricInterpretation.YbrFull422.Value)));
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            if (UsesSixteenBitContainerForEightBitSamples(pixelData))
            {
                frame = UnpackLowEightBits(frame, pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel);
            }

            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422)
            {
                if (pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
                {
                    throw new DicomCodecException("JPEG planar YBR_FULL_422 encoding is not supported.");
                }

                var rgb = ToArray(PixelDataConverter.YbrFull422ToRgb(new MemoryByteBuffer(frame), pixelData.Width));
                var expectedLength = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel;
                if (rgb.Length < expectedLength)
                {
                    throw new DicomCodecException("JPEG YBR_FULL_422 conversion produced an incomplete RGB frame.");
                }

                if (rgb.Length == expectedLength)
                {
                    return rgb;
                }

                var trimmed = new byte[expectedLength];
                Buffer.BlockCopy(rgb, 0, trimmed, 0, trimmed.Length);
                return trimmed;
            }

            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                return JpegColorConverter.PlanarRgbToInterleaved(frame, pixelData.Width * pixelData.Height);
            }

            return frame;
        }

        private static ushort[] NormalizeTwelveBitFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            var samples = ToUInt16Samples(frame);
            var expectedSampleCount = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel;
            if (samples.Length < expectedSampleCount)
            {
                throw new DicomCodecException("JPEG Process 2/4 12-bit input frame is incomplete.");
            }

            if (samples.Length != expectedSampleCount)
            {
                Array.Resize(ref samples, expectedSampleCount);
            }

            for (var index = 0; index < samples.Length; index++)
            {
                if (samples[index] > 0x0FFF)
                {
                    throw new DicomCodecException("JPEG Process 2/4 input contains a sample outside 12-bit precision.");
                }
            }

            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                samples = JpegColorConverter.PlanarToInterleaved(samples, pixelData.Width * pixelData.Height);
            }

            if (pixelData.SamplesPerPixel == 3 && pixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb)
            {
                samples = JpegColorConverter.RgbToYbrFull(samples, samplePrecision: 12);
            }

            return samples;
        }

        private static ushort[] NormalizeTwelveBitFrameForDecode(
            DicomPixelData sourcePixelData,
            DicomPixelData targetPixelData,
            ushort[] frame,
            JpegCodecParams parameters)
        {
            var photometric = sourcePixelData.PhotometricInterpretation?.Value;
            var normalized = frame;
            if (parameters.ConvertColorspaceToRGB
                && (photometric == PhotometricInterpretation.YbrFull.Value
                    || photometric == PhotometricInterpretation.YbrFull422.Value))
            {
                normalized = JpegColorConverter.YbrFullToRgb(normalized, samplePrecision: 12);
            }

            if (targetPixelData.SamplesPerPixel == 3 && targetPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                normalized = JpegColorConverter.InterleavedToPlanar(normalized, targetPixelData.Width * targetPixelData.Height);
            }

            return normalized;
        }

        private static bool UsesSixteenBitContainerForEightBitSamples(DicomPixelData pixelData)
        {
            return pixelData.BitsAllocated == 16 && pixelData.BitsStored <= 8;
        }

        private static void NormalizeEightBitContainerMetadata(DicomPixelData sourcePixelData, DicomPixelData targetPixelData)
        {
            if (UsesSixteenBitContainerForEightBitSamples(sourcePixelData))
            {
                targetPixelData.Dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)8);
            }

        }

        private static void UpdateDecodedColorMetadata(
            DicomPixelData sourcePixelData,
            DicomPixelData targetPixelData,
            JpegCodecParams parameters)
        {
            var photometric = sourcePixelData.PhotometricInterpretation?.Value;
            if (!parameters.ConvertColorspaceToRGB
                || sourcePixelData.SamplesPerPixel != 3
                || (photometric != PhotometricInterpretation.YbrFull.Value
                    && photometric != PhotometricInterpretation.YbrFull422.Value))
            {
                return;
            }

            targetPixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            targetPixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
        }

        private static byte[] UnpackLowEightBits(byte[] frame, int sampleCount)
        {
            if (frame.Length < sampleCount * 2)
            {
                throw new DicomCodecException("JPEG 16-bit DICOM container does not contain all 8-bit samples.");
            }

            var samples = new byte[sampleCount];
            for (var index = 0; index < samples.Length; index++)
            {
                samples[index] = frame[index * 2];
            }

            return samples;
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

        private static byte[] NormalizeCompressedFrameForDecode(byte[] frame)
        {
            for (var index = 0; index + 1 < frame.Length; index++)
            {
                if (frame[index] == 0xFF && frame[index + 1] == JpegMarker.EOI)
                {
                    return frame;
                }
            }

            if (frame.Length < 2
                || frame[frame.Length - 1] == JpegMarker.EOI
                || frame[frame.Length - 2] == 0xFF)
            {
                return frame;
            }

            var normalized = new byte[frame.Length + 2];
            Buffer.BlockCopy(frame, 0, normalized, 0, frame.Length);
            normalized[frame.Length] = 0xFF;
            normalized[frame.Length + 1] = JpegMarker.EOI;
            return normalized;
        }
    }

    internal class JpegCodecParams : DicomJpegParams
    {
        public JpegCodecParams()
        {
            ConvertColorspaceToRGB = true;
        }

        public static JpegCodecParams From(DicomCodecParams parameters)
        {
            if (parameters is JpegCodecParams jpegParameters)
            {
                return jpegParameters;
            }

            if (parameters is FellowOakDicom.Imaging.Codec.DicomJpegParams coreParameters)
            {
                return new JpegCodecParams
                {
                    Quality = coreParameters.Quality,
                    SmoothingFactor = coreParameters.SmoothingFactor,
                    ConvertColorspaceToRGB = coreParameters.ConvertColorspaceToRGB,
                    SampleFactor = coreParameters.SampleFactor,
                    Predictor = coreParameters.Predictor,
                    PointTransform = coreParameters.PointTransform
                };
            }

            return new JpegCodecParams();
        }
    }
}
