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

        public byte[] Encode(
            DicomPixelData pixelData,
            byte[] frame,
            bool irreversible,
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform,
            bool encodeSignedPixelValuesAsUnsigned,
            double rate)
        {
            if (pixelData.SamplesPerPixel != 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 standard encoder currently supports monochrome frames only.");
            }

            if (progressionOrder != Jpeg2000ProgressionOrder.LRCP)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 standard encoder currently supports LRCP progression only.");
            }

            var width = pixelData.Width;
            var height = pixelData.Height;
            var precision = pixelData.BitsStored;
            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed && !encodeSignedPixelValuesAsUnsigned;
            var samples = ReadSamples(frame, pixelData.BitsAllocated, precision, isSigned, encodeSignedPixelValuesAsUnsigned);
            if (!isSigned)
            {
                var shift = 1 << (precision - 1);
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] -= shift;
                }
            }

            var irreversibleSteps = irreversible ? Jpeg2000QuantizationTable.CreateIrreversibleSteps(DefaultLevels, DefaultIrreversibleQuality) : Array.Empty<double>();
            var irreversibleEncodedSteps = irreversible ? EncodeIrreversibleSteps(irreversibleSteps, precision) : Array.Empty<ushort>();
            var tileData = irreversible
                ? EncodeIrreversibleTile(samples, width, height, precision, irreversibleSteps, irreversibleEncodedSteps, rate, frame.Length, layerCount)
                : EncodeReversibleTileLayered(samples, width, height, precision, layerCount);
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
            return writer.ToArray();
        }

        private static byte[] EncodeReversibleTile(int[] samples, int width, int height, int precision)
        {
            return EncodeReversibleTileLayered(samples, width, height, precision, layerCount: 1);
        }

        private static byte[] EncodeReversibleTileLayered(int[] samples, int width, int height, int precision, int layerCount)
        {
            var coefficients = Jpeg2000StandardWavelet.Forward53(samples, width, height, DefaultLevels, 0, 0);
            return EncodePackets(coefficients, width, height, precision, reversible: true, layerCount: layerCount);
        }

        private static byte[] EncodeIrreversibleTile(int[] samples, int width, int height, int precision, double[] steps, ushort[] encodedSteps, double rate, int sourceByteLength, int layerCount = 1)
        {
            var values = new double[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                values[i] = samples[i];
            }

            Jpeg2000StandardIrreversibleWavelet.Forward97(values, width, height, DefaultLevels);
            var coefficients = QuantizeIrreversible(values, width, height, steps);
            return EncodePackets(coefficients, width, height, precision, reversible: false, layerCount: layerCount, targetTileBytes: EstimateTargetTileBytes(rate, sourceByteLength), encodedSteps: encodedSteps);
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
                                    quantized[offset] = RoundToEven(coefficients[offset] / step);
                                }
                            }
                        }
                    }
                }
            }

            return quantized;
        }

        private static byte[] EncodePackets(int[] coefficients, int width, int height, int precision, bool reversible, int layerCount, int targetTileBytes = 0, ushort[]? encodedSteps = null)
        {
            var blocksByResolution = new List<List<Jpeg2000EncodedBlock>>();
            for (var resolution = 0; resolution <= DefaultLevels; resolution++)
            {
                var blocks = BuildCodeBlocks(coefficients, width, height, resolution, precision, reversible, encodedSteps);
                blocksByResolution.Add(blocks);
            }

            var layeredBlocksByResolution = AllocateLayers(blocksByResolution, Math.Max(1, layerCount), targetTileBytes);
            var packetEncoders = new List<Jpeg2000StandardPacketEncoder.LayeredPacketEncoder>(blocksByResolution.Count);
            foreach (var blocks in blocksByResolution)
            {
                packetEncoders.Add(Jpeg2000StandardPacketEncoder.CreateLayeredEncoder(blocks));
            }

            var packets = new List<byte[]>();
            for (var layer = 0; layer < layeredBlocksByResolution.Count; layer++)
            {
                for (var resolution = 0; resolution < blocksByResolution.Count; resolution++)
                {
                    if (blocksByResolution[resolution].Count != 0)
                    {
                        packets.Add(packetEncoders[resolution].EncodePacket(layeredBlocksByResolution[layer][resolution], layer));
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

        private static List<List<Jpeg2000EncodedBlock[]>> AllocateLayers(
            IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution,
            int layerCount,
            int targetTileBytes)
        {
            var selectedPasses = targetTileBytes > 0
                ? SelectRateControlledPasses(blocksByResolution, targetTileBytes)
                : SelectAllPasses(blocksByResolution);
            var layers = new List<List<Jpeg2000EncodedBlock[]>>(layerCount);
            for (var layer = 0; layer < layerCount; layer++)
            {
                var resolutions = new List<Jpeg2000EncodedBlock[]>(blocksByResolution.Count);
                foreach (var blocks in blocksByResolution)
                {
                    var projected = new Jpeg2000EncodedBlock[blocks.Count];
                    for (var index = 0; index < blocks.Count; index++)
                    {
                        var block = blocks[index];
                        var finalPasses = selectedPasses.TryGetValue(block, out var passes) ? passes : 0;
                        var cumulativePasses = layer == layerCount - 1 ? finalPasses : 0;
                        projected[index] = block.TruncateToPasses(cumulativePasses);
                    }

                    resolutions.Add(projected);
                }

                layers.Add(resolutions);
            }

            return layers;
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

        private static List<Jpeg2000EncodedBlock> BuildCodeBlocks(int[] coefficients, int width, int height, int resolution, int precision, bool reversible, ushort[]? encodedSteps)
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
                        var scaled = new int[blockCoefficients.Length];
                        for (var i = 0; i < blockCoefficients.Length; i++)
                        {
                            scaled[i] = blockCoefficients[i] << Tier1FractionalBits;
                        }

                        var bitCount = Math.Max(0, CalculateBitCount(scaled) - Tier1FractionalBits);
                        var zeroBitPlanes = Math.Max(0, subbandBits - bitCount);
                        var passCount = bitCount <= 0 ? 0 : (bitCount * 3) - 2;
                        var passLengths = Array.Empty<int>();
                        var passSnapshots = Array.Empty<byte[]>();
                        var data = passCount == 0
                            ? Array.Empty<byte>()
                            : new Jpeg2000StandardTier1Encoder(blockWidth, blockHeight, block.Orientation, 0).Encode(scaled, passCount, out passLengths, out passSnapshots);
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
                            resolution,
                            block.Orientation));
                    }
                }
            }

            return blocks;
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

            return Math.Max(256, (int)Math.Floor(sourceByteLength / rate));
        }

        private static Dictionary<Jpeg2000EncodedBlock, int> SelectRateControlledPasses(IReadOnlyList<List<Jpeg2000EncodedBlock>> blocksByResolution, int targetTileBytes)
        {
            var selectedPasses = new Dictionary<Jpeg2000EncodedBlock, int>();
            foreach (var blocks in blocksByResolution)
            {
                foreach (var block in blocks)
                {
                    selectedPasses[block] = 0;
                }
            }

            var currentBytes = EstimateEncodedTileBytes(blocksByResolution, selectedPasses);
            while (currentBytes < targetTileBytes)
            {
                PassCandidate? best = null;
                foreach (var blocks in blocksByResolution)
                {
                    foreach (var block in blocks)
                    {
                        var currentPasses = selectedPasses[block];
                        var nextPass = currentPasses + 1;
                        if (nextPass > block.PassCount || nextPass > block.PassLengths.Length)
                        {
                            continue;
                        }

                        var currentLength = currentPasses == 0 ? 0 : block.PassLengths[currentPasses - 1];
                        var nextLength = block.PassLengths[nextPass - 1];
                        var byteDelta = Math.Max(1, nextLength - currentLength);
                        var score = EstimatePassDistortionReduction(block, nextPass) / byteDelta;
                        var candidate = new PassCandidate(block, nextPass, byteDelta, score);
                        if (!best.HasValue || candidate.Score > best.Value.Score)
                        {
                            best = candidate;
                        }
                    }
                }

                if (!best.HasValue)
                {
                    break;
                }

                selectedPasses[best.Value.Block] = best.Value.PassCount;
                var nextBytes = EstimateEncodedTileBytes(blocksByResolution, selectedPasses);
                if (nextBytes > targetTileBytes)
                {
                    selectedPasses[best.Value.Block] = best.Value.PassCount - 1;
                    break;
                }

                currentBytes = nextBytes;
            }

            return selectedPasses;
        }

        private static double EstimatePassDistortionReduction(Jpeg2000EncodedBlock block, int passCount)
        {
            var remainingBitPlanes = Math.Max(0, block.PassCount - passCount);
            var passType = (passCount + 2) % 3;
            var passWeight = passType == 2 ? 1.2 : passType == 0 ? 1.0 : 0.7;
            var resolutionWeight = block.Resolution == 0 ? 8.0 : 1.0 / (1 << Math.Min(block.Resolution - 1, 6));
            var orientationWeight = block.Orientation == 0 ? 4.0 : block.Orientation == 3 ? 0.8 : 1.0;
            return Math.Pow(2.0, remainingBitPlanes) * passWeight * resolutionWeight * orientationWeight;
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

        private readonly struct PassCandidate
        {
            public PassCandidate(Jpeg2000EncodedBlock block, int passCount, int byteDelta, double score)
            {
                Block = block;
                PassCount = passCount;
                ByteDelta = byteDelta;
                Score = score;
            }

            public Jpeg2000EncodedBlock Block { get; }

            public int PassCount { get; }

            public int ByteDelta { get; }

            public double Score { get; }
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

        private static byte[] CreateCommentPayload()
        {
            var text = System.Text.Encoding.ASCII.GetBytes("fo-dicom.PureCodecs JPEG2000 encoder");
            var payload = new byte[text.Length + 2];
            payload[1] = 1;
            Buffer.BlockCopy(text, 0, payload, 2, text.Length);
            return payload;
        }

    }
}
