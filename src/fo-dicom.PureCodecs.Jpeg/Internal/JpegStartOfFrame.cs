using System;

namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public sealed class JpegStartOfFrame
    {
        private JpegStartOfFrame(byte marker, int samplePrecision, int height, int width, JpegFrameComponent[] components)
        {
            Marker = marker;
            SamplePrecision = samplePrecision;
            Height = height;
            Width = width;
            Components = components;
        }

        public byte Marker { get; }

        public int SamplePrecision { get; }

        public int Height { get; }

        public int Width { get; }

        public JpegFrameComponent[] Components { get; }

        public static JpegStartOfFrame Parse(JpegMarkerSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            var payload = segment.Payload;
            if (payload.Length < 6)
            {
                throw JpegMarkerReader.CreateException("JPEG SOF payload is too short.");
            }

            var componentCount = payload[5];
            var expectedLength = 6 + componentCount * 3;
            if (payload.Length != expectedLength)
            {
                throw JpegMarkerReader.CreateException("JPEG SOF payload length does not match the component count.");
            }

            var components = new JpegFrameComponent[componentCount];
            var offset = 6;
            for (var index = 0; index < components.Length; index++)
            {
                var sampling = payload[offset + 1];
                components[index] = new JpegFrameComponent(
                    payload[offset],
                    sampling >> 4,
                    sampling & 0x0F,
                    payload[offset + 2]);
                offset += 3;
            }

            return new JpegStartOfFrame(
                segment.Code,
                payload[0],
                JpegMarkerReader.ReadUInt16BigEndian(payload, 1),
                JpegMarkerReader.ReadUInt16BigEndian(payload, 3),
                components);
        }
    }
}
