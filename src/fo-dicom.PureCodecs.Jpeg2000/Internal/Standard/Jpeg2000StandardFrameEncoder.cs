using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardFrameEncoder
    {
        private const int DefaultLevels = 5;
        private const int CodeBlockWidth = 64;
        private const int CodeBlockHeight = 64;
        private const int DefaultIrreversibleQuality = 80;
        private const int Tier1FractionalBits = 6;
        private const double DoubleMachineEpsilon = 2.2204460492503131E-16;

        public byte[] Encode(
            DicomPixelData pixelData,
            byte[] frame,
            bool irreversible,
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform,
            bool encodeSignedPixelValuesAsUnsigned,
            double rate,
            double[]? layerRates = null)
        {
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 standard encoder currently supports monochrome and RGB frames only.");
            }

            if (progressionOrder != Jpeg2000ProgressionOrder.LRCP)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 standard encoder currently supports LRCP progression only.");
            }

            var width = pixelData.Width;
            var height = pixelData.Height;
            var precision = pixelData.BitsStored;
            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed && !encodeSignedPixelValuesAsUnsigned;
            var components = ReadComponentSamples(frame, pixelData.SamplesPerPixel, pixelData.BitsAllocated, precision, isSigned, encodeSignedPixelValuesAsUnsigned);
            var rateControlSourceByteLength = CalculateRateControlSourceByteLength(frame.Length, precision, pixelData.BitsAllocated);
            ApplyForwardLevelShift(components, precision, isSigned);

            var irreversibleSteps = irreversible ? Jpeg2000QuantizationTable.CreateIrreversibleSteps(DefaultLevels, DefaultIrreversibleQuality) : Array.Empty<double>();
            var irreversibleEncodedSteps = irreversible ? EncodeIrreversibleSteps(irreversibleSteps, precision) : Array.Empty<ushort>();
            var mainHeaderBytesBeforeSot = EstimateMainHeaderBytesBeforeSot(pixelData.SamplesPerPixel, irreversible);
            var tileData = irreversible
                ? EncodeIrreversibleComponents(components, width, height, precision, irreversibleEncodedSteps, usesMultipleComponentTransform, rateControlSourceByteLength, layerCount, layerRates, mainHeaderBytesBeforeSot)
                : EncodeReversibleComponents(components, width, height, precision, usesMultipleComponentTransform, layerCount, rateControlSourceByteLength, layerRates, mainHeaderBytesBeforeSot);
            var writer = new Jpeg2000CodestreamWriter();
            writer.WriteStandalone(Jpeg2000Marker.SOC);
            writer.WriteSegment(Jpeg2000Marker.SIZ, Jpeg2000MarkerPayloadBuilder.CreateSize(width, height, precision, isSigned, pixelData.SamplesPerPixel));
            writer.WriteSegment(Jpeg2000Marker.COD, CreateCodingStylePayload(irreversible, progressionOrder, layerCount, usesMultipleComponentTransform));
            writer.WriteSegment(
                Jpeg2000Marker.QCD,
                irreversible
                    ? Jpeg2000MarkerPayloadBuilder.CreateIrreversibleQuantization(precision, irreversibleSteps)
                    : Jpeg2000MarkerPayloadBuilder.CreateReversibleQuantization(precision, DefaultLevels));
            writer.WriteSegment(Jpeg2000Marker.COM, CreateCommentPayload());
            writer.WriteSegment(Jpeg2000Marker.SOT, Jpeg2000MarkerPayloadBuilder.CreateStartOfTile(tileData.Length));
            writer.WriteStandalone(Jpeg2000Marker.SOD);
            writer.WriteRaw(tileData);
            writer.WriteStandalone(Jpeg2000Marker.EOC);
            return PadToEvenLength(writer.ToArray());
        }

        private static byte[] EncodeReversibleTile(int[] samples, int width, int height, int precision)
        {
            return EncodeReversibleTileLayered(samples, width, height, precision, layerCount: 1);
        }

        private static byte[] EncodeReversibleTileLayered(int[] samples, int width, int height, int precision, int layerCount, int sourceByteLength = 0, double[]? layerRates = null, int mainHeaderBytesBeforeSot = 0)
        {
            var coefficients = Jpeg2000StandardWavelet.Forward53(samples, width, height, DefaultLevels, 0, 0);
            return EncodePackets(coefficients, width, height, precision, reversible: true, layerCount: layerCount, sourceByteLength: sourceByteLength, layerRates: layerRates, mainHeaderBytesBeforeSot: mainHeaderBytesBeforeSot);
        }

        private static byte[] EncodeIrreversibleTile(int[] samples, int width, int height, int precision, double[] steps, ushort[] encodedSteps, double rate, int sourceByteLength, int layerCount = 1, double[]? layerRates = null, int mainHeaderBytesBeforeSot = 0)
        {
            var values = new double[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                values[i] = samples[i];
            }

            Jpeg2000StandardIrreversibleWavelet.Forward97(values, width, height, DefaultLevels);
            var coefficients = QuantizeIrreversibleOpenJpeg(values, width, height, encodedSteps, precision);
            return EncodePackets(coefficients, width, height, precision, reversible: false, layerCount: layerCount, sourceByteLength: sourceByteLength, layerRates: layerRates, mainHeaderBytesBeforeSot: mainHeaderBytesBeforeSot, encodedSteps: encodedSteps);
        }

        private static byte[] EncodeReversibleComponents(int[][] components, int width, int height, int precision, bool usesMultipleComponentTransform, int layerCount, int sourceByteLength, double[]? layerRates, int mainHeaderBytesBeforeSot)
        {
            if (components.Length == 3 && usesMultipleComponentTransform)
            {
                ApplyForwardRct(components);
            }

            var encodedComponents = new List<List<List<Jpeg2000EncodedBlock>>>(components.Length);
            foreach (var component in components)
            {
                var coefficients = Jpeg2000StandardWavelet.Forward53(component, width, height, DefaultLevels, 0, 0);
                encodedComponents.Add(BuildCodeBlocksByResolution(
                    coefficients,
                    width,
                    height,
                    precision,
                    reversible: true,
                    encodedSteps: null,
                    componentIndex: encodedComponents.Count,
                    mctNorms: usesMultipleComponentTransform ? ReversibleMctNorms() : null));
            }

            return EncodePackets(encodedComponents, layerCount, sourceByteLength, layerRates, mainHeaderBytesBeforeSot, reversible: true);
        }

        private static byte[] EncodeIrreversibleComponents(int[][] components, int width, int height, int precision, ushort[] encodedSteps, bool usesMultipleComponentTransform, int sourceByteLength, int layerCount, double[]? layerRates, int mainHeaderBytesBeforeSot)
        {
            var values = CreateDoubleComponents(components);
            if (values.Length == 3 && usesMultipleComponentTransform)
            {
                ApplyForwardIct(values);
            }

            var encodedComponents = new List<List<List<Jpeg2000EncodedBlock>>>(values.Length);
            foreach (var component in values)
            {
                Jpeg2000StandardIrreversibleWavelet.Forward97(component, width, height, DefaultLevels);
                var coefficients = QuantizeIrreversibleOpenJpeg(component, width, height, encodedSteps, precision);
                encodedComponents.Add(BuildCodeBlocksByResolution(
                    coefficients,
                    width,
                    height,
                    precision,
                    reversible: false,
                    encodedSteps: encodedSteps,
                    componentIndex: encodedComponents.Count,
                    mctNorms: usesMultipleComponentTransform ? IrreversibleMctNorms() : null));
            }

            return EncodePackets(encodedComponents, layerCount, sourceByteLength, layerRates, mainHeaderBytesBeforeSot, reversible: false);
        }

        private static int CalculateRateControlSourceByteLength(int frameByteLength, int bitsStored, int bitsAllocated)
        {
            if (frameByteLength <= 0 || bitsStored <= 0 || bitsAllocated <= 0 || bitsStored >= bitsAllocated)
            {
                return frameByteLength;
            }

            return (int)Math.Min(int.MaxValue, ((long)frameByteLength * bitsStored + bitsAllocated - 1) / bitsAllocated);
        }

        private static int[] QuantizeIrreversible(double[] coefficients, int width, int height, double[] steps)
        {
            var quantized = new int[coefficients.Length];
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, DefaultLevels, precision: 1, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            for (var resolution = 0; resolution <= DefaultLevels; resolution++)
            {
                foreach (var precinct in component.GetPrecincts(resolution))
                {
                    foreach (var band in component.GetBands(resolution, precinct))
                    {
                        var stepIndex = Jpeg2000QuantizationTable.SubbandIndex(resolution, band.Orientation);
                        var step = stepIndex >= 0 && stepIndex < steps.Length ? steps[stepIndex] : 1.0;
                        foreach (var block in band.CodeBlocks)
                        {
                            for (var y = block.Y0; y < block.Y0 + block.Height; y++)
                            {
                                for (var x = block.X0; x < block.X0 + block.Width; x++)
                                {
                                    var offset = (y * width) + x;
                                    quantized[offset] = RoundToEven((float)((float)coefficients[offset] / (float)step * (1 << Tier1FractionalBits)));
                                }
                            }
                        }
                    }
                }
            }

            return quantized;
        }

        private static int[] QuantizeIrreversibleOpenJpeg(double[] coefficients, int width, int height, ushort[] encodedSteps, int precision)
        {
            var quantized = new int[coefficients.Length];
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, DefaultLevels, precision: 1, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            for (var resolution = 0; resolution <= DefaultLevels; resolution++)
            {
                foreach (var precinct in component.GetPrecincts(resolution))
                {
                    foreach (var band in component.GetBands(resolution, precinct))
                    {
                        var step = ResolveEncodedStepSize(encodedSteps, precision, resolution, band.Orientation);
                        foreach (var block in band.CodeBlocks)
                        {
                            for (var y = block.Y0; y < block.Y0 + block.Height; y++)
                            {
                                for (var x = block.X0; x < block.X0 + block.Width; x++)
                                {
                                    var offset = (y * width) + x;
                                    quantized[offset] = RoundToEven((float)((float)coefficients[offset] / (float)step * (1 << Tier1FractionalBits)));
                                }
                            }
                        }
                    }
                }
            }

            return quantized;
        }

        private static byte[] EncodePackets(int[] coefficients, int width, int height, int precision, bool reversible, int layerCount, int sourceByteLength = 0, double[]? layerRates = null, int mainHeaderBytesBeforeSot = 0, ushort[]? encodedSteps = null)
        {
            return EncodePackets(
                new List<List<List<Jpeg2000EncodedBlock>>> { BuildCodeBlocksByResolution(coefficients, width, height, precision, reversible, encodedSteps) },
                layerCount,
                sourceByteLength,
                layerRates,
                mainHeaderBytesBeforeSot,
                reversible);
        }

        private static byte[] EncodePackets(
            IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components,
            int layerCount,
            int sourceByteLength,
            double[]? layerRates,
            int mainHeaderBytesBeforeSot,
            bool reversible)
        {
            var allBlocks = FlattenBlocks(components);
            var layerTargets = CreateLayerByteTargets(layerRates, Math.Max(1, layerCount), sourceByteLength, mainHeaderBytesBeforeSot);
            var layerSelections = AllocateLayerSelections(allBlocks, layerTargets, reversible);
            var layeredComponents = new List<List<List<Jpeg2000EncodedBlock[]>>>(layerSelections.Count);
            foreach (var selected in layerSelections)
            {
                layeredComponents.Add(ProjectLayerComponents(components, selected));
            }

            var packetEncoders = new List<List<Jpeg2000StandardPacketEncoder.LayeredPacketEncoder>>(components.Count);
            foreach (var component in components)
            {
                var componentEncoders = new List<Jpeg2000StandardPacketEncoder.LayeredPacketEncoder>(component.Count);
                foreach (var blocks in component)
                {
                    componentEncoders.Add(Jpeg2000StandardPacketEncoder.CreateLayeredEncoder(blocks));
                }

                packetEncoders.Add(componentEncoders);
            }

            var packets = new List<byte[]>();
            for (var layer = 0; layer < layeredComponents.Count; layer++)
            {
                for (var resolution = 0; resolution <= DefaultLevels; resolution++)
                {
                    for (var component = 0; component < components.Count; component++)
                    {
                        if (components[component][resolution].Count != 0)
                        {
                            packets.Add(packetEncoders[component][resolution].EncodePacket(layeredComponents[layer][component][resolution], layer));
                        }
                    }
                }
            }

            var bytes = new List<byte>();
            foreach (var packet in packets)
            {
                bytes.AddRange(packet);
            }

            return bytes.ToArray();
        }

        private static List<List<Jpeg2000EncodedBlock>> BuildCodeBlocksByResolution(int[] coefficients, int width, int height, int precision, bool reversible, ushort[]? encodedSteps, int componentIndex = 0, double[]? mctNorms = null)
        {
            var blocksByResolution = new List<List<Jpeg2000EncodedBlock>>();
            for (var resolution = 0; resolution <= DefaultLevels; resolution++)
            {
                blocksByResolution.Add(BuildCodeBlocksForComponent(coefficients, width, height, resolution, precision, reversible, encodedSteps, componentIndex, mctNorms));
            }

            return blocksByResolution;
        }

        private static List<List<Jpeg2000EncodedBlock[]>> ProjectLayerComponents(
            IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> selectedPasses)
        {
            var projectedComponents = new List<List<Jpeg2000EncodedBlock[]>>(components.Count);
            foreach (var component in components)
            {
                projectedComponents.Add(ProjectLayer(component, selectedPasses));
            }

            return projectedComponents;
        }

        private static List<List<Jpeg2000EncodedBlock>> FlattenBlocks(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components)
        {
            var flattened = new List<List<Jpeg2000EncodedBlock>>();
            foreach (var component in components)
            {
                foreach (var resolution in component)
                {
                    flattened.Add(resolution);
                }
            }

            return flattened;
        }

        private static List<Dictionary<Jpeg2000EncodedBlock, int>> AllocateLayerSelections(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            IReadOnlyList<int> layerTargets,
            bool reversible)
        {
            var layerCount = Math.Max(1, layerTargets.Count);
            var layers = new List<Dictionary<Jpeg2000EncodedBlock, int>>(layerCount);
            var projectedLayers = new List<List<Jpeg2000EncodedBlock[]>>(layerCount);
            var selectedPasses = CreateZeroPassSelection(blocksByResolution);
            for (var layer = 0; layer < layerCount; layer++)
            {
                selectedPasses = layerTargets[layer] > 0
                    ? SelectRateControlledPasses(blocksByResolution, projectedLayers, selectedPasses, layerTargets[layer], reversible)
                    : SelectAllPasses(blocksByResolution);
                layers.Add(CopyPassSelection(selectedPasses));
                projectedLayers.Add(ProjectLayer(blocksByResolution, selectedPasses));
            }

            return layers;
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> CreateZeroPassSelection(IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution)
        {
            var selectedPasses = new Dictionary<Jpeg2000EncodedBlock, int>();
            foreach (var blocks in blocksByResolution)
            {
                foreach (var block in blocks)
                {
                    selectedPasses[block] = 0;
                }
            }

            return selectedPasses;
        }

        private static List<Jpeg2000EncodedBlock[]> ProjectLayer(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> selectedPasses)
        {
            var resolutions = new List<Jpeg2000EncodedBlock[]>(blocksByResolution.Count);
            foreach (var blocks in blocksByResolution)
            {
                var projected = new Jpeg2000EncodedBlock[blocks.Count];
                for (var index = 0; index < blocks.Count; index++)
                {
                    var block = blocks[index];
                    var finalPasses = selectedPasses.TryGetValue(block, out var passes) ? passes : 0;
                    projected[index] = block.TruncateToPasses(finalPasses);
                }

                resolutions.Add(projected);
            }

            return resolutions;
        }

        private static int[] CreateLayerByteTargets(double[]? layerRates, int layerCount, int sourceByteLength, int mainHeaderBytesBeforeSot)
        {
            var targets = new int[layerCount];
            for (var layer = 0; layer < targets.Length; layer++)
            {
                var rate = layerRates != null && layer < layerRates.Length ? layerRates[layer] : 0;
                targets[layer] = EstimateTargetTileBytes(rate, sourceByteLength);
            }

            ApplyOpenJpegHeaderBudget(targets, mainHeaderBytesBeforeSot);
            return targets;
        }

        private static void ApplyOpenJpegHeaderBudget(int[] targets, int mainHeaderBytesBeforeSot)
        {
            if (targets.Length == 0 || mainHeaderBytesBeforeSot <= 0)
            {
                return;
            }

            if (targets[0] > 0)
            {
                targets[0] = Math.Max(30, targets[0] - mainHeaderBytesBeforeSot);
            }

            for (var layer = 1; layer < targets.Length; layer++)
            {
                if (targets[layer] <= 0)
                {
                    continue;
                }

                targets[layer] -= mainHeaderBytesBeforeSot + (layer == targets.Length - 1 ? 2 : 0);
                if (targets[layer] < targets[layer - 1] + 10)
                {
                    targets[layer] = targets[layer - 1] + 20;
                }
            }
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> SelectAllPasses(IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution)
        {
            var selectedPasses = new Dictionary<Jpeg2000EncodedBlock, int>();
            foreach (var blocks in blocksByResolution)
            {
                foreach (var block in blocks)
                {
                    selectedPasses[block] = block.PassCount;
                }
            }

            return selectedPasses;
        }

        private static List<Jpeg2000EncodedBlock> BuildCodeBlocksForComponent(int[] coefficients, int width, int height, int resolution, int precision, bool reversible, ushort[]? encodedSteps, int componentIndex, double[]? mctNorms)
        {
            var blocks = new List<Jpeg2000EncodedBlock>();
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, DefaultLevels, precision, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            foreach (var precinct in component.GetPrecincts(resolution))
            {
                foreach (var band in component.GetBands(resolution, precinct))
                {
                    foreach (var block in band.CodeBlocks)
                    {
                        var blockWidth = block.Width;
                        var blockHeight = block.Height;
                        var blockCoefficients = new int[blockWidth * blockHeight];
                        for (var y = 0; y < blockHeight; y++)
                        {
                            Array.Copy(coefficients, ((block.Y0 + y) * width) + block.X0, blockCoefficients, y * blockWidth, blockWidth);
                        }

                        var subbandBits = BandBitPlaneDepth(precision, resolution, block.Orientation, encodedSteps);
                        var bitCount = Math.Max(0, CalculateBitCountAfterShift(blockCoefficients, reversible ? Tier1FractionalBits : 0) - Tier1FractionalBits);
                        var zeroBitPlanes = Math.Max(0, subbandBits - bitCount);
                        var passCount = bitCount <= 0 ? 0 : (bitCount * 3) - 2;
                        var passLengths = Array.Empty<int>();
                        var passSnapshots = Array.Empty<byte[]>();
                        var passDistortions = Array.Empty<double>();
                        var subbandLevel = DefaultLevels - resolution;
                        var stepSize = encodedSteps != null
                            ? ResolveEncodedStepSize(encodedSteps, precision, resolution, block.Orientation)
                            : 1.0;
                        var data = passCount == 0
                            ? Array.Empty<byte>()
                            : new Jpeg2000StandardTier1Encoder(
                                blockWidth,
                                blockHeight,
                                block.Orientation,
                                0,
                                subbandLevel,
                                reversible ? 1 : 0,
                                stepSize,
                                ResolveMctNorm(componentIndex, mctNorms),
                                reversible ? Tier1FractionalBits : 0)
                                .Encode(blockCoefficients, passCount, out passLengths, out passSnapshots, out passDistortions);
                        blocks.Add(new Jpeg2000EncodedBlock(
                            block.LocalX,
                            block.LocalY,
                            band.CodeBlockCountX,
                            band.CodeBlockCountY,
                            zeroBitPlanes,
                            passCount,
                            data,
                            passLengths,
                            passSnapshots,
                            passDistortions,
                            resolution,
                            block.Orientation));
                    }
                }
            }

            return blocks;
        }

        private static List<Jpeg2000EncodedBlock> BuildCodeBlocks(int[] coefficients, int width, int height, int resolution, int precision, bool reversible, ushort[]? encodedSteps)
        {
            return BuildCodeBlocksForComponent(coefficients, width, height, resolution, precision, reversible, encodedSteps, componentIndex: 0, mctNorms: null);
        }

        private static int CalculateBitCount(int[] data)
        {
            var max = 0;
            foreach (var value in data)
            {
                var abs = value < 0 ? -value : value;
                if (abs > max)
                {
                    max = abs;
                }
            }

            if (max == 0)
            {
                return 0;
            }

            var bitPlane = 0;
            while (max > 0)
            {
                max >>= 1;
                bitPlane++;
            }

            return bitPlane;
        }

        private static int CalculateBitCountAfterShift(int[] data, int shift)
        {
            var max = 0;
            foreach (var value in data)
            {
                var shifted = value << shift;
                var abs = shifted < 0 ? -shifted : shifted;
                if (abs > max)
                {
                    max = abs;
                }
            }

            var bitPlane = 0;
            while (max > 0)
            {
                max >>= 1;
                bitPlane++;
            }

            return bitPlane;
        }

        private static double ResolveEncodedStepSize(ushort[] encodedSteps, int precision, int resolution, int orientation)
        {
            var index = Jpeg2000QuantizationTable.SubbandIndex(resolution, orientation);
            if (index < 0 || index >= encodedSteps.Length)
            {
                return 1.0;
            }

            return Jpeg2000QuantizationTable.DecodeStepSize(encodedSteps[index], precision + GainBits(orientation));
        }

        private static int GainBits(int orientation)
        {
            return orientation == 3 ? 2 : orientation == 0 ? 0 : 1;
        }

        private static int BandBitPlaneDepth(int precision, int resolution, int orientation, ushort[]? encodedSteps)
        {
            var index = Jpeg2000QuantizationTable.SubbandIndex(resolution, orientation);
            if (encodedSteps != null && index >= 0 && index < encodedSteps.Length)
            {
                return Jpeg2000QuantizationTable.BitPlaneDepth(encodedSteps[index], guardBits: 2);
            }

            return precision + GainBits(orientation) + 1;
        }

        private static ushort[] EncodeIrreversibleSteps(double[] steps, int precision)
        {
            var encoded = new ushort[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                encoded[i] = Jpeg2000QuantizationTable.EncodeStepSize(steps[i], precision);
            }

            return encoded;
        }

        private static int EstimateTargetTileBytes(double rate, int sourceByteLength)
        {
            if (rate <= 0 || sourceByteLength < 4096)
            {
                return 0;
            }

            return Math.Max(256, (int)Math.Ceiling(sourceByteLength / rate));
        }

        private static int EstimateMainHeaderBytesBeforeSot(int componentCount, bool irreversible)
        {
            var siz = 38 + (3 * componentCount);
            var cod = 12;
            var qcd = irreversible ? 35 : 19;
            var com = 37;
            return 2 + SegmentBytes(siz) + SegmentBytes(cod) + SegmentBytes(qcd) + SegmentBytes(com);
        }

        private static int SegmentBytes(int segmentLength)
        {
            return 2 + segmentLength;
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> SelectRateControlledPasses(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            IReadOnlyList<List<Jpeg2000EncodedBlock[]>> previousLayers,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> previousPasses,
            int targetTileBytes,
            bool reversible)
        {
            var selectedPasses = CopyPassSelection(previousPasses);
            var min = double.MaxValue;
            var max = 0.0;
            foreach (var blocks in blocksByResolution)
            {
                foreach (var block in blocks)
                {
                    for (var pass = 1; pass <= block.PassCount && pass <= block.PassLengths.Length; pass++)
                    {
                        var slope = GetAdjacentPassSlope(block, pass);
                        if (double.IsNaN(slope))
                        {
                            continue;
                        }

                        if (slope < min)
                        {
                            min = slope;
                        }

                        if (slope > max)
                        {
                            max = slope;
                        }
                    }
                }
            }

            if (max <= 0 || min == double.MaxValue)
            {
                return selectedPasses;
            }

            var low = min;
            var high = max;
            var threshold = 0.0;
            var stableThreshold = 0.0;
            var lastAllocation = selectedPasses;
            var lastAllocationOk = false;
            for (var iteration = 0; iteration < 128; iteration++)
            {
                var newThreshold = (low + high) / 2.0;
                if (threshold != 0.0 && Math.Abs(newThreshold - threshold) <= 0.5e-5 * threshold)
                {
                    break;
                }

                threshold = newThreshold;
                var candidatePasses = SelectPassesAtThreshold(blocksByResolution, previousPasses, threshold);
                var allocationSame = AreSelectionsEqual(candidatePasses, lastAllocation) && iteration != 0;
                if ((allocationSame && !lastAllocationOk) ||
                    (!allocationSame && EstimateLayeredTileBytes(blocksByResolution, previousLayers, candidatePasses) > targetTileBytes))
                {
                    lastAllocationOk = false;
                    low = threshold;
                    lastAllocation = candidatePasses;
                    continue;
                }

                lastAllocationOk = true;
                high = threshold;
                stableThreshold = threshold;
                lastAllocation = candidatePasses;
            }

            var goodThreshold = stableThreshold == 0.0 ? threshold : stableThreshold;
            return goodThreshold == 0.0
                ? selectedPasses
                : SelectPassesAtThreshold(blocksByResolution, previousPasses, goodThreshold);
        }

        private static double GetAdjacentPassSlope(Jpeg2000EncodedBlock block, int passCount)
        {
            if (passCount <= 0 || passCount > block.PassLengths.Length || passCount > block.PassDistortions.Length)
            {
                return double.NaN;
            }

            var previousPassCount = passCount - 1;
            var previousRate = previousPassCount <= 0 ? 0 : block.PassLengths[previousPassCount - 1];
            var nextRate = block.PassLengths[passCount - 1];
            var rateDelta = nextRate - previousRate;
            if (rateDelta == 0)
            {
                return double.NaN;
            }

            return GetDistortionDelta(block, previousPassCount, passCount) / rateDelta;
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> SelectPassesAtThreshold(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> previousPasses,
            double threshold)
        {
            var selectedPasses = CopyPassSelection(previousPasses);
            foreach (var blocks in blocksByResolution)
            {
                foreach (var block in blocks)
                {
                    var passCount = previousPasses.TryGetValue(block, out var passes) ? passes : 0;
                    for (var pass = passCount + 1; pass <= block.PassCount && pass <= block.PassLengths.Length; pass++)
                    {
                        if (threshold - GetPassSlope(block, passCount, pass) < DoubleMachineEpsilon)
                        {
                            passCount = pass;
                        }
                    }

                    selectedPasses[block] = passCount;
                }
            }

            return selectedPasses;
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> CopyPassSelection(IReadOnlyDictionary<Jpeg2000EncodedBlock, int> source)
        {
            var copy = new Dictionary<Jpeg2000EncodedBlock, int>();
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        private static double GetPassSlope(Jpeg2000EncodedBlock block, int basePassCount, int passCount)
        {
            if (passCount <= 0 || passCount > block.PassLengths.Length || passCount > block.PassDistortions.Length)
            {
                return 0.0;
            }

            var previousRate = basePassCount <= 0 ? 0 : block.PassLengths[Math.Min(basePassCount, block.PassLengths.Length) - 1];
            var nextRate = block.PassLengths[passCount - 1];
            var rateDelta = nextRate - previousRate;
            if (rateDelta <= 0)
            {
                var distortionDelta = GetDistortionDelta(block, basePassCount, passCount);
                return distortionDelta != 0.0 ? double.MaxValue : 0.0;
            }

            return GetDistortionDelta(block, basePassCount, passCount) / rateDelta;
        }

        private static double GetDistortionDelta(Jpeg2000EncodedBlock block, int basePassCount, int passCount)
        {
            var previousDistortion = basePassCount <= 0 ? 0.0 : block.PassDistortions[Math.Min(basePassCount, block.PassDistortions.Length) - 1];
            var nextDistortion = block.PassDistortions[passCount - 1];
            return nextDistortion - previousDistortion;
        }

        private static bool AreSelectionsEqual(
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> left,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static int EstimateEncodedTileBytes(IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution, Dictionary<Jpeg2000EncodedBlock, int> selectedPasses)
        {
            var total = 0;
            foreach (var blocks in blocksByResolution)
            {
                if (blocks.Count == 0)
                {
                    continue;
                }

                var projected = new List<Jpeg2000EncodedBlock>(blocks.Count);
                foreach (var block in blocks)
                {
                    projected.Add(block.TruncateToPasses(selectedPasses.TryGetValue(block, out var passCount) ? passCount : 0));
                }

                total += Jpeg2000StandardPacketEncoder.EncodeSingleLayerPacket(projected).Length;
            }

            return total;
        }

        private static int EstimateLayeredTileBytes(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            IReadOnlyList<List<Jpeg2000EncodedBlock[]>> previousLayers,
            IReadOnlyDictionary<Jpeg2000EncodedBlock, int> candidatePasses)
        {
            var packetEncoders = new List<Jpeg2000StandardPacketEncoder.LayeredPacketEncoder>(blocksByResolution.Count);
            foreach (var blocks in blocksByResolution)
            {
                packetEncoders.Add(Jpeg2000StandardPacketEncoder.CreateLayeredEncoder(blocks));
            }

            var total = 0;
            for (var layer = 0; layer < previousLayers.Count; layer++)
            {
                for (var resolution = 0; resolution < blocksByResolution.Count; resolution++)
                {
                    if (blocksByResolution[resolution].Count != 0)
                    {
                        total += packetEncoders[resolution].EncodePacket(previousLayers[layer][resolution], layer).Length;
                    }
                }
            }

            var candidateLayer = ProjectLayer(blocksByResolution, candidatePasses);
            for (var resolution = 0; resolution < blocksByResolution.Count; resolution++)
            {
                if (blocksByResolution[resolution].Count != 0)
                {
                    total += packetEncoders[resolution].EncodePacket(candidateLayer[resolution], previousLayers.Count).Length;
                }
            }

            return total;
        }

        private static int[] ReadSamples(byte[] frame, int bitsAllocated, int bitsStored, bool isSigned, bool encodeSignedAsUnsigned)
        {
            var bytesPerSample = bitsAllocated / 8;
            var samples = new int[frame.Length / bytesPerSample];
            var signBit = 1 << (bitsStored - 1);
            var mask = (1 << bitsStored) - 1;
            for (var i = 0; i < samples.Length; i++)
            {
                var offset = i * bytesPerSample;
                var value = bytesPerSample == 1 ? frame[offset] : frame[offset] | (frame[offset + 1] << 8);
                value &= mask;
                if (isSigned && !encodeSignedAsUnsigned && (value & signBit) != 0)
                {
                    value -= 1 << bitsStored;
                }

                samples[i] = value;
            }

            return samples;
        }

        private static int[][] ReadComponentSamples(byte[] frame, int samplesPerPixel, int bitsAllocated, int bitsStored, bool isSigned, bool encodeSignedAsUnsigned)
        {
            var interleaved = ReadSamples(frame, bitsAllocated, bitsStored, isSigned, encodeSignedAsUnsigned);
            var pixelCount = interleaved.Length / samplesPerPixel;
            var components = new int[samplesPerPixel][];
            for (var component = 0; component < samplesPerPixel; component++)
            {
                components[component] = new int[pixelCount];
            }

            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < samplesPerPixel; component++)
                {
                    components[component][pixel] = interleaved[(pixel * samplesPerPixel) + component];
                }
            }

            return components;
        }

        private static void ApplyForwardLevelShift(int[][] components, int precision, bool isSigned)
        {
            if (isSigned)
            {
                return;
            }

            var shift = 1 << (precision - 1);
            foreach (var component in components)
            {
                for (var i = 0; i < component.Length; i++)
                {
                    component[i] -= shift;
                }
            }
        }

        private static void ApplyForwardRct(int[][] components)
        {
            for (var i = 0; i < components[0].Length; i++)
            {
                var r = components[0][i];
                var g = components[1][i];
                var b = components[2][i];
                var y = (r + (2 * g) + b) >> 2;
                components[0][i] = y;
                components[1][i] = b - g;
                components[2][i] = r - g;
            }
        }

        private static double[][] CreateDoubleComponents(int[][] components)
        {
            var values = new double[components.Length][];
            for (var component = 0; component < components.Length; component++)
            {
                values[component] = new double[components[component].Length];
                for (var i = 0; i < components[component].Length; i++)
                {
                    values[component][i] = components[component][i];
                }
            }

            return values;
        }

        private static void ApplyForwardIct(double[][] components)
        {
            for (var i = 0; i < components[0].Length; i++)
            {
                var r = (float)components[0][i];
                var g = (float)components[1][i];
                var b = (float)components[2][i];
                components[0][i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                components[1][i] = (-0.16875f * r) - (0.331260f * g) + (0.5f * b);
                components[2][i] = (0.5f * r) - (0.418690f * g) - (0.081310f * b);
            }
        }

        private static double ResolveMctNorm(int componentIndex, double[]? mctNorms)
        {
            return mctNorms != null && componentIndex >= 0 && componentIndex < mctNorms.Length
                ? mctNorms[componentIndex]
                : 1.0;
        }

        private static double[] ReversibleMctNorms()
        {
            return new[] { 1.732, .8292, .8292 };
        }

        private static double[] IrreversibleMctNorms()
        {
            return new[] { 1.732, 1.805, 1.573 };
        }

        private static byte[] CreateCodingStylePayload(bool irreversible, Jpeg2000ProgressionOrder progressionOrder, int layerCount, bool usesMultipleComponentTransform)
        {
            return Jpeg2000MarkerPayloadBuilder.CreateCodingStyle(
                progressionOrder,
                layerCount,
                usesMultipleComponentTransform,
                DefaultLevels,
                codeBlockStyle: 0,
                transformation: (byte)(irreversible ? 0 : 1));
        }

        private static int RoundToEven(double value)
        {
            return (int)Math.Round(value, MidpointRounding.ToEven);
        }

        private static int RoundToEven(float value)
        {
            return (int)Math.Round(value, MidpointRounding.ToEven);
        }

        private static byte[] CreateCommentPayload()
        {
            var text = System.Text.Encoding.ASCII.GetBytes("Created by OpenJPEG version 2.5.4");
            var payload = new byte[text.Length + 2];
            payload[1] = 1;
            Buffer.BlockCopy(text, 0, payload, 2, text.Length);
            return payload;
        }

        private static byte[] PadToEvenLength(byte[] frame)
        {
            if ((frame.Length & 1) == 0)
            {
                return frame;
            }

            var padded = new byte[frame.Length + 1];
            Buffer.BlockCopy(frame, 0, padded, 0, frame.Length);
            return padded;
        }

    }
}
