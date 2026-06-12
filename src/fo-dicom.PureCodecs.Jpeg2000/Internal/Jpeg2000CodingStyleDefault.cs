namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000CodingStyleDefault : Jpeg2000CodingStyle
    {
        private Jpeg2000CodingStyleDefault(Jpeg2000CodingStyle style)
            : base(
                style.HasPrecinctSizes,
                style.ProgressionOrder,
                style.LayerCount,
                style.UsesMultipleComponentTransform,
                style.DecompositionLevels,
                style.CodeBlockWidth,
                style.CodeBlockHeight,
                style.CodeBlockStyle,
                style.Transformation,
                CopyPrecincts(style))
        {
        }

        public static Jpeg2000CodingStyleDefault Parse(Jpeg2000MarkerSegment segment)
        {
            if (segment.Code != Jpeg2000Marker.COD)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COD marker segment expected.");
            }

            var payload = segment.Payload;
            if (payload.Length < 7)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 COD payload is too short.");
            }

            var scod = payload[0];
            var progressionOrder = ReadProgressionOrder(payload[1]);
            var layerCount = Jpeg2000Binary.ReadUInt16(payload, 2);
            var usesMct = payload[4] != 0;
            return new Jpeg2000CodingStyleDefault(ParseStyle(payload, 5, scod, progressionOrder, layerCount, usesMct));
        }

        private static byte[] CopyPrecincts(Jpeg2000CodingStyle style)
        {
            var values = new byte[style.PrecinctSizes.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = style.PrecinctSizes[i];
            }

            return values;
        }
    }
}
