using FellowOakDicom.Imaging;

namespace FellowOakDicom.PureCodecs.Internal
{
    internal sealed class PixelMetadataSnapshot
    {
        private PixelMetadataSnapshot(
            ushort width,
            ushort height,
            int numberOfFrames,
            ushort bitsAllocated,
            ushort bitsStored,
            ushort samplesPerPixel,
            PixelRepresentation pixelRepresentation,
            PlanarConfiguration planarConfiguration,
            PhotometricInterpretation photometricInterpretation)
        {
            Width = width;
            Height = height;
            NumberOfFrames = numberOfFrames;
            BitsAllocated = bitsAllocated;
            BitsStored = bitsStored;
            SamplesPerPixel = samplesPerPixel;
            PixelRepresentation = pixelRepresentation;
            PlanarConfiguration = planarConfiguration;
            PhotometricInterpretation = photometricInterpretation;
        }

        public ushort Width { get; }

        public ushort Height { get; }

        public int NumberOfFrames { get; }

        public ushort BitsAllocated { get; }

        public ushort BitsStored { get; }

        public ushort SamplesPerPixel { get; }

        public PixelRepresentation PixelRepresentation { get; }

        public PlanarConfiguration PlanarConfiguration { get; }

        public PhotometricInterpretation PhotometricInterpretation { get; }

        public static PixelMetadataSnapshot From(DicomPixelData pixelData)
        {
            return new PixelMetadataSnapshot(
                pixelData.Width,
                pixelData.Height,
                pixelData.NumberOfFrames,
                pixelData.BitsAllocated,
                pixelData.BitsStored,
                pixelData.SamplesPerPixel,
                pixelData.PixelRepresentation,
                pixelData.PlanarConfiguration,
                pixelData.PhotometricInterpretation);
        }
    }
}
