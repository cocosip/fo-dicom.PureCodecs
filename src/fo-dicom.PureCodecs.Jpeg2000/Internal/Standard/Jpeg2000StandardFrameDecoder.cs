using System;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal sealed class Jpeg2000StandardFrameDecoder
    {
        public byte[] Decode(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000CodingStyleDefault cod, Jpeg2000QuantizationDefault qcd, byte[] tileData)
        {
            Validate(targetPixelData, siz, cod);

            var components = CreateComponents(siz, cod);
            var packetDecoder = new Jpeg2000StandardPacketDecoder(
                tileData,
                components.Length,
                cod.LayerCount,
                cod.DecompositionLevels + 1,
                cod.ProgressionOrder,
                components,
                cod.CodeBlockWidth,
                cod.CodeBlockHeight,
                cod.CodeBlockStyle);
            packetDecoder.Decode();

            foreach (var component in components)
            {
                DecodeComponent(component, cod, qcd);
            }

            if (cod.UsesMultipleComponentTransform && components.Length == 3)
            {
                ApplyInverseRct(components);
            }

            foreach (var component in components)
            {
                ApplyInverseLevelShift(component);
            }

            return Pack(targetPixelData, components);
        }

        private static void DecodeComponent(Jpeg2000StandardComponent component, Jpeg2000CodingStyleDefault cod, Jpeg2000QuantizationDefault qcd)
        {
            foreach (var block in component.AllCodeBlocks())
            {
                if (block.Data.Length == 0 || block.TotalPasses == 0)
                {
                    continue;
                }

                var bitPlaneCount = EstimateBitPlaneCount(component, cod, qcd, block);
                var decoder = new Jpeg2000StandardTier1Decoder(block.Width, block.Height, block.Orientation, cod.CodeBlockStyle);
                var coefficients = decoder.Decode(block.Data, block.TotalPasses, bitPlaneCount);
                for (var y = 0; y < block.Height; y++)
                {
                    for (var x = 0; x < block.Width; x++)
                    {
                        var destination = ((block.Y0 + y) * component.Width) + block.X0 + x;
                        component.Coefficients[destination] = coefficients[(y * block.Width) + x];
                    }
                }
            }

            if (cod.Transformation == 1)
            {
                var samples = new int[component.Coefficients.Length];
                Buffer.BlockCopy(component.Coefficients, 0, samples, 0, samples.Length * sizeof(int));
                Jpeg2000StandardWavelet.Inverse53(samples, component.Width, component.Height, component.Levels, component.X0, component.Y0);
                component.Samples = samples;
                return;
            }

            throw Jpeg2000Binary.CreateException("JPEG 2000 irreversible 9/7 decoding is not implemented in the standard decoder yet.");
        }

        private static int EstimateBitPlaneCount(Jpeg2000StandardComponent component, Jpeg2000CodingStyleDefault cod, Jpeg2000QuantizationDefault qcd, Jpeg2000StandardCodeBlock block)
        {
            var zeroBitPlanes = block.ZeroBitPlanes;
            var subbandBits = component.Precision + GainBits(block.Orientation);
            if (qcd.Style == Jpeg2000QuantizationStyle.None && qcd.StepSizes.Count > 0)
            {
                var resolution = ResolutionForBlock(cod.DecompositionLevels, block.Orientation, block.X0, block.Y0, component.Width, component.Height);
                var index = SubbandIndex(cod.DecompositionLevels, resolution, block.Orientation);
                if (index >= 0 && index < qcd.StepSizes.Count)
                {
                    subbandBits = ((int)qcd.StepSizes[index] >> 3) + qcd.GuardBits - 1;
                }
            }

            var codeBlockBits = subbandBits + 1 - zeroBitPlanes;
            if (codeBlockBits <= 0)
            {
                return -1;
            }

            return codeBlockBits;
        }

        private static int SubbandIndex(int levels, int resolution, int orientation)
        {
            if (resolution < 0 || resolution > levels)
            {
                return -1;
            }

            if (resolution == 0)
            {
                return orientation == 0 ? 0 : -1;
            }

            if (orientation < 1 || orientation > 3)
            {
                return -1;
            }

            return 1 + ((resolution - 1) * 3) + (orientation - 1);
        }

        private static int ResolutionForBlock(int levels, int orientation, int x0, int y0, int width, int height)
        {
            if (orientation == 0)
            {
                return 0;
            }

            for (var resolution = 1; resolution <= levels; resolution++)
            {
                var bands = Jpeg2000StandardGeometry.GetSubbands(width, height, 0, 0, levels, resolution);
                foreach (var band in bands)
                {
                    if (band.Orientation == orientation
                        && x0 >= band.OffsetX
                        && y0 >= band.OffsetY
                        && x0 < band.OffsetX + band.Width
                        && y0 < band.OffsetY + band.Height)
                    {
                        return resolution;
                    }
                }
            }

            return -1;
        }

        private static int GainBits(int orientation)
        {
            return orientation == 3 ? 2 : orientation == 0 ? 0 : 1;
        }

        private static Jpeg2000StandardComponent[] CreateComponents(Jpeg2000SizeSegment siz, Jpeg2000CodingStyleDefault cod)
        {
            var components = new Jpeg2000StandardComponent[siz.Components.Count];
            var tileX0 = (int)Math.Max(siz.ImageOffsetX, siz.TileOffsetX);
            var tileY0 = (int)Math.Max(siz.ImageOffsetY, siz.TileOffsetY);
            var tileX1 = (int)Math.Min(siz.ReferenceGridWidth, siz.TileOffsetX + siz.TileWidth);
            var tileY1 = (int)Math.Min(siz.ReferenceGridHeight, siz.TileOffsetY + siz.TileHeight);

            for (var i = 0; i < components.Length; i++)
            {
                var source = siz.Components[i];
                if (source.HorizontalSeparation != 1 || source.VerticalSeparation != 1)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 component subsampling is not supported by the standard decoder yet.");
                }

                components[i] = new Jpeg2000StandardComponent(
                    i,
                    tileX0,
                    tileY0,
                    tileX1 - tileX0,
                    tileY1 - tileY0,
                    cod.DecompositionLevels,
                    source.Precision,
                    source.IsSigned);
            }

            return components;
        }

        private static void ApplyInverseLevelShift(Jpeg2000StandardComponent component)
        {
            if (component.IsSigned)
            {
                return;
            }

            var shift = 1 << (component.Precision - 1);
            for (var i = 0; i < component.Samples.Length; i++)
            {
                component.Samples[i] += shift;
            }
        }

        private static void ApplyInverseRct(Jpeg2000StandardComponent[] components)
        {
            var count = components[0].Samples.Length;
            for (var i = 0; i < count; i++)
            {
                var y = components[0].Samples[i];
                var cb = components[1].Samples[i];
                var cr = components[2].Samples[i];
                var g = y - ((cb + cr) >> 2);
                var r = cr + g;
                var b = cb + g;
                components[0].Samples[i] = r;
                components[1].Samples[i] = g;
                components[2].Samples[i] = b;
            }
        }

        private static byte[] Pack(DicomPixelData targetPixelData, Jpeg2000StandardComponent[] components)
        {
            var bytesPerSample = targetPixelData.BitsAllocated / 8;
            var pixelCount = targetPixelData.Width * targetPixelData.Height;
            var result = new byte[pixelCount * components.Length * bytesPerSample];
            var precision = components[0].Precision;
            var max = (1 << precision) - 1;

            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < components.Length; component++)
                {
                    var value = components[component].Samples[pixel];
                    if (value < 0)
                    {
                        value = 0;
                    }
                    else if (value > max)
                    {
                        value = max;
                    }

                    var offset = ((pixel * components.Length) + component) * bytesPerSample;
                    result[offset] = (byte)value;
                    if (bytesPerSample == 2)
                    {
                        result[offset + 1] = (byte)(value >> 8);
                    }
                }
            }

            return result;
        }

        private static void Validate(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000CodingStyleDefault cod)
        {
            if (siz.TileWidth != siz.ReferenceGridWidth - siz.ImageOffsetX || siz.TileHeight != siz.ReferenceGridHeight - siz.ImageOffsetY)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 standard decoder currently supports single-tile codestreams only.");
            }

            if (targetPixelData.Width != (int)(siz.ReferenceGridWidth - siz.ImageOffsetX)
                || targetPixelData.Height != (int)(siz.ReferenceGridHeight - siz.ImageOffsetY)
                || targetPixelData.SamplesPerPixel != siz.Components.Count)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 codestream dimensions conflict with DICOM pixel metadata.");
            }

            if (cod.LayerCount <= 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 layer count is invalid.");
            }
        }
    }
}
