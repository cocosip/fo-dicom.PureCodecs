namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public readonly struct Jpeg2000SamplePrecision
    {
        public Jpeg2000SamplePrecision(int bitsStored, bool isSigned)
        {
            BitsStored = bitsStored;
            IsSigned = isSigned;
        }

        public int BitsStored { get; }

        public bool IsSigned { get; }

        public int PixelRepresentation => IsSigned ? 1 : 0;

        public static int ToSsiz(int bitsStored, bool isSigned)
        {
            if (bitsStored < 1 || bitsStored > 38)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 Ssiz precision must be between 1 and 38 bits.");
            }

            return (isSigned ? 0x80 : 0) | (bitsStored - 1);
        }

        public static Jpeg2000SamplePrecision FromSsiz(byte ssiz)
        {
            return new Jpeg2000SamplePrecision((ssiz & 0x7F) + 1, (ssiz & 0x80) != 0);
        }

        public static Jpeg2000SamplePrecision ValidateDicomPixelMetadata(
            int bitsAllocated,
            int bitsStored,
            int pixelRepresentation)
        {
            if ((bitsAllocated != 8 && bitsAllocated != 16)
                || bitsStored < 1
                || bitsStored > bitsAllocated
                || (pixelRepresentation != 0 && pixelRepresentation != 1))
            {
                throw Jpeg2000Binary.CreateException(
                    "JPEG 2000 pixel metadata must use 8 or 16 allocated bits, valid stored bits, and pixel representation 0 or 1.");
            }

            return new Jpeg2000SamplePrecision(bitsStored, pixelRepresentation == 1);
        }
    }
}
