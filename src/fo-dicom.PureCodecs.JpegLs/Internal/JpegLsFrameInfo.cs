using System.Collections.Generic;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsFrameInfo
    {
        private JpegLsFrameInfo(int bitsPerSample, int height, int width, JpegLsFrameComponent[] components)
        {
            BitsPerSample = bitsPerSample;
            Height = height;
            Width = width;
            Components = components;
        }

        public int BitsPerSample { get; }

        public int Height { get; }

        public int Width { get; }

        public IReadOnlyList<JpegLsFrameComponent> Components { get; }

        public static JpegLsFrameInfo Parse(JpegLsMarkerSegment segment)
        {
            if (segment.Code != JpegLsMarker.SOF55)
            {
                throw new DicomCodecException("JPEG-LS SOF55 frame info expected.");
            }

            var payload = segment.Payload;
            if (payload.Length < 6)
            {
                throw new DicomCodecException("JPEG-LS SOF55 payload is too short.");
            }

            var componentCount = payload[5];
            var expectedLength = 6 + componentCount * 3;
            if (payload.Length != expectedLength)
            {
                throw new DicomCodecException("JPEG-LS SOF55 component payload length is invalid.");
            }

            var components = new JpegLsFrameComponent[componentCount];
            var offset = 6;
            for (var i = 0; i < componentCount; i++)
            {
                var sampling = payload[offset + 1];
                components[i] = new JpegLsFrameComponent(
                    payload[offset],
                    sampling >> 4,
                    sampling & 0x0F,
                    payload[offset + 2]);
                offset += 3;
            }

            return new JpegLsFrameInfo(
                payload[0],
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 1),
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 3),
                components);
        }
    }
}
