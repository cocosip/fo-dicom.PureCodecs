using System;
using System.Buffers;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegLosslessFrameCodec
    {
        private const int DefaultSelectionValue = 1;

        public byte[] EncodeFrame(DicomPixelData pixelData, byte[] rawFrame, int selectionValue, int pointTransform = 0)
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
            ValidateLosslessParameters(pixelData.BitsStored, selectionValue, pointTransform);
            var effectivePrecision = pixelData.BitsStored - pointTransform;
            var pixelCount = pixelData.Width * pixelData.Height;
            var sampleCount = GetSampleCount(rawFrame, pixelData.BitsAllocated);
            if (sampleCount != pixelCount * pixelData.SamplesPerPixel)
            {
                throw CreateException($"JPEG Lossless raw frame sample count {sampleCount} does not match dimensions {pixelData.Width}x{pixelData.Height}.");
            }

            var samples = ArrayPool<int>.Shared.Rent(sampleCount);
            try
            {
                BytesToSamples(rawFrame, pixelData.BitsAllocated, samples, sampleCount);
                var interleavedSamples = ToInterleavedComponentSamples(
                    samples,
                    pixelCount,
                    pixelData.SamplesPerPixel,
                    pixelData.PlanarConfiguration);
                ApplyPointTransform(interleavedSamples, sampleCount, pointTransform);
                var huffmanTable = JpegLosslessScanCodec.CreateOptimalHuffmanTableForFrame(
                    interleavedSamples,
                    pixelData.Width,
                    pixelData.Height,
                    pixelData.SamplesPerPixel,
                    effectivePrecision,
                    selectionValue);
                var scanCodec = JpegLosslessScanCodec.Create(huffmanTable);
                var scan = scanCodec.EncodeInterleaved(
                    interleavedSamples,
                    pixelData.Width,
                    pixelData.Height,
                    pixelData.SamplesPerPixel,
                    effectivePrecision,
                    selectionValue);

                var writer = new JpegMarkerWriter();
                writer.WriteStandalone(JpegMarker.SOI);
                WriteColorSpaceMarker(writer, pixelData);
                writer.WriteSegment(JpegMarker.SOF3, CreateStartOfFramePayload(pixelData));
                writer.WriteSegment(JpegMarker.DHT, huffmanTable.CreateDhtPayload(tableClass: 0, tableId: 0));
                writer.WriteSegment(JpegMarker.SOS, CreateStartOfScanPayload(pixelData, selectionValue, pointTransform));
                writer.WriteRaw(scan);
                writer.WriteStandalone(JpegMarker.EOI);
                var frame = writer.ToArray();
                if ((frame.Length & 1) == 0)
                {
                    return frame;
                }

                var paddedFrame = new byte[frame.Length + 1];
                Buffer.BlockCopy(frame, 0, paddedFrame, 0, frame.Length);
                return paddedFrame;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(samples);
            }
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] jpegFrame)
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

            var pixelCount = parsed.Width * parsed.Height;
            var sampleCount = pixelCount * parsed.ComponentCount;
            var samples = ArrayPool<int>.Shared.Rent(sampleCount);
            try
            {
                var pointTransforms = new int[parsed.ComponentCount];
                foreach (var scan in parsed.Scans)
                {
                    var componentIndices = new int[scan.Header.Components.Length];
                    var huffmanTables = new JpegHuffmanTable[scan.Header.Components.Length];
                    for (var scanComponent = 0; scanComponent < scan.Header.Components.Length; scanComponent++)
                    {
                        var component = scan.Header.Components[scanComponent];
                        var componentIndex = parsed.FindComponentIndex(component.Selector);
                        componentIndices[scanComponent] = componentIndex;
                        huffmanTables[scanComponent] = scan.GetHuffmanTable(component.DcTableId);
                        pointTransforms[componentIndex] = scan.PointTransform;
                    }

                    JpegLosslessScanCodec.DecodeInterleavedComponents(
                        scan.Data,
                        parsed.Width,
                        parsed.Height,
                        parsed.ComponentCount,
                        componentIndices,
                        huffmanTables,
                        parsed.SamplePrecision - scan.PointTransform,
                        scan.SelectionValue,
                        samples,
                        scan.RestartInterval);
                }

                RestorePointTransforms(samples, pixelCount, parsed.ComponentCount, pointTransforms);
                var orderedSamples = FromInterleavedComponentSamples(
                    samples,
                    pixelCount,
                    parsed.ComponentCount,
                    targetPixelData.PlanarConfiguration);
                return SamplesToBytes(orderedSamples, targetPixelData.BitsAllocated, sampleCount);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(samples);
            }
        }

        public byte[] DecodeFrame(DicomPixelData targetPixelData, byte[] jpegFrame, int selectionValue)
        {
            return DecodeFrame(targetPixelData, jpegFrame);
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
            var huffmanTables = new JpegHuffmanTable?[4];
            var scans = new List<ParsedLosslessScan>();
            var restartInterval = 0;
            var reachedEndOfImage = false;

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
                    case JpegMarker.DRI:
                        restartInterval = JpegMarkerReader.ReadRestartInterval(segment.Payload);
                        break;
                    case JpegMarker.SOS:
                        var scan = JpegStartOfScan.Parse(segment);
                        scans.Add(new ParsedLosslessScan(
                            scan,
                            reader.ReadEntropyDataUntilNextMarker(),
                            huffmanTables,
                            restartInterval));
                        break;
                    case JpegMarker.EOI:
                        reachedEndOfImage = true;
                        break;
                    default:
                        if (JpegMarker.IsRestart(segment.Code))
                        {
                            throw CreateException("JPEG Lossless restart markers are not supported.");
                        }

                        throw CreateException($"JPEG Lossless marker 0x{segment.Code:X2} is not supported.");
                }

                if (reachedEndOfImage)
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

            if (!reachedEndOfImage)
            {
                throw CreateException("JPEG Lossless frame is missing EOI.");
            }

            if (scans.Count == 0)
            {
                throw CreateException("JPEG Lossless frame is missing SOS.");
            }

            var parsed = new ParsedLosslessFrame(frame, scans.ToArray());
            parsed.ValidateScans();
            return parsed;
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
                payload[offset++] = GetComponentIdentifier(pixelData, component);
                payload[offset++] = 0x11;
                payload[offset++] = 0;
            }

            return payload;
        }

        private static byte[] CreateStartOfScanPayload(DicomPixelData pixelData, int selectionValue, int pointTransform)
        {
            var payload = new byte[1 + pixelData.SamplesPerPixel * 2 + 3];
            payload[0] = (byte)pixelData.SamplesPerPixel;
            var offset = 1;
            for (var component = 0; component < pixelData.SamplesPerPixel; component++)
            {
                payload[offset++] = GetComponentIdentifier(pixelData, component);
                payload[offset++] = 0;
            }

            payload[offset++] = (byte)selectionValue;
            payload[offset++] = 0;
            payload[offset] = (byte)pointTransform;
            return payload;
        }

        private static void ApplyPointTransform(int[] samples, int sampleCount, int pointTransform)
        {
            if (pointTransform == 0)
            {
                return;
            }

            for (var index = 0; index < sampleCount; index++)
            {
                samples[index] >>= pointTransform;
            }
        }

        private static void RestorePointTransforms(
            int[] samples,
            int pixelCount,
            int componentCount,
            int[] pointTransforms)
        {
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < componentCount; component++)
                {
                    samples[pixel * componentCount + component] <<= pointTransforms[component];
                }
            }
        }

        private static void ValidateLosslessParameters(int samplePrecision, int selectionValue, int pointTransform)
        {
            if (selectionValue < 1 || selectionValue > 7)
            {
                throw CreateException($"JPEG Lossless predictor {selectionValue} is outside the supported range 1..7.");
            }

            if (pointTransform < 0 || pointTransform > 15 || pointTransform >= samplePrecision)
            {
                throw CreateException($"JPEG Lossless point transform {pointTransform} must be between 0 and {Math.Min(15, samplePrecision - 1)}.");
            }
        }

        private static void WriteColorSpaceMarker(JpegMarkerWriter writer, DicomPixelData pixelData)
        {
            if (pixelData.PhotometricInterpretation == PhotometricInterpretation.Rgb)
            {
                writer.WriteSegment(JpegMarker.APP14, CreateAdobePayload());
                return;
            }

            writer.WriteSegment(JpegMarker.APP0, CreateJfifPayload());
        }

        private static byte GetComponentIdentifier(DicomPixelData pixelData, int component)
        {
            if (pixelData.PhotometricInterpretation != PhotometricInterpretation.Rgb)
            {
                return (byte)(component + 1);
            }

            return component switch
            {
                0 => (byte)'R',
                1 => (byte)'G',
                2 => (byte)'B',
                _ => throw CreateException($"JPEG Lossless RGB component {component} is not supported."),
            };
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

        private static byte[] CreateAdobePayload()
        {
            return new byte[]
            {
                (byte)'A',
                (byte)'d',
                (byte)'o',
                (byte)'b',
                (byte)'e',
                0,
                100,
                0,
                0,
                0,
                0,
                0,
            };
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

            var interleaved = new int[pixelCount * samplesPerPixel];
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

        private static int GetSampleCount(byte[] frame, int bitsAllocated)
        {
            if (bitsAllocated == 8)
            {
                return frame.Length;
            }

            if (frame.Length % 2 != 0)
            {
                throw CreateException("JPEG Lossless 16-bit frame has odd byte length.");
            }

            return frame.Length / 2;
        }

        private static void BytesToSamples(byte[] frame, int bitsAllocated, int[] samples, int sampleCount)
        {
            if (bitsAllocated == 8)
            {
                for (var index = 0; index < sampleCount; index++)
                {
                    samples[index] = frame[index];
                }

                return;
            }

            for (var index = 0; index < sampleCount; index++)
            {
                samples[index] = frame[index * 2] | (frame[index * 2 + 1] << 8);
            }
        }

        private static byte[] SamplesToBytes(int[] samples, int bitsAllocated, int sampleCount)
        {
            if (bitsAllocated == 8)
            {
                var bytes = new byte[sampleCount];
                for (var index = 0; index < sampleCount; index++)
                {
                    bytes[index] = (byte)samples[index];
                }

                return bytes;
            }

            var output = new byte[sampleCount * 2];
            for (var index = 0; index < sampleCount; index++)
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
                JpegStartOfFrame frame,
                ParsedLosslessScan[] scans)
            {
                Frame = frame;
                Scans = scans;
            }

            public int Width => Frame.Width;

            public int Height => Frame.Height;

            public int SamplePrecision => Frame.SamplePrecision;

            public int ComponentCount => Frame.Components.Length;

            public JpegStartOfFrame Frame { get; }

            public ParsedLosslessScan[] Scans { get; }

            public int FindComponentIndex(int selector)
            {
                for (var index = 0; index < Frame.Components.Length; index++)
                {
                    if (Frame.Components[index].Identifier == selector)
                    {
                        return index;
                    }
                }

                throw CreateException($"JPEG Lossless scan references unknown component {selector}.");
            }

            public void ValidateScans()
            {
                var decodedComponents = new bool[Frame.Components.Length];
                foreach (var scan in Scans)
                {
                    if (scan.Header.Components.Length == 0
                        || scan.Header.Components.Length > Frame.Components.Length)
                    {
                        throw CreateException("JPEG Lossless scan component count is outside the frame component range.");
                    }

                    if (scan.Header.SpectralSelectionEnd != 0
                        || scan.Header.SuccessiveApproximationHigh != 0)
                    {
                        throw CreateException("JPEG Lossless scan parameters require Se=0 and Ah=0.");
                    }

                    ValidateLosslessParameters(SamplePrecision, scan.SelectionValue, scan.PointTransform);
                    foreach (var component in scan.Header.Components)
                    {
                        var componentIndex = FindComponentIndex(component.Selector);
                        if (decodedComponents[componentIndex])
                        {
                            throw CreateException($"JPEG Lossless frame contains duplicate scan coverage for component {component.Selector}.");
                        }

                        scan.GetHuffmanTable(component.DcTableId);
                        decodedComponents[componentIndex] = true;
                    }
                }

                for (var componentIndex = 0; componentIndex < decodedComponents.Length; componentIndex++)
                {
                    if (!decodedComponents[componentIndex])
                    {
                        throw CreateException($"JPEG Lossless frame is missing scan data for component {Frame.Components[componentIndex].Identifier}.");
                    }
                }
            }
        }

        private sealed class ParsedLosslessScan
        {
            private readonly JpegHuffmanTable?[] _huffmanTables;

            public ParsedLosslessScan(
                JpegStartOfScan header,
                byte[] data,
                JpegHuffmanTable?[] huffmanTables,
                int restartInterval)
            {
                Header = header;
                Data = data;
                _huffmanTables = (JpegHuffmanTable?[])huffmanTables.Clone();
                RestartInterval = restartInterval;
            }

            public JpegStartOfScan Header { get; }

            public byte[] Data { get; }

            public int RestartInterval { get; }

            public int SelectionValue => Header.SpectralSelectionStart;

            public int PointTransform => Header.SuccessiveApproximationLow;

            public JpegHuffmanTable GetHuffmanTable(int id)
            {
                var table = id >= 0 && id < _huffmanTables.Length ? _huffmanTables[id] : null;
                return table ?? JpegLosslessScanCodec.CreateDefaultHuffmanTableForFrame();
            }
        }
    }
}
