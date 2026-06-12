using System;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegLosslessFrameCodec
    {
        private const int DefaultSelectionValue = 1;

        public byte[] EncodeFrame(DicomPixelData pixelData, byte[] rawFrame, int selectionValue)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (rawFrame == null)
            {
                throw new ArgumentNullException(nameof(rawFrame));
            }

            ValidateSupportedPixelData(pixelData);
            var samples = BytesToSamples(rawFrame, pixelData.BitsAllocated);
            if (samples.Length != pixelData.Width * pixelData.Height)
            {
                throw CreateException($"JPEG Lossless raw frame sample count {samples.Length} does not match dimensions {pixelData.Width}x{pixelData.Height}.");
            }

            var scanCodec = JpegLosslessScanCodec.CreateDefault();
            var scan = scanCodec.Encode(samples, pixelData.Width, pixelData.Height, pixelData.BitsStored, selectionValue);

            var writer = new JpegMarkerWriter();
            writer.WriteStandalone(JpegMarker.SOI);
            writer.WriteSegment(JpegMarker.SOF3, CreateStartOfFramePayload(pixelData));
            writer.WriteSegment(JpegMarker.DHT, CreateDefaultHuffmanPayload());
            writer.WriteSegment(JpegMarker.SOS, CreateStartOfScanPayload(selectionValue));
            writer.WriteRaw(scan);
            writer.WriteStandalone(JpegMarker.EOI);
            return writer.ToArray();
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] jpegFrame, int selectionValue)
        {
            if (targetPixelData == null)
            {
                throw new ArgumentNullException(nameof(targetPixelData));
            }

            if (jpegFrame == null)
            {
                throw new ArgumentNullException(nameof(jpegFrame));
            }

            ValidateSupportedPixelData(targetPixelData);

            var parsed = ParseFrame(jpegFrame);
            if (parsed.Width != targetPixelData.Width || parsed.Height != targetPixelData.Height)
            {
                throw CreateException("JPEG Lossless frame dimensions do not match DICOM pixel data.");
            }

            if (parsed.SamplePrecision != targetPixelData.BitsStored)
            {
                throw CreateException("JPEG Lossless sample precision does not match DICOM BitsStored.");
            }

            var scanCodec = JpegLosslessScanCodec.CreateDefault();
            var samples = scanCodec.Decode(parsed.ScanData, parsed.Width, parsed.Height, parsed.SamplePrecision, parsed.SelectionValue);
            return SamplesToBytes(samples, targetPixelData.BitsAllocated);
        }

        public static int GetDefaultSelectionValue(bool firstOrderPrediction)
        {
            return firstOrderPrediction ? 1 : DefaultSelectionValue;
        }

        private static ParsedLosslessFrame ParseFrame(byte[] jpegFrame)
        {
            var reader = new JpegMarkerReader(jpegFrame);
            var soi = reader.ReadNextSkippingMetadata();
            if (soi.Code != JpegMarker.SOI)
            {
                throw CreateException("JPEG Lossless frame is missing SOI.");
            }

            JpegStartOfFrame? frame = null;
            JpegStartOfScan? scan = null;
            byte[]? scanData = null;

            while (!reader.EndOfData)
            {
                var segment = reader.ReadNextSkippingMetadata();
                switch (segment.Code)
                {
                    case JpegMarker.SOF3:
                        frame = JpegStartOfFrame.Parse(segment);
                        break;
                    case JpegMarker.DHT:
                        break;
                    case JpegMarker.SOS:
                        scan = JpegStartOfScan.Parse(segment);
                        scanData = reader.ReadEntropyDataUntilMarker(JpegMarker.EOI);
                        break;
                    case JpegMarker.EOI:
                        break;
                    default:
                        throw CreateException($"JPEG Lossless marker 0x{segment.Code:X2} is not supported.");
                }

                if (scanData != null)
                {
                    break;
                }
            }

            if (frame == null)
            {
                throw CreateException("JPEG Lossless frame is missing SOF3.");
            }

            if (frame.Components.Length != 1)
            {
                throw CreateException("JPEG Lossless currently supports only one component.");
            }

            if (scan == null || scanData == null)
            {
                throw CreateException("JPEG Lossless frame is missing SOS.");
            }

            if (scan.Components.Length != 1)
            {
                throw CreateException("JPEG Lossless currently supports only one scan component.");
            }

            return new ParsedLosslessFrame(frame.Width, frame.Height, frame.SamplePrecision, scan.SpectralSelectionStart, scanData);
        }

        private static byte[] CreateStartOfFramePayload(DicomPixelData pixelData)
        {
            return new[]
            {
                (byte)pixelData.BitsStored,
                (byte)(pixelData.Height >> 8),
                (byte)pixelData.Height,
                (byte)(pixelData.Width >> 8),
                (byte)pixelData.Width,
                (byte)1,
                (byte)1,
                (byte)0x11,
                (byte)0,
            };
        }

        private static byte[] CreateDefaultHuffmanPayload()
        {
            var payload = new byte[1 + 16 + 17];
            payload[0] = 0;
            payload[8] = 17;
            for (var index = 0; index < 17; index++)
            {
                payload[17 + index] = (byte)index;
            }

            return payload;
        }

        private static byte[] CreateStartOfScanPayload(int selectionValue)
        {
            return new[]
            {
                (byte)1,
                (byte)1,
                (byte)0,
                (byte)selectionValue,
                (byte)0,
                (byte)0,
            };
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

            if (frame.Length % 2 != 0)
            {
                throw CreateException("JPEG Lossless 16-bit frame has odd byte length.");
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

        private static void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData.SamplesPerPixel != 1)
            {
                throw CreateException($"JPEG Lossless currently supports only SamplesPerPixel 1.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw CreateException($"JPEG Lossless does not support BitsAllocated {pixelData.BitsAllocated}.");
            }

            if (pixelData.BitsStored < 2 || pixelData.BitsStored > pixelData.BitsAllocated)
            {
                throw CreateException($"JPEG Lossless BitsStored {pixelData.BitsStored} is not supported.");
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }

        private sealed class ParsedLosslessFrame
        {
            public ParsedLosslessFrame(int width, int height, int samplePrecision, int selectionValue, byte[] scanData)
            {
                Width = width;
                Height = height;
                SamplePrecision = samplePrecision;
                SelectionValue = selectionValue;
                ScanData = scanData;
            }

            public int Width { get; }

            public int Height { get; }

            public int SamplePrecision { get; }

            public int SelectionValue { get; }

            public byte[] ScanData { get; }
        }
    }
}
