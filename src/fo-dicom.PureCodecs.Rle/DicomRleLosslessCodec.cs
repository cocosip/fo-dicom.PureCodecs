using FellowOakDicom;
using FellowOakDicom.PureCodecs.Internal;

namespace FellowOakDicom.PureCodecs.Rle
{
    public sealed class DicomRleLosslessCodec : UnimplementedDicomCodec
    {
        public DicomRleLosslessCodec()
            : base(DicomTransferSyntax.RLELossless)
        {
        }
    }
}
