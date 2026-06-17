namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000MarkerPayloadBuilder
    {
        public static byte[] CreateSize(int width, int height, int bitsStored, bool isSigned, int samplesPerPixel)
        {
            return CreateSize(width, height, bitsStored, isSigned, samplesPerPixel, capabilities: 0);
        }

        public static byte[] CreateSize(int width, int height, int bitsStored, bool isSigned, int samplesPerPixel, ushort capabilities)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(capabilities);
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

        public static byte[] CreateHighThroughputCapabilities(bool reversible, int magnitudeBound)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt32(0x00020000);
            writer.WriteUInt16(CreateHighThroughputCcap(reversible, magnitudeBound));
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
            return CreateStartOfTile(tileDataLength, tilePartIndex: 0, tilePartCount: 1);
        }

        public static byte[] CreateStartOfTile(int tileDataLength, int tilePartIndex, int tilePartCount)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt32((uint)(tileDataLength + 14));
            writer.WriteByte((byte)tilePartIndex);
            writer.WriteByte((byte)tilePartCount);
            return writer.ToArray();
        }

        public static byte[] CreateTilePartLengths(int[] tileDataLengths)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte(0);
            writer.WriteByte(0x60);
            if (tileDataLengths != null)
            {
                for (var i = 0; i < tileDataLengths.Length; i++)
                {
                    writer.WriteUInt16(0);
                    writer.WriteUInt32((uint)(tileDataLengths[i] + 14));
                }
            }

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

        public static byte[] CreateReversibleQuantizationFromExponentBits(int[] exponentBits)
        {
            var writer = new Jpeg2000ByteWriter();
            var guardBits = 1;
            if (exponentBits != null)
            {
                for (var i = 0; i < exponentBits.Length; i++)
                {
                    guardBits = System.Math.Max(guardBits, exponentBits[i] - 31);
                }
            }

            writer.WriteByte((byte)(guardBits << 5));
            if (exponentBits != null)
            {
                for (var i = 0; i < exponentBits.Length; i++)
                {
                    writer.WriteByte((byte)((exponentBits[i] - guardBits) << 3));
                }
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

        public static byte[] CreateScalarExpoundedIrreversibleQuantization(ushort[] encodedSteps)
        {
            return CreateScalarExpoundedIrreversibleQuantization(encodedSteps, guardBits: 2);
        }

        public static byte[] CreateScalarExpoundedIrreversibleQuantization(ushort[] encodedSteps, int guardBits)
        {
            var writer = new Jpeg2000ByteWriter();
            writer.WriteByte((byte)((guardBits << 5) | 0x02));
            if (encodedSteps != null)
            {
                foreach (var step in encodedSteps)
                {
                    writer.WriteUInt16(step);
                }
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

        public static int ComputeReversibleMagnitudeBound(byte[] qcdPayload)
        {
            var bound = 0;
            if (qcdPayload != null && qcdPayload.Length > 0)
            {
                var guardBits = qcdPayload[0] >> 5;
                for (var i = 1; i < qcdPayload.Length; i++)
                {
                    bound = System.Math.Max(bound, (qcdPayload[i] >> 3) + guardBits - 1);
                }
            }

            return bound;
        }

        public static int ComputeScalarExpoundedMagnitudeBound(ushort[] encodedSteps, int guardBits, int decompositionLevels)
        {
            var bound = 0;
            if (encodedSteps != null)
            {
                for (var i = 0; i < encodedSteps.Length; i++)
                {
                    var subbandDecompositionLevel = decompositionLevels - (i == 0 ? 0 : (i - 1) / 3);
                    bound = System.Math.Max(bound, (encodedSteps[i] >> 11) + guardBits - subbandDecompositionLevel);
                }
            }

            return bound;
        }

        private static ushort CreateHighThroughputCcap(bool reversible, int magnitudeBound)
        {
            var ccap = reversible ? 0 : 0x0020;
            var bp = magnitudeBound <= 8
                ? 0
                : magnitudeBound < 28
                    ? magnitudeBound - 8
                    : 13 + (magnitudeBound >> 2);
            return (ushort)(ccap | bp);
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
