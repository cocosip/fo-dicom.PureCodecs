namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public sealed class Jpeg2000SizeComponent
    {
        public Jpeg2000SizeComponent(int index, int precision, bool isSigned, int horizontalSeparation, int verticalSeparation)
        {
            Index = index;
            Precision = precision;
            IsSigned = isSigned;
            HorizontalSeparation = horizontalSeparation;
            VerticalSeparation = verticalSeparation;
        }

        public int Index { get; }

        public int Precision { get; }

        public bool IsSigned { get; }

        public int HorizontalSeparation { get; }

        public int VerticalSeparation { get; }
    }
}
