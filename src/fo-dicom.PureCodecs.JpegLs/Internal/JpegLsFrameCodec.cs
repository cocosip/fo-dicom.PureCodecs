using System;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsFrameCodec
    {
        public byte[] EncodeFrame(
            DicomPixelData pixelData,
            byte[] rawFrame,
            int nearLossless,
            JpegLsInterleaveMode interleaveMode)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (rawFrame == null)
            {
                throw new ArgumentNullException(nameof(rawFrame));
            }

            ValidateSupportedPixelData(pixelData, nearLossless);
            var samples = BytesToSamples(rawFrame, pixelData.BitsAllocated);
            var expectedSamples = pixelData.Width * pixelData.Height * pixelData.SamplesPerPixel;
            if (samples.Length != expectedSamples)
            {
                throw CreateException($"JPEG-LS raw frame sample count {samples.Length} does not match expected count {expectedSamples}.");
            }

            var scanCodec = new JpegLsScanCodec(
                pixelData.Width,
                pixelData.Height,
                pixelData.SamplesPerPixel,
                pixelData.BitsStored,
                nearLossless,
                interleaveMode);
            var scan = scanCodec.Encode(samples);

            var writer = new JpegLsMarkerWriter();
            writer.WriteStandalone(JpegLsMarker.SOI);
            writer.WriteSegment(JpegLsMarker.SOF55, CreateStartOfFramePayload(pixelData));
            writer.WriteSegment(JpegLsMarker.LSE, CreatePresetCodingParametersPayload(pixelData.BitsStored, nearLossless));
            writer.WriteSegment(JpegLsMarker.SOS, CreateStartOfScanPayload(pixelData.SamplesPerPixel, nearLossless, interleaveMode));
            writer.WriteRaw(scan);
            writer.WriteStandalone(JpegLsMarker.EOI);
            return writer.ToArray();
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] jpegLsFrame)
        {
            if (targetPixelData == null)
            {
                throw new ArgumentNullException(nameof(targetPixelData));
            }

            if (jpegLsFrame == null)
            {
                throw new ArgumentNullException(nameof(jpegLsFrame));
            }

            ValidateSupportedPixelData(targetPixelData, nearLossless: 0);
            var parsed = ParseFrame(jpegLsFrame);
            if (parsed.FrameInfo.Width != targetPixelData.Width || parsed.FrameInfo.Height != targetPixelData.Height)
            {
                throw CreateException("JPEG-LS frame dimensions do not match DICOM pixel data.");
            }

            if (parsed.FrameInfo.BitsPerSample != targetPixelData.BitsStored)
            {
                throw CreateException("JPEG-LS frame sample precision does not match DICOM BitsStored.");
            }

            if (parsed.FrameInfo.Components.Count != targetPixelData.SamplesPerPixel)
            {
                throw CreateException("JPEG-LS component count does not match DICOM SamplesPerPixel.");
            }

            var scanCodec = new JpegLsScanCodec(parsed.FrameInfo.Width, parsed.FrameInfo.Height, parsed.FrameInfo.Components.Count, parsed.FrameInfo.BitsPerSample, parsed.Scan.NearLossless, parsed.Scan.InterleaveMode);
            var samples = scanCodec.Decode(parsed.ScanData);
            return SamplesToBytes(samples, targetPixelData.BitsAllocated);
        }

        private static ParsedJpegLsFrame ParseFrame(byte[] jpegLsFrame)
        {
            var reader = new JpegLsMarkerReader(jpegLsFrame);
            var soi = reader.ReadNextSkippingMetadata();
            if (soi.Code != JpegLsMarker.SOI)
            {
                throw CreateException("JPEG-LS frame is missing SOI.");
            }

            JpegLsFrameInfo? frameInfo = null;
            JpegLsStartOfScan? scan = null;
            byte[]? scanData = null;

            while (!reader.EndOfData)
            {
                var segment = reader.ReadNextSkippingMetadata();
                switch (segment.Code)
                {
                    case JpegLsMarker.SOF55:
                        frameInfo = JpegLsFrameInfo.Parse(segment);
                        break;
                    case JpegLsMarker.LSE:
                        _ = JpegLsPresetCodingParameters.Parse(segment);
                        break;
                    case JpegLsMarker.SOS:
                        scan = JpegLsStartOfScan.Parse(segment);
                        scanData = reader.ReadEntropyDataUntilMarker(JpegLsMarker.EOI);
                        break;
                    case JpegLsMarker.EOI:
                        break;
                    default:
                        throw CreateException($"JPEG-LS marker 0x{segment.Code:X2} is not supported.");
                }

                if (scanData != null)
                {
                    break;
                }
            }

            if (frameInfo == null)
            {
                throw CreateException("JPEG-LS frame is missing SOF55.");
            }

            if (scan == null || scanData == null)
            {
                throw CreateException("JPEG-LS frame is missing SOS.");
            }

            return new ParsedJpegLsFrame(frameInfo, scan, scanData);
        }

        private static byte[] CreateStartOfFramePayload(DicomPixelData pixelData)
        {
            var payload = new byte[6 + pixelData.SamplesPerPixel * 3];
            payload[0] = (byte)pixelData.BitsStored;
            payload[1] = (byte)(pixelData.Height >> 8);
            payload[2] = (byte)pixelData.Height;
            payload[3] = (byte)(pixelData.Width >> 8);
            payload[4] = (byte)pixelData.Width;
            payload[5] = (byte)pixelData.SamplesPerPixel;

            for (var component = 0; component < pixelData.SamplesPerPixel; component++)
            {
                var offset = 6 + component * 3;
                payload[offset] = (byte)(component + 1);
                payload[offset + 1] = 0x11;
                payload[offset + 2] = 0;
            }

            return payload;
        }

        private static byte[] CreatePresetCodingParametersPayload(int bitsStored, int nearLossless)
        {
            var maximumSampleValue = (1 << bitsStored) - 1;
            var traits = JpegLsTraits.CreateDefault(maximumSampleValue, nearLossless, resetThreshold: 64);
            return new[]
            {
                (byte)1,
                (byte)(maximumSampleValue >> 8),
                (byte)maximumSampleValue,
                (byte)(traits.Threshold1 >> 8),
                (byte)traits.Threshold1,
                (byte)(traits.Threshold2 >> 8),
                (byte)traits.Threshold2,
                (byte)(traits.Threshold3 >> 8),
                (byte)traits.Threshold3,
                (byte)(traits.ResetThreshold >> 8),
                (byte)traits.ResetThreshold,
            };
        }

        private static byte[] CreateStartOfScanPayload(int samplesPerPixel, int nearLossless, JpegLsInterleaveMode interleaveMode)
        {
            var payload = new byte[1 + samplesPerPixel * 2 + 3];
            payload[0] = (byte)samplesPerPixel;
            for (var component = 0; component < samplesPerPixel; component++)
            {
                var offset = 1 + component * 2;
                payload[offset] = (byte)(component + 1);
                payload[offset + 1] = 0;
            }

            payload[payload.Length - 3] = (byte)nearLossless;
            payload[payload.Length - 2] = (byte)interleaveMode;
            payload[payload.Length - 1] = 0;
            return payload;
        }

        private static int[] BytesToSamples(byte[] frame, int bitsAllocated)
        {
            if (bitsAllocated == 8)
            {
                var samples = new int[frame.Length];
                for (var index = 0; index < samples.Length; index++)
                {
                    samples[index] = frame[index];
                }

                return samples;
            }

            if (bitsAllocated != 16 || frame.Length % 2 != 0)
            {
                throw CreateException("JPEG-LS frame byte layout is invalid.");
            }

            var values = new int[frame.Length / 2];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = frame[index * 2] | (frame[index * 2 + 1] << 8);
            }

            return values;
        }

        private static byte[] SamplesToBytes(int[] samples, int bitsAllocated)
        {
            if (bitsAllocated == 8)
            {
                var bytes = new byte[samples.Length];
                for (var index = 0; index < samples.Length; index++)
                {
                    bytes[index] = (byte)samples[index];
                }

                return bytes;
            }

            var output = new byte[samples.Length * 2];
            for (var index = 0; index < samples.Length; index++)
            {
                output[index * 2] = (byte)samples[index];
                output[index * 2 + 1] = (byte)(samples[index] >> 8);
            }

            return output;
        }

        private static void ValidateSupportedPixelData(DicomPixelData pixelData, int nearLossless)
        {
            if (nearLossless < 0 || nearLossless > 255)
            {
                throw CreateException("JPEG-LS AllowedError must be between 0 and 255.");
            }

            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw CreateException($"JPEG-LS does not support SamplesPerPixel {pixelData.SamplesPerPixel}.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw CreateException($"JPEG-LS does not support BitsAllocated {pixelData.BitsAllocated}.");
            }

            if (pixelData.BitsStored < 2 || pixelData.BitsStored > pixelData.BitsAllocated)
            {
                throw CreateException($"JPEG-LS BitsStored {pixelData.BitsStored} is not supported.");
            }

            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrPartial422
                || pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrPartial420)
            {
                throw CreateException($"Photometric Interpretation {pixelData.PhotometricInterpretation} is not supported by JPEG-LS.");
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }

        private sealed class ParsedJpegLsFrame
        {
            public ParsedJpegLsFrame(JpegLsFrameInfo frameInfo, JpegLsStartOfScan scan, byte[] scanData)
            {
                FrameInfo = frameInfo;
                Scan = scan;
                ScanData = scanData;
            }

            public JpegLsFrameInfo FrameInfo { get; }

            public JpegLsStartOfScan Scan { get; }

            public byte[] ScanData { get; }
        }
    }
}
