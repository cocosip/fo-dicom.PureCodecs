using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal.Standard
{
    internal enum Jpeg2000DecodeProfile
    {
        ClassicOpenJpeg,
        HighThroughputOpenJph
    }

    internal sealed class Jpeg2000StandardFrameDecoder
    {
        private readonly Jpeg2000DecodeProfile _profile;

        public Jpeg2000StandardFrameDecoder()
            : this(Jpeg2000DecodeProfile.ClassicOpenJpeg)
        {
        }

        public Jpeg2000StandardFrameDecoder(Jpeg2000DecodeProfile profile)
        {
            _profile = profile;
        }

        public byte[] Decode(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000CodingStyleDefault cod, Jpeg2000QuantizationDefault qcd, byte[] tileData)
        {
            return Decode(
                targetPixelData,
                siz,
                cod,
                qcd,
                ResolveDefaultCodingStyles(siz, cod),
                ResolveDefaultQuantizations(siz, qcd),
                tileData);
        }

        public byte[] Decode(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            byte[] tileData)
        {
            return Decode(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                Array.Empty<Jpeg2000ProgressionOrderChange>(),
                tileData);
        }

        public byte[] Decode(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[] tileData)
        {
            return Decode(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                new int[siz.Components.Count],
                progressionChanges,
                tileData);
        }

        public byte[] Decode(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<int> regionOfInterestShifts,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[] tileData)
        {
            return Decode(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                regionOfInterestShifts,
                progressionChanges,
                null,
                tileData);
        }

        public byte[] Decode(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<int> regionOfInterestShifts,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[]? packedPacketHeaders,
            byte[] tileData)
        {
            var image = Jpeg2000ImageModel.FromSizeSegment(siz);
            if (image.Tiles.Count != 1)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 multi-tile codestream requires tile-indexed decoding.");
            }

            return DecodeTile(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                regionOfInterestShifts,
                progressionChanges,
                packedPacketHeaders,
                tileData,
                image.Tiles[0]);
        }

        public byte[] DecodeTile(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            byte[] tileData,
            Jpeg2000TileModel tile)
        {
            return DecodeTile(
                targetPixelData,
                siz,
                cod,
                qcd,
                ResolveDefaultCodingStyles(siz, cod),
                ResolveDefaultQuantizations(siz, qcd),
                tileData,
                tile);
        }

        public byte[] DecodeTile(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            byte[] tileData,
            Jpeg2000TileModel tile)
        {
            return DecodeTile(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                Array.Empty<Jpeg2000ProgressionOrderChange>(),
                tileData,
                tile);
        }

        public byte[] DecodeTile(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[] tileData,
            Jpeg2000TileModel tile)
        {
            return DecodeTile(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                new int[siz.Components.Count],
                progressionChanges,
                tileData,
                tile);
        }

        public byte[] DecodeTile(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<int> regionOfInterestShifts,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[] tileData,
            Jpeg2000TileModel tile)
        {
            return DecodeTile(
                targetPixelData,
                siz,
                cod,
                qcd,
                componentCodingStyles,
                componentQuantizations,
                regionOfInterestShifts,
                progressionChanges,
                null,
                tileData,
                tile);
        }

        public byte[] DecodeTile(
            DicomPixelData targetPixelData,
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod,
            Jpeg2000QuantizationDefault qcd,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> componentQuantizations,
            IReadOnlyList<int> regionOfInterestShifts,
            IReadOnlyList<Jpeg2000ProgressionOrderChange> progressionChanges,
            byte[]? packedPacketHeaders,
            byte[] tileData,
            Jpeg2000TileModel tile)
        {
            Validate(targetPixelData, siz, cod);
            ValidateComponentParameters(siz, componentCodingStyles, componentQuantizations);
            if (regionOfInterestShifts == null || regionOfInterestShifts.Count != siz.Components.Count)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 resolved RGN component count does not match SIZ.");
            }

            var components = CreateTileComponents(siz, componentCodingStyles, tile);
            var resolutionCount = 0;
            for (var component = 0; component < componentCodingStyles.Count; component++)
            {
                resolutionCount = Math.Max(
                    resolutionCount,
                    componentCodingStyles[component].DecompositionLevels + 1);
            }

            var packetDecoder = new Jpeg2000StandardPacketDecoder(
                tileData,
                components.Length,
                cod.LayerCount,
                resolutionCount,
                cod.ProgressionOrder,
                components,
                cod.CodeBlockWidth,
                cod.CodeBlockHeight,
                cod.CodeBlockStyle,
                cod,
                componentCodingStyles,
                progressionChanges,
                packedPacketHeaders);
            packetDecoder.Decode();

            for (var component = 0; component < components.Length; component++)
            {
                DecodeComponent(
                    components[component],
                    componentCodingStyles[component],
                    componentQuantizations[component],
                    regionOfInterestShifts[component]);
            }

            if (cod.UsesMultipleComponentTransform && components.Length == 3)
            {
                var transform = componentCodingStyles[0].Transformation;
                if (componentCodingStyles[1].Transformation != transform
                    || componentCodingStyles[2].Transformation != transform)
                {
                    throw Jpeg2000Binary.CreateException(
                        "JPEG 2000 multiple component transform requires matching component transformations.");
                }

                if (transform == 0)
                {
                    ApplyInverseIct(components);
                }
                else
                {
                    ApplyInverseRct(components);
                }
            }

            foreach (var component in components)
            {
                ApplyInverseLevelShift(component);
            }

            return Pack(targetPixelData, components);
        }

        private static void DecodeComponent(
            Jpeg2000StandardComponent component,
            Jpeg2000ResolvedCodingStyle cod,
            Jpeg2000ResolvedQuantization qcd,
            int regionOfInterestShift)
        {
            foreach (var block in component.AllCodeBlocks())
            {
                if (block.Data.Length == 0 || block.TotalPasses == 0)
                {
                    continue;
                }

                double[]? irreversibleCoefficients = null;
                var coefficients = IsHighThroughput(cod)
                    ? DecodeHighThroughputBlock(component, cod, qcd, block, out irreversibleCoefficients)
                    : DecodeStandardBlock(component, cod, qcd, block, regionOfInterestShift);
                for (var y = 0; y < block.Height; y++)
                {
                    for (var x = 0; x < block.Width; x++)
                    {
                        var destination = ((block.Y0 + y) * component.Width) + block.X0 + x;
                        component.Coefficients[destination] = coefficients[(y * block.Width) + x];
                        if (irreversibleCoefficients != null)
                        {
                            component.IrreversibleCoefficients ??= new double[component.Coefficients.Length];
                            component.IrreversibleCoefficients[destination] = irreversibleCoefficients[(y * block.Width) + x];
                        }
                    }
                }
            }

            if (cod.Transformation == 1)
            {
                var samples = new int[component.Coefficients.Length];
                Buffer.BlockCopy(component.Coefficients, 0, samples, 0, samples.Length * sizeof(int));
                if (IsHighThroughput(cod))
                {
                    Jpeg2000StandardWavelet.Inverse53HighThroughput(samples, component.Width, component.Height, component.Levels, component.X0, component.Y0);
                }
                else
                {
                    Jpeg2000StandardWavelet.Inverse53Classic(samples, component.Width, component.Height, component.Levels, component.X0, component.Y0);
                }
                component.Samples = samples;
                return;
            }

            if (cod.Transformation == 0)
            {
                var samples = component.IrreversibleCoefficients ?? DequantizeIrreversible(component, cod, qcd);
                if (IsHighThroughput(cod))
                {
                    Jpeg2000StandardIrreversibleWavelet.Inverse97HighThroughput(
                        samples,
                        component.Width,
                        component.Height,
                        component.Levels);
                }
                else
                {
                    Jpeg2000StandardIrreversibleWavelet.Inverse97(
                        samples,
                        component.Width,
                        component.Height,
                        component.Levels);
                }
                component.FloatSamples = samples;
                component.Samples = Round(samples);
                return;
            }

            throw Jpeg2000Binary.CreateException("JPEG 2000 transformation type is unsupported.");
        }

        private static int[] DecodeStandardBlock(
            Jpeg2000StandardComponent component,
            Jpeg2000ResolvedCodingStyle cod,
            Jpeg2000ResolvedQuantization qcd,
            Jpeg2000StandardCodeBlock block,
            int regionOfInterestShift)
        {
            var bitPlaneCount = EstimateBitPlaneCount(component, cod, qcd, block);
            var decoder = new Jpeg2000StandardTier1Decoder(block.Width, block.Height, block.Orientation, cod.CodeBlockStyle);
            return cod.Transformation == 0
                ? decoder.DecodeScaled(block.Data, block.TotalPasses, bitPlaneCount, regionOfInterestShift)
                : decoder.Decode(block.Data, block.TotalPasses, bitPlaneCount, regionOfInterestShift);
        }

        private static int[] DecodeHighThroughputBlock(
            Jpeg2000StandardComponent component,
            Jpeg2000ResolvedCodingStyle cod,
            Jpeg2000ResolvedQuantization qcd,
            Jpeg2000StandardCodeBlock block,
            out double[]? irreversibleCoefficients)
        {
            irreversibleCoefficients = null;
            var reversible = cod.Transformation == 1 && qcd.Style == Jpeg2000QuantizationStyle.None;
            var irreversible = cod.Transformation == 0 && qcd.Style == Jpeg2000QuantizationStyle.ScalarExpounded;
            if (!reversible && !irreversible)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K pure decoder supports reversible lossless and scalar-expounded irreversible cleanup code-blocks.");
            }

            if (block.TotalPasses > 3)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K pure decoder currently supports up to three HT coding passes.");
            }

            Jpeg2000ClassicCodeBlock decoded;
            try
            {
                decoded = DecodeHighThroughputSegments(block);
            }
            catch (FellowOakDicom.Imaging.Codec.DicomCodecException ex)
            {
                throw Jpeg2000Binary.CreateException(
                    $"HTJ2K code-block decode failed at x={block.X0}, y={block.Y0}, local=({block.LocalX},{block.LocalY}), orientation={block.Orientation}, zeroBitPlanes={block.ZeroBitPlanes}, size={block.Width}x{block.Height}, passes={block.TotalPasses}, segments={DescribeSegments(block)}. {ex.Message}");
            }
            var kMax = EstimateHighThroughputBandKmax(component, cod, qcd, block);
            if (reversible)
            {
                return UnscaleOpenJphReversibleCodeBlock(decoded.Coefficients, kMax);
            }

            irreversibleCoefficients = UnscaleOpenJphIrreversibleCodeBlock(component, cod, qcd, block, decoded.Coefficients, kMax);

            return new int[decoded.Coefficients.Count];
        }

        private static Jpeg2000ClassicCodeBlock DecodeHighThroughputSegments(Jpeg2000StandardCodeBlock block)
        {
            if (block.Segments.Count == 0)
            {
                return Jpeg2000HtCodeBlockDecoder.DecodeStandardCleanupPass(
                    block.Data,
                    block.Width,
                    block.Height,
                    block.ZeroBitPlanes);
            }

            var cleanup = block.Segments[0].Data;
            if (block.TotalPasses <= 1)
            {
                return Jpeg2000HtCodeBlockDecoder.DecodeStandardCleanupPass(
                    cleanup,
                    block.Width,
                    block.Height,
                    block.ZeroBitPlanes);
            }

            var refinementLength = 0;
            for (var i = 1; i < block.Segments.Count; i++)
            {
                refinementLength += block.Segments[i].Data.Length;
            }

            var refinement = new byte[refinementLength];
            var offset = 0;
            for (var i = 1; i < block.Segments.Count; i++)
            {
                var data = block.Segments[i].Data;
                Buffer.BlockCopy(data, 0, refinement, offset, data.Length);
                offset += data.Length;
            }

            return Jpeg2000HtStandardCleanupPassDecoder.Decode(
                cleanup,
                refinement,
                block.TotalPasses,
                block.Width,
                block.Height,
                block.ZeroBitPlanes);
        }

        private static string DescribeSegments(Jpeg2000StandardCodeBlock block)
        {
            if (block.Segments.Count == 0)
            {
                return block.Data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var values = new string[block.Segments.Count];
            for (var i = 0; i < values.Length; i++)
            {
                var data = block.Segments[i].Data;
                values[i] = data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (data.Length >= 2)
                {
                    values[i] += ":" + data[data.Length - 2].ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + data[data.Length - 1].ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return string.Join(",", values);
        }

        private static int EstimateHighThroughputBandKmax(
            Jpeg2000StandardComponent component,
            Jpeg2000ResolvedCodingStyle cod,
            Jpeg2000ResolvedQuantization qcd,
            Jpeg2000StandardCodeBlock block)
        {
            var resolution = ResolutionForBlock(cod.DecompositionLevels, block.Orientation, block.X0, block.Y0, component.Width, component.Height);
            var index = SubbandIndex(cod.DecompositionLevels, resolution, block.Orientation);
            if (index >= 0 && index < qcd.StepSizes.Count)
            {
                if (qcd.Style == Jpeg2000QuantizationStyle.None)
                {
                    return (qcd.StepSizes[index] >> 3) + qcd.GuardBits - 1;
                }

                if (qcd.Style == Jpeg2000QuantizationStyle.ScalarExpounded)
                {
                    return Jpeg2000HtIrreversibleQuantization.Kmax(qcd.StepSizes[index], qcd.GuardBits);
                }
            }

            return EstimateBandBitPlaneDepth(component, cod, qcd, block);
        }

        private static int[] UnscaleOpenJphReversibleCodeBlock(IReadOnlyList<int> coefficients, int kMax)
        {
            var shift = 31 - kMax;
            var unscaled = new int[coefficients.Count];
            for (var i = 0; i < coefficients.Count; i++)
            {
                var value = unchecked((uint)coefficients[i]);
                var magnitude = (int)((value & 0x7FFFFFFFu) >> shift);
                unscaled[i] = (value & 0x80000000u) != 0 ? -magnitude : magnitude;
            }

            return unscaled;
        }

        private static double[] UnscaleOpenJphIrreversibleCodeBlock(
            Jpeg2000StandardComponent component,
            Jpeg2000ResolvedCodingStyle cod,
            Jpeg2000ResolvedQuantization qcd,
            Jpeg2000StandardCodeBlock block,
            IReadOnlyList<int> coefficients,
            int kMax)
        {
            var resolution = ResolutionForBlock(cod.DecompositionLevels, block.Orientation, block.X0, block.Y0, component.Width, component.Height);
            var index = SubbandIndex(cod.DecompositionLevels, resolution, block.Orientation);
            if (qcd.StepSizes.Count == 0)
            {
                throw Jpeg2000Binary.CreateException("HTJ2K irreversible QCD marker does not contain quantization steps.");
            }

            if (index < 0 || index >= qcd.StepSizes.Count)
            {
                index = qcd.StepSizes.Count - 1;
            }

            var delta = Jpeg2000HtIrreversibleQuantization.DecodeDelta(qcd.StepSizes[index], block.Orientation, kMax)
                * Math.Pow(2.0, component.Precision);
            return Jpeg2000HtIrreversibleQuantization.FromSignMagnitude(coefficients, delta);
        }

        private static bool IsHighThroughput(Jpeg2000ResolvedCodingStyle cod)
        {
            return (cod.CodeBlockStyle & 0x40) != 0;
        }

        private static double[] DequantizeIrreversible(Jpeg2000StandardComponent component, Jpeg2000ResolvedCodingStyle cod, Jpeg2000ResolvedQuantization qcd)
        {
            var samples = new double[component.Coefficients.Length];
            foreach (var block in component.AllCodeBlocks())
            {
                var resolution = ResolutionForBlock(cod.DecompositionLevels, block.Orientation, block.X0, block.Y0, component.Width, component.Height);
                var index = SubbandIndex(cod.DecompositionLevels, resolution, block.Orientation);
                var step = 1.0;
                if (index >= 0 && index < qcd.StepSizes.Count)
                {
                    step = Jpeg2000QuantizationTable.DecodeStepSize(qcd.StepSizes[index], component.Precision);
                }

                for (var y = block.Y0; y < block.Y0 + block.Height; y++)
                {
                    for (var x = block.X0; x < block.X0 + block.Width; x++)
                    {
                        var offset = (y * component.Width) + x;
                        var coefficient = component.Coefficients[offset];
                        if (qcd.Style != Jpeg2000QuantizationStyle.None)
                        {
                            samples[offset] = coefficient * step / 64.0;
                        }
                        else
                        {
                            samples[offset] = coefficient * step;
                        }
                    }
                }
            }

            return samples;
        }

        private static int[] Round(double[] values)
        {
            var result = new int[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                result[i] = values[i] >= 0 ? (int)(values[i] + 0.5) : (int)(values[i] - 0.5);
            }

            return result;
        }

        private static int EstimateBitPlaneCount(Jpeg2000StandardComponent component, Jpeg2000ResolvedCodingStyle cod, Jpeg2000ResolvedQuantization qcd, Jpeg2000StandardCodeBlock block)
        {
            var zeroBitPlanes = block.ZeroBitPlanes;
            var subbandBits = EstimateBandBitPlaneDepth(component, cod, qcd, block);
            var codeBlockBits = subbandBits - zeroBitPlanes;
            if (codeBlockBits <= 0)
            {
                return -1;
            }

            return codeBlockBits;
        }

        private static int EstimateBandBitPlaneDepth(Jpeg2000StandardComponent component, Jpeg2000ResolvedCodingStyle cod, Jpeg2000ResolvedQuantization qcd, Jpeg2000StandardCodeBlock block)
        {
            if (qcd.Style != Jpeg2000QuantizationStyle.None)
            {
                var resolution = ResolutionForBlock(cod.DecompositionLevels, block.Orientation, block.X0, block.Y0, component.Width, component.Height);
                var index = SubbandIndex(cod.DecompositionLevels, resolution, block.Orientation);
                if (index >= 0 && index < qcd.StepSizes.Count)
                {
                    return Jpeg2000QuantizationTable.BitPlaneDepth(qcd.StepSizes[index], qcd.GuardBits);
                }
            }

            return component.Precision + GainBits(block.Orientation) + qcd.GuardBits - 1;
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

        private static Jpeg2000StandardComponent[] CreateComponents(
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod)
        {
            return CreateTileComponents(
                siz,
                ResolveDefaultCodingStyles(siz, cod),
                Jpeg2000ImageModel.FromSizeSegment(siz).Tiles[0]);
        }

        private static Jpeg2000StandardComponent[] CreateTileComponents(
            Jpeg2000SizeSegment siz,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> componentCodingStyles,
            Jpeg2000TileModel tile)
        {
            var components = new Jpeg2000StandardComponent[siz.Components.Count];
            var tileX0 = checked((int)tile.X0);
            var tileY0 = checked((int)tile.Y0);
            var tileX1 = checked((int)tile.X1);
            var tileY1 = checked((int)tile.Y1);

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
                    componentCodingStyles[i].DecompositionLevels,
                    source.Precision,
                    source.IsSigned);
            }

            return components;
        }

        private static Jpeg2000ResolvedCodingStyle[] ResolveDefaultCodingStyles(
            Jpeg2000SizeSegment siz,
            Jpeg2000CodingStyleDefault cod)
        {
            var styles = new Jpeg2000ResolvedCodingStyle[siz.Components.Count];
            for (var component = 0; component < styles.Length; component++)
            {
                styles[component] = Jpeg2000ResolvedCodingStyle.FromDefault(component, cod);
            }

            return styles;
        }

        private static Jpeg2000ResolvedQuantization[] ResolveDefaultQuantizations(
            Jpeg2000SizeSegment siz,
            Jpeg2000QuantizationDefault qcd)
        {
            var quantizations = new Jpeg2000ResolvedQuantization[siz.Components.Count];
            for (var component = 0; component < quantizations.Length; component++)
            {
                quantizations[component] = Jpeg2000ResolvedQuantization.FromDefault(component, qcd);
            }

            return quantizations;
        }

        private static void ValidateComponentParameters(
            Jpeg2000SizeSegment siz,
            IReadOnlyList<Jpeg2000ResolvedCodingStyle> codingStyles,
            IReadOnlyList<Jpeg2000ResolvedQuantization> quantizations)
        {
            if (codingStyles.Count != siz.Components.Count || quantizations.Count != siz.Components.Count)
            {
                throw Jpeg2000Binary.CreateException(
                    "JPEG 2000 component marker state does not match the SIZ component count.");
            }

            for (var component = 0; component < siz.Components.Count; component++)
            {
                if (codingStyles[component].ComponentIndex != component
                    || quantizations[component].ComponentIndex != component)
                {
                    throw Jpeg2000Binary.CreateException(
                        "JPEG 2000 component marker state is out of order.");
                }
            }
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
                if (component.FloatSamples != null)
                {
                    component.FloatSamples[i] += shift;
                }
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

        private static void ApplyInverseIct(Jpeg2000StandardComponent[] components)
        {
            var count = components[0].Samples.Length;
            var floatY = components[0].FloatSamples;
            var floatCb = components[1].FloatSamples;
            var floatCr = components[2].FloatSamples;
            for (var i = 0; i < count; i++)
            {
                var y = components[0].Samples[i];
                var cb = components[1].Samples[i];
                var cr = components[2].Samples[i];
                var yf = floatY != null ? floatY[i] : y;
                var cbf = floatCb != null ? floatCb[i] : cb;
                var crf = floatCr != null ? floatCr[i] : cr;
                var r = yf + (1.402 * crf);
                var g = yf - (0.344136286201022 * cbf) - (0.714136286201022 * crf);
                var b = yf + (1.772 * cbf);
                components[0].Samples[i] = RoundSample(r);
                components[1].Samples[i] = RoundSample(g);
                components[2].Samples[i] = RoundSample(b);
                if (floatY != null && floatCb != null && floatCr != null)
                {
                    floatY[i] = r;
                    floatCb[i] = g;
                    floatCr[i] = b;
                }
            }
        }

        private static int RoundSample(double value)
        {
            return value >= 0 ? (int)(value + 0.5) : (int)(value - 0.5);
        }

        private byte[] Pack(DicomPixelData targetPixelData, Jpeg2000StandardComponent[] components)
        {
            var bytesPerSample = targetPixelData.BitsAllocated / 8;
            var pixelCount = components[0].Width * components[0].Height;
            var result = new byte[pixelCount * components.Length * bytesPerSample];
            var precision = components[0].Precision;
            var max = (1 << precision) - 1;
            var targetIsSigned = targetPixelData.PixelRepresentation == PixelRepresentation.Signed;
            var storedMin = targetIsSigned ? -(1 << (targetPixelData.BitsStored - 1)) : 0;
            var storedMax = targetIsSigned
                ? (1 << (targetPixelData.BitsStored - 1)) - 1
                : (1 << targetPixelData.BitsStored) - 1;

            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var component = 0; component < components.Length; component++)
                {
                    var floatSamples = components[component].FloatSamples;
                    var value = floatSamples != null
                        ? RoundSample(floatSamples[pixel])
                        : components[component].Samples[pixel];
                    if (_profile == Jpeg2000DecodeProfile.HighThroughputOpenJph
                        && components[component].Precision != targetPixelData.BitsStored
                        && (value < storedMin || value > storedMax))
                    {
                        if (floatSamples == null)
                        {
                            throw Jpeg2000Binary.CreateException(
                                "HTJ2K decoded sample is outside the DICOM BitsStored range.");
                        }

                        value = Math.Min(storedMax, Math.Max(storedMin, value));
                    }

                    if (components[component].IsSigned)
                    {
                        var min = -(1 << (components[component].Precision - 1));
                        var signedMax = (1 << (components[component].Precision - 1)) - 1;
                        if (value < min)
                        {
                            value = min;
                        }
                        else if (value > signedMax)
                        {
                            value = signedMax;
                        }

                        value &= max;
                    }
                    else
                    {
                        if (value < 0)
                        {
                            value = 0;
                        }
                        else if (value > max)
                        {
                            value = max;
                        }
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

        private void Validate(DicomPixelData targetPixelData, Jpeg2000SizeSegment siz, Jpeg2000CodingStyleDefault cod)
        {
            if (targetPixelData.Width != (int)(siz.ReferenceGridWidth - siz.ImageOffsetX)
                || targetPixelData.Height != (int)(siz.ReferenceGridHeight - siz.ImageOffsetY)
                || targetPixelData.SamplesPerPixel != siz.Components.Count)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 codestream dimensions conflict with DICOM pixel metadata.");
            }

            var targetIsSigned = targetPixelData.PixelRepresentation == PixelRepresentation.Signed;
            for (var component = 0; component < siz.Components.Count; component++)
            {
                var sizeComponent = siz.Components[component];
                var usesAllocatedContainerPrecision = _profile == Jpeg2000DecodeProfile.HighThroughputOpenJph
                    && targetPixelData.BitsStored < targetPixelData.BitsAllocated
                    && sizeComponent.Precision == targetPixelData.BitsAllocated;
                if (sizeComponent.Precision != targetPixelData.BitsStored && !usesAllocatedContainerPrecision)
                {
                    throw Jpeg2000Binary.CreateException(
                        $"JPEG 2000 SIZ component {component} precision conflicts with DICOM BitsStored.");
                }

                if (sizeComponent.IsSigned != targetIsSigned)
                {
                    throw Jpeg2000Binary.CreateException(
                        $"JPEG 2000 SIZ component {component} signedness conflicts with DICOM PixelRepresentation.");
                }
            }

            if (cod.LayerCount <= 0)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 layer count is invalid.");
            }
        }
    }
}
