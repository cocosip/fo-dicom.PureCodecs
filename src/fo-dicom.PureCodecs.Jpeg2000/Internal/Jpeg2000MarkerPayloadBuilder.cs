namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000MarkerPayloadBuilder
    {
        public static byte[] CreateSize(int width, int height, int bitsStored, bool isSigned, int samplesPerPixel)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt32((uint)width);
            writer.WriteUInt32((uint)height);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32((uint)width);
            writer.WriteUInt32((uint)height);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt16((ushort)samplesPerPixel);
            for (var i = 0; i < samplesPerPixel; i++)
            {
                writer.WriteByte((byte)Jpeg2000SamplePrecision.ToSsiz(bitsStored, isSigned));
                writer.WriteByte(1);
                writer.WriteByte(1);
            }

            return writer.ToArray();
        }

        public static byte[] CreateCodingStyle(
            Jpeg2000ProgressionOrder progressionOrder,
            int layerCount,
            bool usesMultipleComponentTransform,
            int decompositionLevels,
            byte codeBlockStyle,
            byte transformation)
        {
            return new byte[]
            {
                0x00,
                (byte)progressionOrder,
                (byte)(layerCount >> 8),
                (byte)layerCount,
                (byte)(usesMultipleComponentTransform ? 1 : 0),
                (byte)decompositionLevels,
                0x04,
                0x04,
                codeBlockStyle,
                transformation
            };
        }

        public static byte[] CreateStartOfTile(int tileDataLength)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt32((uint)(tileDataLength + 14));
            writer.WriteByte(0);
            writer.WriteByte(1);
            return writer.ToArray();
        }

        public static byte[] CreateReversibleQuantization(int precision, int decompositionLevels)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte(0x40);
            foreach (var gain in SubbandGains(decompositionLevels))
            {
                writer.WriteByte((byte)((precision + gain) << 3));
            }

            return writer.ToArray();
        }

        public static byte[] CreateIrreversibleQuantization(int precision, double[] steps)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte(0x42);
            foreach (var step in steps)
            {
                writer.WriteUInt16(Jpeg2000QuantizationTable.EncodeStepSize(step, precision));
            }

            return writer.ToArray();
        }

        public static byte[] CreateScalarDerivedIrreversibleQuantization(double step, int precision)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte(0x22);
            writer.WriteUInt16(Jpeg2000QuantizationTable.EncodeStepSize(step, precision));
            return writer.ToArray();
        }

        public static byte[] CreateScalarDerivedIrreversibleQuantization(ushort encodedStep)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte(0x22);
            writer.WriteUInt16(encodedStep);
            return writer.ToArray();
        }

        public static byte[] CreateNoQuantization(byte guardAndStyle, byte stepSize)
        {
            return new[] { guardAndStyle, stepSize };
        }

        private static System.Collections.Generic.IEnumerable<int> SubbandGains(int decompositionLevels)
        {
            yield return 0;
            for (var level = 1; level <= decompositionLevels; level++)
            {
                yield return 1;
                yield return 1;
                yield return 2;
            }
        }
    }
}
