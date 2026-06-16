using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal sealed class Jpeg2000HtTileEncoder
    {
        private const int CodeBlockWidth = 64;
        private const int CodeBlockHeight = 64;

        public byte[] EncodeLossless(DicomPixelData pixelData, byte[] frame, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K encoder currently supports monochrome and RGB frames only.");
            }

            var isSigned = pixelData.PixelRepresentation == PixelRepresentation.Signed;
            var components = ReadComponentSamples(frame, pixelData.SamplesPerPixel, pixelData.BitsAllocated, pixelData.BitsStored, isSigned);
            ApplyForwardLevelShift(components, pixelData.BitsStored, isSigned);
            if (components.Length == 3)
            {
                ApplyForwardRct(components);
            }

            var encodedComponents = new List<List<List<Jpeg2000EncodedBlock>>>(components.Length);
            foreach (var component in components)
            {
                var coefficients = Jpeg2000StandardWavelet.Forward53(component, pixelData.Width, pixelData.Height, decompositionLevels, 0, 0);
                encodedComponents.Add(BuildHtCodeBlocksByResolution(coefficients, pixelData.Width, pixelData.Height, pixelData.BitsStored, components.Length == 3, decompositionLevels));
            }

            return EncodePackets(encodedComponents, progressionOrder, decompositionLevels);
        }

        private static byte[] EncodePackets(IReadOnlyList<List<List<Jpeg2000EncodedBlock>>> components, Jpeg2000ProgressionOrder progressionOrder, int decompositionLevels)
        {
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

            var packets = Jpeg2000ProgressionOrderIterator.Enumerate(
                progressionOrder,
                layerCount: 1,
                resolutionCount: decompositionLevels + 1,
                componentCount: components.Count,
                precinctCount: 1);
            var bytes = new List<byte>();
            foreach (var packet in packets)
            {
                var blocks = components[packet.ComponentIndex][packet.ResolutionLevel];
                if (blocks.Count == 0)
                {
                    continue;
                }

                bytes.AddRange(packetEncoders[packet.ComponentIndex][packet.ResolutionLevel].EncodePacket(blocks, packet.LayerIndex));
            }

            return bytes.ToArray();
        }

        private static List<List<Jpeg2000EncodedBlock>> BuildHtCodeBlocksByResolution(int[] coefficients, int width, int height, int precision, bool usesMultipleComponentTransform, int decompositionLevels)
        {
            var result = new List<List<Jpeg2000EncodedBlock>>();
            for (var resolution = 0; resolution <= decompositionLevels; resolution++)
            {
                result.Add(BuildHtCodeBlocksForResolution(coefficients, width, height, precision, resolution, usesMultipleComponentTransform, decompositionLevels));
            }

            return result;
        }

        private static List<Jpeg2000EncodedBlock> BuildHtCodeBlocksForResolution(int[] coefficients, int width, int height, int precision, int resolution, bool usesMultipleComponentTransform, int decompositionLevels)
        {
            var blocks = new List<Jpeg2000EncodedBlock>();
            var component = new Jpeg2000StandardComponent(0, 0, 0, width, height, decompositionLevels, precision, isSigned: true);
            component.BuildCodeBlocks(CodeBlockWidth, CodeBlockHeight);
            foreach (var precinct in component.GetPrecincts(resolution))
            {
                foreach (var band in component.GetBands(resolution, precinct))
                {
                    foreach (var block in band.CodeBlocks)
                    {
                        var blockCoefficients = new int[block.Width * block.Height];
                        for (var y = 0; y < block.Height; y++)
                        {
                            Array.Copy(coefficients, ((block.Y0 + y) * width) + block.X0, blockCoefficients, y * block.Width, block.Width);
                        }

                        var kMax = Jpeg2000ReversibleBiboGains.BandKmax(
                            precision,
                            usesMultipleComponentTransform,
                            decompositionLevels,
                            resolution,
                            block.Orientation);
                        var missingMostSignificantBits = kMax - 1;
                        var data = HasNonZero(blockCoefficients)
                            ? Jpeg2000HtCodeBlockEncoder.EncodeStandardCleanupPass(
                                new Jpeg2000ClassicCodeBlock(block.Width, block.Height, ScaleForOpenJphReversibleCodeBlock(blockCoefficients, kMax)),
                                missingMostSignificantBits)
                            : Array.Empty<byte>();
                        blocks.Add(new Jpeg2000EncodedBlock(
                            block.LocalX,
                            block.LocalY,
                            band.CodeBlockCountX,
                            band.CodeBlockCountY,
                            data.Length == 0 ? kMax : missingMostSignificantBits,
                            data.Length == 0 ? 0 : 1,
                            data,
                            data.Length == 0 ? Array.Empty<int>() : new[] { data.Length },
                            data.Length == 0 ? Array.Empty<byte[]>() : new[] { data },
                            resolution,
                            block.Orientation));
                    }
                }
            }

            return blocks;
        }

        private static int[] ScaleForOpenJphReversibleCodeBlock(int[] coefficients, int kMax)
        {
            var shift = 31 - kMax;
            var scaled = new int[coefficients.Length];
            for (var i = 0; i < coefficients.Length; i++)
            {
                var value = coefficients[i];
                var sign = value < 0 ? unchecked((int)0x80000000) : 0;
                var magnitude = Math.Abs(value) << shift;
                scaled[i] = sign | magnitude;
            }

            return scaled;
        }

        private static bool HasNonZero(int[] values)
        {
            foreach (var value in values)
            {
                if (value != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GainBits(int orientation)
        {
            return orientation == 3 ? 2 : orientation == 0 ? 0 : 1;
        }

        private static int[][] ReadComponentSamples(byte[] frame, int samplesPerPixel, int bitsAllocated, int bitsStored, bool isSigned)
        {
            var bytesPerSample = bitsAllocated / 8;
            var sampleCount = frame.Length / bytesPerSample;
            var pixelCount = sampleCount / samplesPerPixel;
            var components = new int[samplesPerPixel][];
            for (var component = 0; component < components.Length; component++)
            {
                components[component] = new int[pixelCount];
            }

            var signBit = 1 << (bitsStored - 1);
            var mask = (1 << bitsStored) - 1;
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < samplesPerPixel; component++)
                {
                    var sample = (pixel * samplesPerPixel) + component;
                    var offset = sample * bytesPerSample;
                    var value = bytesPerSample == 1 ? frame[offset] : frame[offset] | (frame[offset + 1] << 8);
                    value &= mask;
                    if (isSigned && (value & signBit) != 0)
                    {
                        value -= 1 << bitsStored;
                    }

                    components[component][pixel] = value;
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
                var y = (r + (g * 2) + b) >> 2;
                var db = b - g;
                var dr = r - g;
                components[0][i] = y;
                components[1][i] = db;
                components[2][i] = dr;
            }
        }
    }
}
