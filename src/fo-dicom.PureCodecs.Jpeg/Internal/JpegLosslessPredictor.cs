namespace FellowOakDicom.PureCodecs.Jpeg.Internal
{
    public static class JpegLosslessPredictor
    {
        public static int PredictSample(
            int selectionValue,
            int samplePrecision,
            int x,
            int y,
            int left,
            int above,
            int upperLeft)
        {
            if (x == 0 && y == 0)
            {
                return 1 << (samplePrecision - 1);
            }

            if (y == 0)
            {
                return left;
            }

            if (x == 0)
            {
                return above;
            }

            return Predict(selectionValue, left, above, upperLeft);
        }

        public static int Predict(int selectionValue, int left, int above, int upperLeft)
        {
            switch (selectionValue)
            {
                case 1:
                    return left;
                case 2:
                    return above;
                case 3:
                    return upperLeft;
                case 4:
                    return left + above - upperLeft;
                case 5:
                    return left + ((above - upperLeft) >> 1);
                case 6:
                    return above + ((left - upperLeft) >> 1);
                case 7:
                    return (left + above) >> 1;
                default:
                    throw JpegMarkerReader.CreateException($"JPEG lossless predictor selection value {selectionValue} is not supported.");
            }
        }
    }
}
