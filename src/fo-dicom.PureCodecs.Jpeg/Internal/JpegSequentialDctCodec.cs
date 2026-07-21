using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegSequentialDctCodec
    {
        private static readonly int[] LuminanceQuantization =
        {
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68, 109, 103, 77,
            24, 35, 55, 64, 81, 104, 113, 92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103, 99,
        };

        private static readonly int[] ChrominanceQuantization =
        {
            17, 18, 24, 47, 99, 99, 99, 99,
            18, 21, 26, 66, 99, 99, 99, 99,
            24, 26, 56, 99, 99, 99, 99, 99,
            47, 66, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
        };

        private readonly JpegSequentialProcess _process;

        public JpegSequentialDctCodec(JpegSequentialProcess process)
        {
            _process = process;
        }

        public byte[] Encode(byte[] samples, int width, int height, int quality)
        {
            return Encode(samples, width, height, componentCount: 1, quality);
        }

        public byte[] Encode(byte[] samples, int width, int height, int componentCount, int quality, bool useYbrFull422 = false)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            return Encode(ToIntegerSamples(samples), width, height, componentCount, samplePrecision: 8, quality, useYbrFull422);
        }

        public byte[] Encode12Bit(ushort[] samples, int width, int height, int quality)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            return Encode(ToIntegerSamples(samples), width, height, componentCount: 1, samplePrecision: 12, quality, useYbrFull422: false);
        }

        private byte[] Encode(int[] samples, int width, int height, int componentCount, int samplePrecision, int quality, bool useYbrFull422)
        {
            ValidateDimensions(width, height);
            ValidateComponentCount(componentCount);
            ValidateSamplePrecision(samplePrecision);
            if (useYbrFull422 && componentCount != 3)
            {
                throw CreateException("JPEG YBR_FULL_422 encoding requires three components.");
            }

            if (samples.Length != width * height * componentCount)
            {
                throw CreateException($"JPEG sequential sample count {samples.Length} does not match dimensions {width}x{height}x{componentCount}.");
            }

            var quantizationTables = CreateQuantizationTables(componentCount, quality);
            CreateHuffmanTables(samples, width, height, componentCount, samplePrecision, quantizationTables, useYbrFull422, out var dcTables, out var acTables);
            var scan = EncodeScan(samples, width, height, componentCount, samplePrecision, quantizationTables, dcTables, acTables, useYbrFull422);

            var writer = new JpegMarkerWriter();
            writer.WriteStandalone(JpegMarker.SOI);
            writer.WriteSegment(JpegMarker.APP0, CreateJfifPayload());
            for (var table = 0; table < quantizationTables.Length; table++)
            {
                writer.WriteSegment(JpegMarker.DQT, CreateQuantizationPayload(table, quantizationTables[table]));
            }

            writer.WriteSegment(samplePrecision == 8 ? JpegMarker.SOF0 : JpegMarker.SOF1, CreateStartOfFramePayload(width, height, componentCount, samplePrecision, useYbrFull422));
            for (var table = 0; table < dcTables.Length; table++)
            {
                writer.WriteSegment(JpegMarker.DHT, dcTables[table].CreateDhtPayload(tableClass: 0, tableId: table));
                writer.WriteSegment(JpegMarker.DHT, acTables[table].CreateDhtPayload(tableClass: 1, tableId: table));
            }

            writer.WriteSegment(JpegMarker.SOS, CreateStartOfScanPayload(componentCount));
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

        public byte[] Decode(byte[] jpegFrame, int expectedWidth, int expectedHeight)
        {
            return Decode(jpegFrame, expectedWidth, expectedHeight, expectedComponentCount: 1);
        }

        public byte[] Decode(byte[] jpegFrame, int expectedWidth, int expectedHeight, int expectedComponentCount)
        {
            var samples = DecodeSamples(jpegFrame, expectedWidth, expectedHeight, expectedComponentCount, expectedSamplePrecision: 8);
            var output = new byte[samples.Length];
            for (var index = 0; index < samples.Length; index++)
            {
                output[index] = (byte)samples[index];
            }

            return output;
        }

        public ushort[] Decode12Bit(byte[] jpegFrame, int expectedWidth, int expectedHeight)
        {
            var samples = DecodeSamples(jpegFrame, expectedWidth, expectedHeight, expectedComponentCount: 1, expectedSamplePrecision: 12);
            var output = new ushort[samples.Length];
            for (var index = 0; index < samples.Length; index++)
            {
                output[index] = (ushort)samples[index];
            }

            return output;
        }

        private int[] DecodeSamples(byte[] jpegFrame, int expectedWidth, int expectedHeight, int expectedComponentCount, int expectedSamplePrecision)
        {
            if (jpegFrame == null)
            {
                throw new ArgumentNullException(nameof(jpegFrame));
            }

            ValidateDimensions(expectedWidth, expectedHeight);
            ValidateComponentCount(expectedComponentCount);
            ValidateSamplePrecision(expectedSamplePrecision);
            var parsed = ParseFrame(jpegFrame, expectedSamplePrecision);
            if (parsed.Width != expectedWidth || parsed.Height != expectedHeight || parsed.ComponentCount != expectedComponentCount)
            {
                throw CreateException("JPEG sequential frame shape does not match expected pixel data.");
            }

            return DecodeScan(parsed.ScanData, parsed);
        }

        private static void CreateHuffmanTables(
            int[] samples,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            JpegQuantizationTable[] quantizationTables,
            bool useYbrFull422,
            out JpegHuffmanTable[] dcTables,
            out JpegHuffmanTable[] acTables)
        {
            var tableCount = componentCount == 3 ? 2 : 1;
            var dcFrequencies = new int[tableCount][];
            var acFrequencies = new int[tableCount][];
            for (var table = 0; table < tableCount; table++)
            {
                dcFrequencies[table] = new int[256];
                acFrequencies[table] = new int[256];
            }

            var previousDc = new int[componentCount];
            VisitQuantizedBlocks(samples, width, height, componentCount, samplePrecision, quantizationTables, useYbrFull422, (component, zigzag) =>
            {
                var table = GetComponentTableId(component, componentCount);
                var dc = ToInt(zigzag[0]);
                dcFrequencies[table][GetCategory(dc - previousDc[component])]++;
                previousDc[component] = dc;

                var zeroRun = 0;
                for (var index = 1; index < zigzag.Length; index++)
                {
                    var value = ToInt(zigzag[index]);
                    if (value == 0)
                    {
                        zeroRun++;
                        continue;
                    }

                    while (zeroRun > 15)
                    {
                        acFrequencies[table][0xF0]++;
                        zeroRun -= 16;
                    }

                    acFrequencies[table][(zeroRun << 4) | GetCategory(value)]++;
                    zeroRun = 0;
                }

                if (zeroRun > 0)
                {
                    acFrequencies[table][0]++;
                }
            });

            dcTables = new JpegHuffmanTable[tableCount];
            acTables = new JpegHuffmanTable[tableCount];
            for (var table = 0; table < tableCount; table++)
            {
                dcTables[table] = JpegHuffmanTable.CreateOptimal(dcFrequencies[table]);
                acTables[table] = JpegHuffmanTable.CreateOptimal(acFrequencies[table]);
            }
        }

        private static byte[] EncodeScan(
            int[] samples,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            JpegQuantizationTable[] quantizationTables,
            JpegHuffmanTable[] dcTables,
            JpegHuffmanTable[] acTables,
            bool useYbrFull422)
        {
            var bitWriter = new JpegEntropyBitWriter();
            var previousDc = new int[componentCount];
            VisitQuantizedBlocks(samples, width, height, componentCount, samplePrecision, quantizationTables, useYbrFull422, (component, zigzag) =>
            {
                var table = GetComponentTableId(component, componentCount);
                var dc = ToInt(zigzag[0]);
                EncodeDifference(bitWriter, dcTables[table], dc - previousDc[component]);
                previousDc[component] = dc;

                var zeroRun = 0;
                for (var index = 1; index < zigzag.Length; index++)
                {
                    var value = ToInt(zigzag[index]);
                    if (value == 0)
                    {
                        zeroRun++;
                        continue;
                    }

                    while (zeroRun > 15)
                    {
                        acTables[table].Encode(bitWriter, 0xF0);
                        zeroRun -= 16;
                    }

                    var category = GetCategory(value);
                    acTables[table].Encode(bitWriter, (zeroRun << 4) | category);
                    bitWriter.WriteBits(EncodeMagnitude(value, category), category);
                    zeroRun = 0;
                }

                if (zeroRun > 0)
                {
                    acTables[table].Encode(bitWriter, 0);
                }
            });

            return bitWriter.ToArray();
        }

        private static void VisitQuantizedBlocks(
            int[] samples,
            int width,
            int height,
            int componentCount,
            int samplePrecision,
            JpegQuantizationTable[] quantizationTables,
            bool useYbrFull422,
            Action<int, double[]> visitor)
        {
            var mcuWidth = useYbrFull422 ? 16 : 8;
            var sampleBlock = new JpegBlock8x8();
            var dctBlock = new JpegBlock8x8();
            var quantizedBlock = new JpegBlock8x8();
            var dctWorkspace = new long[JpegBlock8x8.CoefficientCount];
            var zigzag = new double[JpegBlock8x8.CoefficientCount];
            for (var blockY = 0; blockY < height; blockY += 8)
            {
                for (var blockX = 0; blockX < width; blockX += mcuWidth)
                {
                    for (var component = 0; component < componentCount; component++)
                    {
                        var horizontalBlocks = useYbrFull422 && component == 0 ? 2 : 1;
                        for (var horizontalBlock = 0; horizontalBlock < horizontalBlocks; horizontalBlock++)
                        {
                            if (useYbrFull422 && component > 0)
                            {
                                ReadSubsampledChromaBlock(samples, width, height, componentCount, component, blockX / 2, blockY, samplePrecision, sampleBlock);
                            }
                            else
                            {
                                ReadSampleBlock(samples, width, height, componentCount, component, blockX + (horizontalBlock * 8), blockY, samplePrecision, sampleBlock);
                            }

                            var table = quantizationTables[GetComponentTableId(component, componentCount)];
                            JpegNativeIntegerDct.ForwardInto(sampleBlock, samplePrecision, dctBlock, dctWorkspace);
                            table.QuantizeNativeIntegerDctInto(dctBlock, quantizedBlock);
                            JpegZigZag.CopyToZigZag(quantizedBlock, zigzag);
                            visitor(component, zigzag);
                        }
                    }
                }
            }
        }

        private static int[] DecodeScan(byte[] scanData, ParsedSequentialFrame frame)
        {
            var maxHorizontal = 1;
            var maxVertical = 1;
            foreach (var component in frame.Frame.Components)
            {
                maxHorizontal = Math.Max(maxHorizontal, component.HorizontalSamplingFactor);
                maxVertical = Math.Max(maxVertical, component.VerticalSamplingFactor);
            }

            var componentPlanes = new ComponentPlane[frame.Frame.Components.Length];
            var previousDc = new int[frame.Frame.Components.Length];
            for (var index = 0; index < componentPlanes.Length; index++)
            {
                var component = frame.Frame.Components[index];
                var width = DivideRoundUp(frame.Width * component.HorizontalSamplingFactor, maxHorizontal);
                var height = DivideRoundUp(frame.Height * component.VerticalSamplingFactor, maxVertical);
                componentPlanes[index] = new ComponentPlane(component, width, height, frame.Frame.SamplePrecision);
            }

            var reader = new JpegEntropyBitReader(scanData);
            var mcuWidth = maxHorizontal * 8;
            var mcuHeight = maxVertical * 8;
            for (var mcuY = 0; mcuY < frame.Height; mcuY += mcuHeight)
            {
                for (var mcuX = 0; mcuX < frame.Width; mcuX += mcuWidth)
                {
                    for (var scanComponentIndex = 0; scanComponentIndex < frame.Scan.Components.Length; scanComponentIndex++)
                    {
                        var scanComponent = frame.Scan.Components[scanComponentIndex];
                        var planeIndex = frame.FindComponentIndex(scanComponent.Selector);
                        var plane = componentPlanes[planeIndex];
                        var component = plane.Component;
                        var dcTable = frame.GetHuffmanTable(isAc: false, scanComponent.DcTableId);
                        var acTable = frame.GetHuffmanTable(isAc: true, scanComponent.AcTableId);
                        var quantizationTable = frame.GetQuantizationTable(component.QuantizationTableId);

                        for (var vertical = 0; vertical < component.VerticalSamplingFactor; vertical++)
                        {
                            for (var horizontal = 0; horizontal < component.HorizontalSamplingFactor; horizontal++)
                            {
                                var block = DecodeBlock(reader, dcTable, acTable, quantizationTable, previousDc[planeIndex], out var dc);
                                previousDc[planeIndex] = dc;

                                var componentBlockX = ((mcuX / 8) * component.HorizontalSamplingFactor / maxHorizontal) + horizontal;
                                var componentBlockY = ((mcuY / 8) * component.VerticalSamplingFactor / maxVertical) + vertical;
                                plane.WriteBlock(componentBlockX * 8, componentBlockY * 8, block);
                            }
                        }
                    }
                }
            }

            var output = new int[frame.Width * frame.Height * frame.Frame.Components.Length];
            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    for (var componentIndex = 0; componentIndex < componentPlanes.Length; componentIndex++)
                    {
                        var plane = componentPlanes[componentIndex];
                        output[(y * frame.Width + x) * componentPlanes.Length + componentIndex] =
                            plane.GetUpsampledSample(x, y, frame.Width, frame.Height);
                    }
                }
            }

            return output;
        }

        private static JpegBlock8x8 DecodeBlock(
            JpegEntropyBitReader bitReader,
            JpegHuffmanTable dcTable,
            JpegHuffmanTable acTable,
            JpegQuantizationTable quantizationTable,
            int previousDc,
            out int dc)
        {
            var coefficients = new JpegBlock8x8();
            var dcCategory = dcTable.Decode(bitReader);
            var dcDifference = dcCategory == 0 ? 0 : DecodeMagnitude(bitReader.ReadBits(dcCategory), dcCategory);
            dc = previousDc + dcDifference;
            JpegZigZag.SetFromZigZag(coefficients, 0, dc);

            var index = 1;
            while (index < 64)
            {
                var symbol = acTable.Decode(bitReader);
                if (symbol == 0)
                {
                    break;
                }

                if (symbol == 0xF0)
                {
                    index += 16;
                    continue;
                }

                var run = symbol >> 4;
                var category = symbol & 0x0F;
                index += run;
                if (index >= 64)
                {
                    throw CreateException("JPEG sequential AC run exceeds block length.");
                }

                JpegZigZag.SetFromZigZag(coefficients, index++, DecodeMagnitude(bitReader.ReadBits(category), category));
            }

            quantizationTable.DequantizeInPlace(coefficients);
            return JpegDct.Inverse(coefficients);
        }

        private static ParsedSequentialFrame ParseFrame(byte[] jpegFrame, int expectedSamplePrecision)
        {
            var reader = new JpegMarkerReader(jpegFrame);
            var soi = reader.ReadNextSkippingMetadata();
            if (soi.Code != JpegMarker.SOI)
            {
                throw CreateException("JPEG sequential frame is missing SOI.");
            }

            JpegStartOfFrame? frame = null;
            JpegStartOfScan? scan = null;
            var quantizationTables = new JpegQuantizationTable?[4];
            var dcHuffmanTables = new JpegHuffmanTable?[4];
            var acHuffmanTables = new JpegHuffmanTable?[4];
            byte[]? scanData = null;

            while (!reader.EndOfData)
            {
                var segment = reader.ReadNextSkippingMetadata();
                switch (segment.Code)
                {
                    case JpegMarker.DQT:
                        ParseQuantizationTables(segment.Payload, quantizationTables);
                        break;
                    case JpegMarker.SOF0:
                    case JpegMarker.SOF1:
                        frame = JpegStartOfFrame.Parse(segment);
                        break;
                    case JpegMarker.DHT:
                        ParseHuffmanTables(segment.Payload, dcHuffmanTables, acHuffmanTables);
                        break;
                    case JpegMarker.SOS:
                        scan = JpegStartOfScan.Parse(segment);
                        scanData = reader.ReadEntropyDataUntilMarker(JpegMarker.EOI);
                        break;
                    case JpegMarker.EOI:
                        break;
                    default:
                        throw CreateException($"JPEG sequential marker 0x{segment.Code:X2} is not supported.");
                }

                if (scanData != null)
                {
                    break;
                }
            }

            if (frame == null)
            {
                throw CreateException("JPEG sequential frame is missing SOF.");
            }

            if (frame.SamplePrecision != expectedSamplePrecision)
            {
                throw CreateException("JPEG sequential frame sample precision does not match expected pixel data.");
            }

            ValidateComponentCount(frame.Components.Length);

            if (scan == null)
            {
                throw CreateException("JPEG sequential frame is missing SOS.");
            }

            if (scanData == null)
            {
                throw CreateException("JPEG sequential frame is missing scan data.");
            }

            var parsed = new ParsedSequentialFrame(frame.Width, frame.Height, frame.Components.Length, frame, scan, quantizationTables, dcHuffmanTables, acHuffmanTables, scanData);
            parsed.ValidateReferencedTables();
            return parsed;
        }

        private static void ReadSampleBlock(int[] samples, int width, int height, int componentCount, int component, int blockX, int blockY, int samplePrecision, JpegBlock8x8 block)
        {
            for (var y = 0; y < 8; y++)
            {
                var sourceY = Math.Min(blockY + y, height - 1);
                for (var x = 0; x < 8; x++)
                {
                    var sourceX = Math.Min(blockX + x, width - 1);
                    block[y, x] = samples[(sourceY * width + sourceX) * componentCount + component] - (1 << (samplePrecision - 1));
                }
            }

        }

        private static void ReadSubsampledChromaBlock(int[] samples, int width, int height, int componentCount, int component, int blockX, int blockY, int samplePrecision, JpegBlock8x8 block)
        {
            for (var y = 0; y < 8; y++)
            {
                var sourceY = Math.Min(blockY + y, height - 1);
                for (var x = 0; x < 8; x++)
                {
                    var sourceX = Math.Min((blockX + x) * 2, width - 1);
                    var nextSourceX = Math.Min(sourceX + 1, width - 1);
                    var first = samples[(sourceY * width + sourceX) * componentCount + component];
                    var second = samples[(sourceY * width + nextSourceX) * componentCount + component];
                    block[y, x] = ((first + second + 1) / 2) - (1 << (samplePrecision - 1));
                }
            }

        }

        private static void WriteSampleBlock(int[] output, int width, int height, int componentCount, int component, int blockX, int blockY, JpegBlock8x8 block, int samplePrecision)
        {
            for (var y = 0; y < 8 && blockY + y < height; y++)
            {
                for (var x = 0; x < 8 && blockX + x < width; x++)
                {
                    output[((blockY + y) * width + blockX + x) * componentCount + component] = ClampToSamplePrecision(ToInt(block[y, x] + (1 << (samplePrecision - 1))), samplePrecision);
                }
            }
        }

        private static byte ClampToByte(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return (byte)value;
        }

        private static int ClampToSamplePrecision(int value, int samplePrecision)
        {
            var maximum = (1 << samplePrecision) - 1;
            return Math.Max(0, Math.Min(maximum, value));
        }

        private static JpegQuantizationTable[] CreateQuantizationTables(int componentCount, int quality)
        {
            var luminance = CreateQuantizationTable(LuminanceQuantization, quality);
            return componentCount == 3
                ? new[] { luminance, CreateQuantizationTable(ChrominanceQuantization, quality) }
                : new[] { luminance };
        }

        private static JpegQuantizationTable CreateQuantizationTable(int[] source, int quality)
        {
            if (quality < 1 || quality > 100)
            {
                throw CreateException("JPEG quality must be in the range 1..100.");
            }

            var scale = quality < 50 ? 5000 / quality : 200 - quality * 2;
            var divisors = new int[64];
            for (var index = 0; index < divisors.Length; index++)
            {
                var value = (source[index] * scale + 50) / 100;
                divisors[index] = Math.Max(1, Math.Min(255, value));
            }

            return new JpegQuantizationTable(divisors);
        }

        private static byte[] CreateQuantizationPayload(int tableId, JpegQuantizationTable table)
        {
            if (tableId < 0 || tableId > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(tableId));
            }

            var payload = new byte[65];
            payload[0] = (byte)tableId;
            var zigzag = JpegZigZag.ToZigZag(table.ToBlock());
            for (var index = 0; index < zigzag.Length; index++)
            {
                payload[index + 1] = (byte)ToInt(zigzag[index]);
            }

            return payload;
        }

        private static void ParseQuantizationTables(byte[] payload, JpegQuantizationTable?[] tables)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var offset = 0;
            while (offset < payload.Length)
            {
                var info = payload[offset++];
                var precision = info >> 4;
                var id = info & 0x0F;
                if (id >= tables.Length)
                {
                    throw CreateException($"JPEG quantization table id {id} is not supported.");
                }

                var entrySize = precision == 0 ? 1 : 2;
                if (precision != 0)
                {
                    throw CreateException("JPEG sequential codec currently supports only 8-bit quantization tables.");
                }

                if (offset + 64 * entrySize > payload.Length)
                {
                    throw CreateException("JPEG quantization table payload is too short.");
                }

                var zigzag = new double[64];
                for (var index = 0; index < zigzag.Length; index++)
                {
                    zigzag[index] = payload[offset++];
                }

                var block = JpegZigZag.FromZigZag(zigzag);
                var divisors = new int[64];
                for (var index = 0; index < divisors.Length; index++)
                {
                    divisors[index] = ToInt(block[index]);
                }

                tables[id] = new JpegQuantizationTable(divisors);
            }
        }

        private static void ParseHuffmanTables(byte[] payload, JpegHuffmanTable?[] dcTables, JpegHuffmanTable?[] acTables)
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
                if (id >= dcTables.Length)
                {
                    throw CreateException($"JPEG Huffman table id {id} is not supported.");
                }

                if (offset + 16 > payload.Length)
                {
                    throw CreateException("JPEG Huffman table payload is too short.");
                }

                var counts = new byte[16];
                Buffer.BlockCopy(payload, offset, counts, 0, counts.Length);
                offset += 16;

                var valueCount = 0;
                foreach (var count in counts)
                {
                    valueCount += count;
                }

                if (offset + valueCount > payload.Length)
                {
                    throw CreateException("JPEG Huffman table values exceed payload length.");
                }

                var values = new byte[valueCount];
                Buffer.BlockCopy(payload, offset, values, 0, values.Length);
                offset += valueCount;

                if (tableClass == 0)
                {
                    dcTables[id] = JpegHuffmanTable.Build(counts, values);
                }
                else if (tableClass == 1)
                {
                    acTables[id] = JpegHuffmanTable.Build(counts, values);
                }
                else
                {
                    throw CreateException($"JPEG Huffman table class {tableClass} is not supported.");
                }
            }
        }

        private static byte[] CreateStartOfFramePayload(int width, int height, int componentCount, int samplePrecision, bool useYbrFull422)
        {
            var payload = new byte[6 + componentCount * 3];
            payload[0] = (byte)samplePrecision;
            payload[1] = (byte)(height >> 8);
            payload[2] = (byte)height;
            payload[3] = (byte)(width >> 8);
            payload[4] = (byte)width;
            payload[5] = (byte)componentCount;
            for (var component = 0; component < componentCount; component++)
            {
                var offset = 6 + component * 3;
                payload[offset] = (byte)(component + 1);
                payload[offset + 1] = useYbrFull422 && component == 0 ? (byte)0x21 : (byte)0x11;
                payload[offset + 2] = (byte)GetComponentTableId(component, componentCount);
            }

            return payload;
        }

        private static byte[] CreateStartOfScanPayload(int componentCount)
        {
            var payload = new byte[1 + componentCount * 2 + 3];
            payload[0] = (byte)componentCount;
            for (var component = 0; component < componentCount; component++)
            {
                var offset = 1 + component * 2;
                payload[offset] = (byte)(component + 1);
                var table = GetComponentTableId(component, componentCount);
                payload[offset + 1] = (byte)((table << 4) | table);
            }

            var tail = 1 + componentCount * 2;
            payload[tail] = 0;
            payload[tail + 1] = 63;
            payload[tail + 2] = 0;
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

        private static int GetComponentTableId(int component, int componentCount)
        {
            return componentCount == 3 && component > 0 ? 1 : 0;
        }

        private static void EncodeDifference(JpegEntropyBitWriter bitWriter, JpegHuffmanTable table, int difference)
        {
            var category = GetCategory(difference);
            table.Encode(bitWriter, category);
            if (category > 0)
            {
                bitWriter.WriteBits(EncodeMagnitude(difference, category), category);
            }
        }

        private static int GetCategory(int value)
        {
            var magnitude = Math.Abs(value);
            var category = 0;
            while (magnitude > 0)
            {
                category++;
                magnitude >>= 1;
            }

            return category;
        }

        private static int EncodeMagnitude(int value, int category)
        {
            return value >= 0 ? value : value + ((1 << category) - 1);
        }

        private static int DecodeMagnitude(int encoded, int category)
        {
            var threshold = 1 << (category - 1);
            return encoded >= threshold ? encoded : encoded - ((1 << category) - 1);
        }

        private static int ToInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static int DivideRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }

        private static void ValidateDimensions(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
        }

        private static void ValidateComponentCount(int componentCount)
        {
            if (componentCount != 1 && componentCount != 3)
            {
                throw CreateException($"JPEG sequential component count {componentCount} is not supported.");
            }
        }

        private void ValidateSamplePrecision(int samplePrecision)
        {
            if (samplePrecision == 8)
            {
                return;
            }

            if (_process == JpegSequentialProcess.Extended && samplePrecision == 12)
            {
                return;
            }

            throw CreateException("JPEG sequential codec supports 8-bit samples and 12-bit samples only for Process 2/4.");
        }

        private static int[] ToIntegerSamples(byte[] samples)
        {
            var output = new int[samples.Length];
            for (var index = 0; index < samples.Length; index++)
            {
                output[index] = samples[index];
            }

            return output;
        }

        private static int[] ToIntegerSamples(ushort[] samples)
        {
            var output = new int[samples.Length];
            for (var index = 0; index < samples.Length; index++)
            {
                output[index] = samples[index];
            }

            return output;
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }

        private sealed class ParsedSequentialFrame
        {
            private readonly JpegQuantizationTable?[] _quantizationTables;
            private readonly JpegHuffmanTable?[] _dcHuffmanTables;
            private readonly JpegHuffmanTable?[] _acHuffmanTables;

            public ParsedSequentialFrame(
                int width,
                int height,
                int componentCount,
                JpegStartOfFrame frame,
                JpegStartOfScan scan,
                JpegQuantizationTable?[] quantizationTables,
                JpegHuffmanTable?[] dcHuffmanTables,
                JpegHuffmanTable?[] acHuffmanTables,
                byte[] scanData)
            {
                Width = width;
                Height = height;
                ComponentCount = componentCount;
                Frame = frame;
                Scan = scan;
                _quantizationTables = quantizationTables;
                _dcHuffmanTables = dcHuffmanTables;
                _acHuffmanTables = acHuffmanTables;
                ScanData = scanData;
            }

            public int Width { get; }

            public int Height { get; }

            public int ComponentCount { get; }

            public JpegStartOfFrame Frame { get; }

            public JpegStartOfScan Scan { get; }

            public byte[] ScanData { get; }

            public int FindComponentIndex(int selector)
            {
                for (var index = 0; index < Frame.Components.Length; index++)
                {
                    if (Frame.Components[index].Identifier == selector)
                    {
                        return index;
                    }
                }

                throw CreateException($"JPEG scan references unknown component {selector}.");
            }

            public JpegQuantizationTable GetQuantizationTable(int id)
            {
                var table = id >= 0 && id < _quantizationTables.Length ? _quantizationTables[id] : null;
                return table ?? throw CreateException($"JPEG sequential frame is missing quantization table {id}.");
            }

            public JpegHuffmanTable GetHuffmanTable(bool isAc, int id)
            {
                var tables = isAc ? _acHuffmanTables : _dcHuffmanTables;
                var table = id >= 0 && id < tables.Length ? tables[id] : null;
                return table ?? throw CreateException($"JPEG sequential frame is missing {(isAc ? "AC" : "DC")} Huffman table {id}.");
            }

            public void ValidateReferencedTables()
            {
                for (var index = 0; index < Frame.Components.Length; index++)
                {
                    GetQuantizationTable(Frame.Components[index].QuantizationTableId);
                }

                for (var index = 0; index < Scan.Components.Length; index++)
                {
                    var component = Scan.Components[index];
                    FindComponentIndex(component.Selector);
                    GetHuffmanTable(isAc: false, component.DcTableId);
                    GetHuffmanTable(isAc: true, component.AcTableId);
                }
            }
        }

        private sealed class ComponentPlane
        {
            private readonly int[] _samples;
            private readonly int _samplePrecision;

            public ComponentPlane(JpegFrameComponent component, int width, int height, int samplePrecision)
            {
                Component = component;
                Width = width;
                Height = height;
                _samples = new int[width * height];
                _samplePrecision = samplePrecision;
            }

            public JpegFrameComponent Component { get; }

            public int Width { get; }

            public int Height { get; }

            public int GetSample(int x, int y)
            {
                return _samples[y * Width + x];
            }

            public int GetUpsampledSample(int x, int y, int outputWidth, int outputHeight)
            {
                var sampleY = Math.Min((y * Height) / outputHeight, Height - 1);
                if (Width * 2 == outputWidth && Height == outputHeight && Width > 2)
                {
                    var sampleX = x / 2;
                    if (x == 0 || x == outputWidth - 1)
                    {
                        return GetSample(sampleX, sampleY);
                    }

                    if ((x & 1) == 0)
                    {
                        return (GetSample(sampleX, sampleY) * 3 + GetSample(sampleX - 1, sampleY) + 1) >> 2;
                    }

                    return (GetSample(sampleX, sampleY) * 3 + GetSample(sampleX + 1, sampleY) + 2) >> 2;
                }

                var nearestSampleX = Math.Min((x * Width) / outputWidth, Width - 1);
                return GetSample(nearestSampleX, sampleY);
            }

            public void WriteBlock(int blockX, int blockY, JpegBlock8x8 block)
            {
                for (var y = 0; y < 8 && blockY + y < Height; y++)
                {
                    for (var x = 0; x < 8 && blockX + x < Width; x++)
                    {
                        _samples[(blockY + y) * Width + blockX + x] = ClampToSamplePrecision(ToInt(block[y, x] + (1 << (_samplePrecision - 1))), _samplePrecision);
                    }
                }
            }
        }
    }
}
