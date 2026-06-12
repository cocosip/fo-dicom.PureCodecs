using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsRunModeScanner
    {
        private static readonly int[] RunIndexBitCounts =
        {
            0, 0, 0, 0, 1, 1, 1, 1,
            2, 2, 2, 2, 3, 3, 3, 3,
            4, 4, 5, 5, 6, 6, 7, 7,
            8, 9, 10, 11, 12, 13, 14, 15,
        };

        private readonly JpegLsTraits _traits;

        public JpegLsRunModeScanner(JpegLsTraits traits)
            : this(traits, null)
        {
        }

        public JpegLsRunModeScanner(JpegLsTraits traits, JpegLsRunModeContext[]? runInterruptionContexts)
        {
            _traits = traits;
            RunInterruptionContexts = runInterruptionContexts ?? new[]
            {
                new JpegLsRunModeContext(0, traits.Range),
                new JpegLsRunModeContext(1, traits.Range),
            };
        }

        public JpegLsRunModeContext[] RunInterruptionContexts { get; }

        public int RunIndex { get; private set; }

        public void EncodeRunLength(JpegLsGolombCodeWriter writer, int runLength, bool endOfLine)
        {
            while (runLength >= (1 << RunIndexBitCounts[RunIndex]))
            {
                writer.WriteBit(1);
                runLength -= 1 << RunIndexBitCounts[RunIndex];
                IncrementRunIndex();
            }

            if (endOfLine)
            {
                if (runLength != 0)
                {
                    writer.WriteBit(1);
                }

                return;
            }

            writer.WriteBits(runLength, RunIndexBitCounts[RunIndex] + 1);
        }

        public int DecodeRunLength(JpegLsGolombCodeReader reader, int remainingInLine)
        {
            var runLength = 0;
            while (reader.ReadBit() == 1)
            {
                var count = Math.Min(1 << RunIndexBitCounts[RunIndex], remainingInLine - runLength);
                runLength += count;
                if (count == (1 << RunIndexBitCounts[RunIndex]))
                {
                    IncrementRunIndex();
                }

                if (runLength >= remainingInLine)
                {
                    return remainingInLine;
                }
            }

            if (RunIndexBitCounts[RunIndex] > 0)
            {
                runLength += reader.ReadBits(RunIndexBitCounts[RunIndex]);
            }

            return runLength > remainingInLine
                ? throw new InvalidOperationException("JPEG-LS run length exceeds the remaining line length.")
                : runLength;
        }

        public void EncodeRunInterruption(JpegLsGolombCodeWriter writer, JpegLsRunModeContext context, int errorValue)
        {
            var parameter = context.GetGolombParameter();
            var map = context.ComputeMap(errorValue, parameter);
            var mappedError = 2 * Math.Abs(errorValue) - (ReferenceEquals(context, RunInterruptionContexts[1]) ? 1 : 0);
            if (map)
            {
                mappedError--;
            }

            writer.EncodeMappedValue(parameter, mappedError, _traits.Limit - RunIndexBitCounts[RunIndex] - 1, _traits.QuantizedBitsPerPixel);
            context.Update(errorValue, mappedError, _traits.ResetThreshold);
        }

        public int DecodeRunInterruption(JpegLsGolombCodeReader reader, JpegLsRunModeContext context)
        {
            var parameter = context.GetGolombParameter();
            var runInterruptionType = ReferenceEquals(context, RunInterruptionContexts[1]) ? 1 : 0;
            var mapped = reader.DecodeMappedValue(parameter, _traits.Limit - RunIndexBitCounts[RunIndex] - 1, _traits.QuantizedBitsPerPixel);
            var errorValue = context.ComputeErrorValue(mapped + runInterruptionType, parameter);
            context.Update(errorValue, mapped, _traits.ResetThreshold);
            return errorValue;
        }

        public void DecrementRunIndex()
        {
            if (RunIndex > 0)
            {
                RunIndex--;
            }
        }

        private void IncrementRunIndex()
        {
            if (RunIndex < RunIndexBitCounts.Length - 1)
            {
                RunIndex++;
            }
        }
    }
}
