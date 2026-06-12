using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsRunModeContext
    {
        private readonly int _runInterruptionType;

        public JpegLsRunModeContext(int runInterruptionType, int range)
        {
            _runInterruptionType = runInterruptionType;
            A = Math.Max(2, (range + 32) / 64);
            N = 1;
        }

        public int A { get; private set; }

        public int N { get; private set; }

        public int NegativeErrorCount { get; private set; }

        public int GetGolombParameter()
        {
            var temp = A + (N >> 1) * _runInterruptionType;
            var nTest = N;
            var parameter = 0;
            while (nTest < temp)
            {
                nTest <<= 1;
                parameter++;
            }

            return parameter;
        }

        public bool ComputeMap(int errorValue, int parameter)
        {
            if (parameter == 0 && errorValue > 0 && 2 * NegativeErrorCount < N)
            {
                return true;
            }

            if (errorValue < 0 && 2 * NegativeErrorCount >= N)
            {
                return true;
            }

            return errorValue < 0 && parameter != 0;
        }

        public int ComputeErrorValue(int mappedValue, int parameter)
        {
            var mapBit = mappedValue & 1;
            var absoluteValue = (mappedValue + mapBit) / 2;
            var mapCondition = parameter != 0 || 2 * NegativeErrorCount >= N;
            return mapCondition == (mapBit != 0) ? -absoluteValue : absoluteValue;
        }

        public void Update(int errorValue, int mappedErrorValue, int resetThreshold)
        {
            if (errorValue < 0)
            {
                NegativeErrorCount++;
            }

            A += (mappedErrorValue + 1 - _runInterruptionType) >> 1;
            if (N == resetThreshold)
            {
                A >>= 1;
                N >>= 1;
                NegativeErrorCount >>= 1;
            }

            N++;
        }
    }
}
