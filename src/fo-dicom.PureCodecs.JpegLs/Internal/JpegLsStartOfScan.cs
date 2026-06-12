using System.Collections.Generic;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsStartOfScan
    {
        private JpegLsStartOfScan(JpegLsScanComponent[] components, int nearLossless, JpegLsInterleaveMode interleaveMode, int pointTransform)
        {
            Components = components;
            NearLossless = nearLossless;
            InterleaveMode = interleaveMode;
            PointTransform = pointTransform;
        }

        public IReadOnlyList<JpegLsScanComponent> Components { get; }

        public int NearLossless { get; }

        public JpegLsInterleaveMode InterleaveMode { get; }

        public int PointTransform { get; }

        public static JpegLsStartOfScan Parse(JpegLsMarkerSegment segment)
        {
            if (segment.Code != JpegLsMarker.SOS)
            {
                throw new DicomCodecException("JPEG-LS SOS expected.");
            }

            var payload = segment.Payload;
            if (payload.Length < 4)
            {
                throw new DicomCodecException("JPEG-LS SOS payload is too short.");
            }

            var componentCount = payload[0];
            var expectedLength = 1 + componentCount * 2 + 3;
            if (payload.Length != expectedLength)
            {
                throw new DicomCodecException("JPEG-LS SOS component payload length is invalid.");
            }

            var components = new JpegLsScanComponent[componentCount];
            var offset = 1;
            for (var i = 0; i < componentCount; i++)
            {
                components[i] = new JpegLsScanComponent(payload[offset], payload[offset + 1]);
                offset += 2;
            }

            var interleaveMode = payload[offset + 1];
            if (interleaveMode > 2)
            {
                throw new DicomCodecException("JPEG-LS SOS interleave mode is invalid.");
            }

            return new JpegLsStartOfScan(
                components,
                payload[offset],
                (JpegLsInterleaveMode)interleaveMode,
                payload[offset + 2]);
        }
    }
}
