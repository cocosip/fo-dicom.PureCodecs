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

            ValidateDimensions(width, height);
            ValidateComponentCount(componentCount);
            if (useYbrFull422 && componentCount != 3)
            {
                throw CreateException("JPEG YBR_FULL_422 encoding requires three components.");
            }

            if (samples.Length != width * height * componentCount)
            {
                throw CreateException($"JPEG sequential sample count {samples.Length} does not match dimensions {width}x{height}x{componentCount}.");
            }

            var quantizationTables = CreateQuantizationTables(componentCount, quality);
            CreateHuffmanTables(samples, width, height, componentCount, quantizationTables, useYbrFull422, out var dcTables, out var acTables);
            var scan = EncodeScan(samples, width, height, componentCount, quantizationTables, dcTables, acTables, useYbrFull422);

            var writer = new JpegMarkerWriter();
            writer.WriteStandalone(JpegMarker.SOI);
            writer.WriteSegment(JpegMarker.DQT, CreateQuantizationPayload(quantizationTables));
            writer.WriteSegment(_process == JpegSequentialProcess.Baseline ? JpegMarker.SOF0 : JpegMarker.SOF1, CreateStartOfFramePayload(width, height, componentCount, useYbrFull422));
            writer.WriteSegment(JpegMarker.DHT, CreateHuffmanPayload(dcTables, acTables));
            writer.WriteSegment(JpegMarker.SOS, CreateStartOfScanPayload(componentCount));
            writer.WriteRaw(scan);
            writer.WriteStandalone(JpegMarker.EOI);
            return writer.ToArray();
        }

        public byte[] Decode(byte[] jpegFrame, int expectedWidth, int expectedHeight)
        {
            return Decode(jpegFrame, expectedWidth, expectedHeight, expectedComponentCount: 1);
        }

        public byte[] Decode(byte[] jpegFrame, int expectedWidth, int expectedHeight, int expectedComponentCount)
        {
            if (jpegFrame == null)
            {
                throw new ArgumentNullException(nameof(jpegFrame));
            }

            ValidateDimensions(expectedWidth, expectedHeight);
            ValidateComponentCount(expectedComponentCount);
            var parsed = ParseFrame(jpegFrame);
            if (parsed.Width != expectedWidth || parsed.Height != expectedHeight || parsed.ComponentCount != expectedComponentCount)
            {
                throw CreateException("JPEG sequential frame shape does not match expected pixel data.");
            }

            return DecodeScan(parsed.ScanData, parsed);
        }

        private static void CreateHuffmanTables(
            byte[] samples,
            int width,
            int height,
            int componentCount,
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
            VisitQuantizedBlocks(samples, width, height, componentCount, quantizationTables, useYbrFull422, (component, zigzag) =>
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
            byte[] samples,
            int width,
            int height,
            int componentCount,
            JpegQuantizationTable[] quantizationTables,
            JpegHuffmanTable[] dcTables,
            JpegHuffmanTable[] acTables,
            bool useYbrFull422)
        {
            var bitWriter = new JpegEntropyBitWriter();
            var previousDc = new int[componentCount];
            VisitQuantizedBlocks(samples, width, height, componentCount, quantizationTables, useYbrFull422, (component, zigzag) =>
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
            byte[] samples,
            int width,
            int height,
            int componentCount,
            JpegQuantizationTable[] quantizationTables,
            bool useYbrFull422,
            Action<int, double[]> visitor)
        {
            var mcuWidth = useYbrFull422 ? 16 : 8;
            for (var blockY = 0; blockY < height; blockY += 8)
            {
                for (var blockX = 0; blockX < width; blockX += mcuWidth)
                {
                    for (var component = 0; component < componentCount; component++)
                    {
                        var horizontalBlocks = useYbrFull422 && component == 0 ? 2 : 1;
                        for (var horizontalBlock = 0; horizontalBlock < horizontalBlocks; horizontalBlock++)
                        {
                            var block = useYbrFull422 && component > 0
                                ? ReadSubsampledChromaBlock(samples, width, height, componentCount, component, blockX / 2, blockY)
                                : ReadSampleBlock(samples, width, height, componentCount, component, blockX + (horizontalBlock * 8), blockY);
                            var table = quantizationTables[GetComponentTableId(component, componentCount)];
                            visitor(component, JpegZigZag.ToZigZag(table.Quantize(JpegDct.Forward(block))));
                        }
                    }
                }
            }
        }

        private static byte[] DecodeScan(byte[] scanData, ParsedSequentialFrame frame)
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
                componentPlanes[index] = new ComponentPlane(component, width, height);
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

            var output = new byte[frame.Width * frame.Height * frame.Frame.Components.Length];
            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    for (var componentIndex = 0; componentIndex < componentPlanes.Length; componentIndex++)
                    {
                        var plane = componentPlanes[componentIndex];
                        var sampleX = Math.Min((x * plane.Width) / frame.Width, plane.Width - 1);
                        var sampleY = Math.Min((y * plane.Height) / frame.Height, plane.Height - 1);
                        output[(y * frame.Width + x) * componentPlanes.Length + componentIndex] = plane.GetSample(sampleX, sampleY);
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
            var coefficients = new double[64];
            var dcCategory = dcTable.Decode(bitReader);
            var dcDifference = dcCategory == 0 ? 0 : DecodeMagnitude(bitReader.ReadBits(dcCategory), dcCategory);
            dc = previousDc + dcDifference;
            coefficients[0] = dc;

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

                coefficients[index++] = DecodeMagnitude(bitReader.ReadBits(category), category);
            }

            return JpegDct.Inverse(quantizationTable.Dequantize(JpegZigZag.FromZigZag(coefficients)));
        }

        private static ParsedSequentialFrame ParseFrame(byte[] jpegFrame)
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

            if (frame.SamplePrecision != 8)
            {
                throw CreateException("JPEG sequential codec currently supports only 8-bit samples.");
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

        private static JpegBlock8x8 ReadSampleBlock(byte[] samples, int width, int height, int componentCount, int component, int blockX, int blockY)
        {
            var block = new JpegBlock8x8();
            for (var y = 0; y < 8; y++)
            {
                var sourceY = Math.Min(blockY + y, height - 1);
                for (var x = 0; x < 8; x++)
                {
                    var sourceX = Math.Min(blockX + x, width - 1);
                    block[y, x] = samples[(sourceY * width + sourceX) * componentCount + component] - 128;
                }
            }

            return block;
        }

        private static JpegBlock8x8 ReadSubsampledChromaBlock(byte[] samples, int width, int height, int componentCount, int component, int blockX, int blockY)
        {
            var block = new JpegBlock8x8();
            for (var y = 0; y < 8; y++)
            {
                var sourceY = Math.Min(blockY + y, height - 1);
                for (var x = 0; x < 8; x++)
                {
                    var sourceX = Math.Min((blockX + x) * 2, width - 1);
                    var nextSourceX = Math.Min(sourceX + 1, width - 1);
                    var first = samples[(sourceY * width + sourceX) * componentCount + component];
                    var second = samples[(sourceY * width + nextSourceX) * componentCount + component];
                    block[y, x] = ((first + second + 1) / 2) - 128;
                }
            }

            return block;
        }

        private static void WriteSampleBlock(byte[] output, int width, int height, int componentCount, int component, int blockX, int blockY, JpegBlock8x8 block)
        {
            for (var y = 0; y < 8 && blockY + y < height; y++)
            {
                for (var x = 0; x < 8 && blockX + x < width; x++)
                {
                    output[((blockY + y) * width + blockX + x) * componentCount + component] = ClampToByte(ToInt(block[y, x] + 128));
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

        private static byte[] CreateQuantizationPayload(JpegQuantizationTable[] tables)
        {
            var payload = new byte[tables.Length * 65];
            for (var table = 0; table < tables.Length; table++)
            {
                var zigzag = JpegZigZag.ToZigZag(tables[table].ToBlock());
                var offset = table * 65;
                payload[offset] = (byte)table;
                for (var index = 0; index < zigzag.Length; index++)
                {
                    payload[offset + index + 1] = (byte)ToInt(zigzag[index]);
                }
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

        private static byte[] CreateStartOfFramePayload(int width, int height, int componentCount, bool useYbrFull422)
        {
            var payload = new byte[6 + componentCount * 3];
            payload[0] = 8;
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

        private static byte[] CreateHuffmanPayload(JpegHuffmanTable[] dcTables, JpegHuffmanTable[] acTables)
        {
            var payloads = new byte[dcTables.Length * 2][];
            for (var table = 0; table < dcTables.Length; table++)
            {
                payloads[table * 2] = dcTables[table].CreateDhtPayload(tableClass: 0, tableId: table);
                payloads[table * 2 + 1] = acTables[table].CreateDhtPayload(tableClass: 1, tableId: table);
            }

            return CombinePayloads(payloads);
        }

        private static byte[] CombinePayloads(byte[][] payloads)
        {
            var length = 0;
            for (var index = 0; index < payloads.Length; index++)
            {
                length += payloads[index].Length;
            }

            var payload = new byte[length];
            var offset = 0;
            for (var index = 0; index < payloads.Length; index++)
            {
                Buffer.BlockCopy(payloads[index], 0, payload, offset, payloads[index].Length);
                offset += payloads[index].Length;
            }

            return payload;
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
            private readonly byte[] _samples;

            public ComponentPlane(JpegFrameComponent component, int width, int height)
            {
                Component = component;
                Width = width;
                Height = height;
                _samples = new byte[width * height];
            }

            public JpegFrameComponent Component { get; }

            public int Width { get; }

            public int Height { get; }

            public byte GetSample(int x, int y)
            {
                return _samples[y * Width + x];
            }

            public void WriteBlock(int blockX, int blockY, JpegBlock8x8 block)
            {
                for (var y = 0; y < 8 && blockY + y < Height; y++)
                {
                    for (var x = 0; x < 8 && blockX + x < Width; x++)
                    {
                        _samples[(blockY + y) * Width + blockX + x] = ClampToByte(ToInt(block[y, x] + 128));
                    }
                }
            }
        }
    }
}
