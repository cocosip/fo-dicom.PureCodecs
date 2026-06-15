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
            var pixelCount = pixelData.Width * pixelData.Height;
            if (samples.Length != pixelCount * pixelData.SamplesPerPixel)
            {
                throw CreateException($"JPEG Lossless raw frame sample count {samples.Length} does not match dimensions {pixelData.Width}x{pixelData.Height}.");
            }

            var interleavedSamples = ToInterleavedComponentSamples(
                samples,
                pixelCount,
                pixelData.SamplesPerPixel,
                pixelData.PlanarConfiguration);
            var huffmanTable = CreateReferenceLosslessHuffmanTable(pixelData.BitsStored);
            var scanCodec = JpegLosslessScanCodec.Create(huffmanTable);
            var scan = scanCodec.EncodeInterleaved(
                interleavedSamples,
                pixelData.Width,
                pixelData.Height,
                pixelData.SamplesPerPixel,
                pixelData.BitsStored,
                selectionValue);

            var writer = new JpegMarkerWriter();
            writer.WriteStandalone(JpegMarker.SOI);
            writer.WriteSegment(JpegMarker.APP0, CreateJfifPayload());
            writer.WriteSegment(JpegMarker.SOF3, CreateStartOfFramePayload(pixelData));
            writer.WriteSegment(JpegMarker.DHT, huffmanTable.CreateDhtPayload(tableClass: 0, tableId: 0));
            writer.WriteSegment(JpegMarker.SOS, CreateStartOfScanPayload(pixelData.SamplesPerPixel, selectionValue));
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

            var parsed = ParseFrame(jpegFrame, targetPixelData.SamplesPerPixel);
            if (parsed.Width != targetPixelData.Width || parsed.Height != targetPixelData.Height)
            {
                throw CreateException("JPEG Lossless frame dimensions do not match DICOM pixel data.");
            }

            if (parsed.SamplePrecision != targetPixelData.BitsStored)
            {
                throw CreateException("JPEG Lossless sample precision does not match DICOM BitsStored.");
            }

            var scanCodec = JpegLosslessScanCodec.Create(parsed.HuffmanTable);
            var samples = scanCodec.DecodeInterleaved(
                parsed.ScanData,
                parsed.Width,
                parsed.Height,
                parsed.Components,
                parsed.SamplePrecision,
                parsed.SelectionValue);
            samples = FromInterleavedComponentSamples(
                samples,
                parsed.Width * parsed.Height,
                parsed.Components,
                targetPixelData.PlanarConfiguration);
            return SamplesToBytes(samples, targetPixelData.BitsAllocated);
        }

        public static int GetDefaultSelectionValue(bool firstOrderPrediction)
        {
            return firstOrderPrediction ? 1 : DefaultSelectionValue;
        }

        private static ParsedLosslessFrame ParseFrame(byte[] jpegFrame, int expectedComponents)
        {
            var reader = new JpegMarkerReader(jpegFrame);
            var soi = reader.ReadNextSkippingMetadata();
            if (soi.Code != JpegMarker.SOI)
            {
                throw CreateException("JPEG Lossless frame is missing SOI.");
            }

            JpegStartOfFrame? frame = null;
            JpegStartOfScan? scan = null;
            var huffmanTables = new JpegHuffmanTable?[4];
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
                        ParseHuffmanTables(segment.Payload, huffmanTables);
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

            if (frame.Components.Length != expectedComponents)
            {
                throw CreateException("JPEG Lossless frame component count does not match DICOM SamplesPerPixel.");
            }

            if (scan == null || scanData == null)
            {
                throw CreateException("JPEG Lossless frame is missing SOS.");
            }

            if (scan.Components.Length != expectedComponents)
            {
                throw CreateException("JPEG Lossless scan component count does not match DICOM SamplesPerPixel.");
            }

            return new ParsedLosslessFrame(
                frame.Width,
                frame.Height,
                frame.SamplePrecision,
                frame.Components.Length,
                scan.SpectralSelectionStart,
                ResolveHuffmanTable(scan, huffmanTables),
                scanData);
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

            var offset = 6;
            for (var component = 0; component < pixelData.SamplesPerPixel; component++)
            {
                payload[offset++] = (byte)(component + 1);
                payload[offset++] = 0x11;
                payload[offset++] = 0;
            }

            return payload;
        }

        private static byte[] CreateStartOfScanPayload(int samplesPerPixel, int selectionValue)
        {
            var payload = new byte[1 + samplesPerPixel * 2 + 3];
            payload[0] = (byte)samplesPerPixel;
            var offset = 1;
            for (var component = 0; component < samplesPerPixel; component++)
            {
                payload[offset++] = (byte)(component + 1);
                payload[offset++] = 0;
            }

            payload[offset++] = (byte)selectionValue;
            payload[offset++] = 0;
            payload[offset] = 0;
            return payload;
        }

        private static byte[] CreateJfifPayload()
        {
            return new byte[]
            {
                (byte)'J',
                (byte)'F',
                (byte)'I',
                (byte)'F',
                0,
                1,
                1,
                0,
                0,
                1,
                0,
                1,
                0,
                0
            };
        }

        private static JpegHuffmanTable CreateReferenceLosslessHuffmanTable(int bitsStored)
        {
            if (bitsStored >= 12)
            {
                return JpegHuffmanTable.Build(
                    new byte[] { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 2 },
                    new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            }

            return JpegLosslessScanCodec.CreateDefaultHuffmanTableForFrame();
        }

        private static void ParseHuffmanTables(byte[] payload, JpegHuffmanTable?[] tables)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var offset = 0;
            while (offset < payload.Length)
            {
                var info = payload[offset++];
                var tableClass = info >> 4;
                var id = info & 0x0F;
                if (id >= tables.Length)
                {
                    throw CreateException($"JPEG Lossless Huffman table id {id} is not supported.");
                }

                if (offset + 16 > payload.Length)
                {
                    throw CreateException("JPEG Lossless Huffman table payload is too short.");
                }

                var counts = new byte[16];
                Buffer.BlockCopy(payload, offset, counts, 0, counts.Length);
                offset += counts.Length;

                var valueCount = 0;
                foreach (var count in counts)
                {
                    valueCount += count;
                }

                if (offset + valueCount > payload.Length)
                {
                    throw CreateException("JPEG Lossless Huffman table values exceed payload length.");
                }

                var values = new byte[valueCount];
                Buffer.BlockCopy(payload, offset, values, 0, values.Length);
                offset += valueCount;

                if (tableClass == 0)
                {
                    tables[id] = JpegHuffmanTable.Build(counts, values);
                }
            }
        }

        private static JpegHuffmanTable ResolveHuffmanTable(JpegStartOfScan scan, JpegHuffmanTable?[] tables)
        {
            var tableId = scan.Components[0].DcTableId;
            foreach (var component in scan.Components)
            {
                if (component.DcTableId != tableId)
                {
                    throw CreateException("JPEG Lossless currently supports one DC Huffman table per scan.");
                }
            }

            var table = tableId >= 0 && tableId < tables.Length ? tables[tableId] : null;
            return table ?? JpegLosslessScanCodec.CreateDefaultHuffmanTableForFrame();
        }

        private static int[] ToInterleavedComponentSamples(
            int[] samples,
            int pixelCount,
            int samplesPerPixel,
            PlanarConfiguration planarConfiguration)
        {
            if (samplesPerPixel == 1 || planarConfiguration == PlanarConfiguration.Interleaved)
            {
                return samples;
            }

            var interleaved = new int[samples.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < samplesPerPixel; component++)
                {
                    interleaved[pixel * samplesPerPixel + component] = samples[component * pixelCount + pixel];
                }
            }

            return interleaved;
        }

        private static int[] FromInterleavedComponentSamples(
            int[] samples,
            int pixelCount,
            int samplesPerPixel,
            PlanarConfiguration planarConfiguration)
        {
            if (samplesPerPixel == 1 || planarConfiguration == PlanarConfiguration.Interleaved)
            {
                return samples;
            }

            var planar = new int[samples.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < samplesPerPixel; component++)
                {
                    planar[component * pixelCount + pixel] = samples[pixel * samplesPerPixel + component];
                }
            }

            return planar;
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
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw CreateException($"JPEG Lossless supports only SamplesPerPixel 1 or 3.");
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
            public ParsedLosslessFrame(
                int width,
                int height,
                int samplePrecision,
                int components,
                int selectionValue,
                JpegHuffmanTable huffmanTable,
                byte[] scanData)
            {
                Width = width;
                Height = height;
                SamplePrecision = samplePrecision;
                Components = components;
                SelectionValue = selectionValue;
                HuffmanTable = huffmanTable;
                ScanData = scanData;
            }

            public int Width { get; }

            public int Height { get; }

            public int SamplePrecision { get; }

            public int Components { get; }

            public int SelectionValue { get; }

            public JpegHuffmanTable HuffmanTable { get; }

            public byte[] ScanData { get; }
        }
    }
}
