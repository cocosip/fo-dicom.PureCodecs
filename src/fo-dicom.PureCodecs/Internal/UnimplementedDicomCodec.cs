using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Internal
{
    public abstract class UnimplementedDicomCodec : IDicomCodec
    {
        protected UnimplementedDicomCodec(DicomTransferSyntax transferSyntax)
        {
            TransferSyntax = transferSyntax;
        }

        public string Name => TransferSyntax.UID.Name;

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DefaultPureCodecParams();
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            throw CodecFailure.NotImplemented(TransferSyntax, "encode");
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            throw CodecFailure.NotImplemented(TransferSyntax, "decode");
        }
    }
}
