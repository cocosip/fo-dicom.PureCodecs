using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000RateDistortionPass
    {
        public Jpeg2000RateDistortionPass(int index, int byteLength, double distortionReduction)
        {
            Index = index;
            ByteLength = byteLength;
            DistortionReduction = distortionReduction;
        }

        public int Index { get; }

        public int ByteLength { get; }

        public double DistortionReduction { get; }
    }

    public sealed class Jpeg2000RateControlOptions
    {
        public Jpeg2000RateControlOptions(double rate, int rateLevels, double targetRatio, int numLayers, bool includeFinalLosslessLayer)
        {
            Rate = rate;
            RateLevels = rateLevels;
            TargetRatio = targetRatio;
            NumLayers = numLayers;
            IncludeFinalLosslessLayer = includeFinalLosslessLayer;
        }

        public double Rate { get; }

        public int RateLevels { get; }

        public double TargetRatio { get; }

        public int NumLayers { get; }

        public bool IncludeFinalLosslessLayer { get; }
    }

    public sealed class Jpeg2000QualityLayer
    {
        public Jpeg2000QualityLayer(IReadOnlyList<Jpeg2000RateDistortionPass> passes, bool isFinalLosslessLayer)
        {
            Passes = passes ?? new Jpeg2000RateDistortionPass[0];
            IsFinalLosslessLayer = isFinalLosslessLayer;
        }

        public IReadOnlyList<Jpeg2000RateDistortionPass> Passes { get; }

        public bool IsFinalLosslessLayer { get; }

        public int TotalBytes
        {
            get
            {
                var total = 0;
                foreach (var pass in Passes)
                {
                    total += pass.ByteLength;
                }

                return total;
            }
        }
    }

    public static class Jpeg2000PcrdLayerAllocator
    {
        public static IReadOnlyList<Jpeg2000QualityLayer> Allocate(
            IReadOnlyList<Jpeg2000RateDistortionPass> passes,
            Jpeg2000RateControlOptions options,
            int totalUncompressedBytes)
        {
            var layerCount = options.NumLayers > 0 ? options.NumLayers : 1;
            var targetBytes = ResolveTargetBytes(options, totalUncompressedBytes);
            var layers = new List<Jpeg2000QualityLayer>();
            var nextPass = 0;

            for (var layer = 0; layer < layerCount; layer++)
            {
                var layerPasses = new List<Jpeg2000RateDistortionPass>();
                var layerBytes = 0;
                while (nextPass < passes.Count && layerBytes + passes[nextPass].ByteLength <= targetBytes)
                {
                    layerPasses.Add(passes[nextPass]);
                    layerBytes += passes[nextPass].ByteLength;
                    nextPass++;
                }

                layers.Add(new Jpeg2000QualityLayer(layerPasses, isFinalLosslessLayer: false));
            }

            if (options.IncludeFinalLosslessLayer)
            {
                layers.Add(new Jpeg2000QualityLayer(passes, isFinalLosslessLayer: true));
            }

            return layers;
        }

        private static int ResolveTargetBytes(Jpeg2000RateControlOptions options, int totalUncompressedBytes)
        {
            if (options.TargetRatio > 0)
            {
                return System.Math.Max(1, (int)(totalUncompressedBytes / options.TargetRatio));
            }

            if (options.Rate > 0)
            {
                return System.Math.Max(1, (int)(totalUncompressedBytes * options.Rate));
            }

            return totalUncompressedBytes;
        }
    }
}
