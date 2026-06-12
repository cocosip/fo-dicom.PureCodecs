using System;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsTraits
    {
        private JpegLsTraits(
            int maximumSampleValue,
            int nearLossless,
            int range,
            int quantizedBitsPerPixel,
            int limit,
            int resetThreshold,
            int threshold1,
            int threshold2,
            int threshold3)
        {
            MaximumSampleValue = maximumSampleValue;
            NearLossless = nearLossless;
            Range = range;
            QuantizedBitsPerPixel = quantizedBitsPerPixel;
            Limit = limit;
            ResetThreshold = resetThreshold;
            Threshold1 = threshold1;
            Threshold2 = threshold2;
            Threshold3 = threshold3;
        }

        public int MaximumSampleValue { get; }

        public int NearLossless { get; }

        public int Range { get; }

        public int QuantizedBitsPerPixel { get; }

        public int Limit { get; }

        public int ResetThreshold { get; }

        public int Threshold1 { get; }

        public int Threshold2 { get; }

        public int Threshold3 { get; }

        public static JpegLsTraits CreateDefault(int maximumSampleValue, int nearLossless, int resetThreshold)
        {
            if (maximumSampleValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSampleValue), "JPEG-LS maximum sample value must be positive.");
            }

            if (nearLossless < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nearLossless), "JPEG-LS NEAR cannot be negative.");
            }

            if (resetThreshold <= 0)
            {
                resetThreshold = 64;
            }

            var range = maximumSampleValue + 1;
            if (nearLossless > 0)
            {
                range = (maximumSampleValue + 2 * nearLossless) / (2 * nearLossless + 1) + 1;
            }

            var quantizedBitsPerPixel = BitsLength(range);
            var bitsPerPixel = BitsLength(maximumSampleValue);
            var limit = 2 * (bitsPerPixel + Math.Max(8, bitsPerPixel));
            ComputeThresholds(maximumSampleValue, nearLossless, out var threshold1, out var threshold2, out var threshold3);

            return new JpegLsTraits(
                maximumSampleValue,
                nearLossless,
                range,
                quantizedBitsPerPixel,
                limit,
                resetThreshold,
                threshold1,
                threshold2,
                threshold3);
        }

        public int CorrectPrediction(int prediction)
        {
            if (prediction < 0)
            {
                return 0;
            }

            return prediction > MaximumSampleValue ? MaximumSampleValue : prediction;
        }

        public int ComputeErrorValue(int errorValue)
        {
            return ModuloRange(Quantize(errorValue));
        }

        public int ComputeReconstructedSample(int prediction, int errorValue)
        {
            var value = prediction + errorValue * (2 * NearLossless + 1);
            if (NearLossless == 0 && ((MaximumSampleValue + 1) & MaximumSampleValue) == 0)
            {
                return value & MaximumSampleValue;
            }

            if (value < -NearLossless)
            {
                value += Range * (2 * NearLossless + 1);
            }
            else if (value > MaximumSampleValue + NearLossless)
            {
                value -= Range * (2 * NearLossless + 1);
            }

            return CorrectPrediction(value);
        }

        public int ModuloRange(int errorValue)
        {
            if (errorValue < 0)
            {
                errorValue += Range;
            }

            if (errorValue >= (Range + 1) / 2)
            {
                errorValue -= Range;
            }

            return errorValue;
        }

        public bool IsNear(int left, int right)
        {
            return Math.Abs(left - right) <= NearLossless;
        }

        private int Quantize(int errorValue)
        {
            if (NearLossless == 0)
            {
                return errorValue;
            }

            if (errorValue > 0)
            {
                return (errorValue + NearLossless) / (2 * NearLossless + 1);
            }

            return -((NearLossless - errorValue) / (2 * NearLossless + 1));
        }

        private static void ComputeThresholds(int maximumSampleValue, int nearLossless, out int threshold1, out int threshold2, out int threshold3)
        {
            const int threshold1Default = 3;
            const int threshold2Default = 7;
            const int threshold3Default = 21;

            if (maximumSampleValue >= 128)
            {
                var factor = (Math.Min(maximumSampleValue, 4095) + 128) / 256;
                threshold1 = Clamp(factor * (threshold1Default - 2) + 2 + 3 * nearLossless, nearLossless + 1, maximumSampleValue);
                threshold2 = Clamp(factor * (threshold2Default - 3) + 3 + 5 * nearLossless, threshold1, maximumSampleValue);
                threshold3 = Clamp(factor * (threshold3Default - 4) + 4 + 7 * nearLossless, threshold2, maximumSampleValue);
                return;
            }

            var lowFactor = 256 / (maximumSampleValue + 1);
            threshold1 = Clamp(Math.Max(2, threshold1Default / lowFactor + 3 * nearLossless), nearLossless + 1, maximumSampleValue);
            threshold2 = Clamp(Math.Max(3, threshold2Default / lowFactor + 5 * nearLossless), threshold1, maximumSampleValue);
            threshold3 = Clamp(Math.Max(4, threshold3Default / lowFactor + 7 * nearLossless), threshold2, maximumSampleValue);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private static int BitsLength(int value)
        {
            if (value <= 1)
            {
                return 1;
            }

            var length = 0;
            value--;
            while (value > 0)
            {
                value >>= 1;
                length++;
            }

            return length;
        }
    }
}
