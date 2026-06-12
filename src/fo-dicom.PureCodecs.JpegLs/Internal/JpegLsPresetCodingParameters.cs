using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsPresetCodingParameters
    {
        private JpegLsPresetCodingParameters(int maximumSampleValue, int threshold1, int threshold2, int threshold3, int reset)
        {
            MaximumSampleValue = maximumSampleValue;
            Threshold1 = threshold1;
            Threshold2 = threshold2;
            Threshold3 = threshold3;
            Reset = reset;
        }

        public int MaximumSampleValue { get; }

        public int Threshold1 { get; }

        public int Threshold2 { get; }

        public int Threshold3 { get; }

        public int Reset { get; }

        public static JpegLsPresetCodingParameters Parse(JpegLsMarkerSegment segment)
        {
            if (segment.Code != JpegLsMarker.LSE)
            {
                throw new DicomCodecException("JPEG-LS LSE preset coding parameters expected.");
            }

            var payload = segment.Payload;
            if (payload.Length != 11 || payload[0] != 1)
            {
                throw new DicomCodecException("JPEG-LS LSE preset coding parameter payload is invalid.");
            }

            return new JpegLsPresetCodingParameters(
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 1),
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 3),
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 5),
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 7),
                JpegLsMarkerReader.ReadUInt16BigEndian(payload, 9));
        }
    }
}
