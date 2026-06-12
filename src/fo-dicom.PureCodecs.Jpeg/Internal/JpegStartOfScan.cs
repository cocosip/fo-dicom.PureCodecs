using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegStartOfScan
    {
        private JpegStartOfScan(
            JpegScanComponent[] components,
            int spectralSelectionStart,
            int spectralSelectionEnd,
            int successiveApproximationHigh,
            int successiveApproximationLow)
        {
            Components = components;
            SpectralSelectionStart = spectralSelectionStart;
            SpectralSelectionEnd = spectralSelectionEnd;
            SuccessiveApproximationHigh = successiveApproximationHigh;
            SuccessiveApproximationLow = successiveApproximationLow;
        }

        public JpegScanComponent[] Components { get; }

        public int SpectralSelectionStart { get; }

        public int SpectralSelectionEnd { get; }

        public int SuccessiveApproximationHigh { get; }

        public int SuccessiveApproximationLow { get; }

        public static JpegStartOfScan Parse(JpegMarkerSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            var payload = segment.Payload;
            if (payload.Length < 4)
            {
                throw JpegMarkerReader.CreateException("JPEG SOS payload is too short.");
            }

            var componentCount = payload[0];
            var expectedLength = 1 + componentCount * 2 + 3;
            if (payload.Length != expectedLength)
            {
                throw JpegMarkerReader.CreateException("JPEG SOS payload length does not match the component count.");
            }

            var components = new JpegScanComponent[componentCount];
            var offset = 1;
            for (var index = 0; index < components.Length; index++)
            {
                var tableSelector = payload[offset + 1];
                components[index] = new JpegScanComponent(
                    payload[offset],
                    tableSelector >> 4,
                    tableSelector & 0x0F);
                offset += 2;
            }

            var approximation = payload[offset + 2];
            return new JpegStartOfScan(
                components,
                payload[offset],
                payload[offset + 1],
                approximation >> 4,
                approximation & 0x0F);
        }
    }
}
