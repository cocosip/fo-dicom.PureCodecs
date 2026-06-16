using System.Collections.Generic;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.PureCodecs.Jpeg;
using FellowOakDicom.PureCodecs.Jpeg2000;
using FellowOakDicom.PureCodecs.JpegLs;
using FellowOakDicom.PureCodecs.Rle;

namespace FellowOakDicom.PureCodecs
{
    /// <summary>
    /// Pure managed transcoder manager entry point for fo-dicom.
    /// </summary>
    public sealed class PureTranscoderManager : TranscoderManager, ITranscoderManager
    {
        private static readonly HashSet<DicomTransferSyntax> UnsupportedJpeg2000TransferSyntaxes =
            new HashSet<DicomTransferSyntax>
            {
                DicomTransferSyntax.HTJ2KLossless,
                DicomTransferSyntax.HTJ2KLosslessRPCL,
                DicomTransferSyntax.HTJ2K,
                DicomTransferSyntax.JPEG2000Part2MultiComponentLosslessOnly,
                DicomTransferSyntax.JPEG2000Part2MultiComponent,
                DicomTransferSyntax.JPIPReferenced,
                DicomTransferSyntax.JPIPReferencedDeflate,
                DicomTransferSyntax.JPIPHTJ2KReferenced,
                DicomTransferSyntax.JPIPHTJ2KReferencedDeflate
            };

        public PureTranscoderManager()
        {
            LoadCodecs();
        }

        public new bool HasCodec(DicomTransferSyntax syntax)
        {
            return !UnsupportedJpeg2000TransferSyntaxes.Contains(syntax) && base.HasCodec(syntax);
        }

        public new bool CanTranscode(DicomTransferSyntax inSyntax, DicomTransferSyntax outSyntax)
        {
            return !UnsupportedJpeg2000TransferSyntaxes.Contains(inSyntax)
                && !UnsupportedJpeg2000TransferSyntaxes.Contains(outSyntax)
                && base.CanTranscode(inSyntax, outSyntax);
        }

        public new IDicomCodec GetCodec(DicomTransferSyntax syntax)
        {
            if (UnsupportedJpeg2000TransferSyntaxes.Contains(syntax))
            {
                throw new DicomCodecException($"No codec registered for unsupported transfer syntax: {syntax}");
            }

            return base.GetCodec(syntax);
        }

        bool ITranscoderManager.HasCodec(DicomTransferSyntax syntax)
        {
            return HasCodec(syntax);
        }

        bool ITranscoderManager.CanTranscode(DicomTransferSyntax inSyntax, DicomTransferSyntax outSyntax)
        {
            return CanTranscode(inSyntax, outSyntax);
        }

        IDicomCodec ITranscoderManager.GetCodec(DicomTransferSyntax syntax)
        {
            return GetCodec(syntax);
        }

        public override void LoadCodecs(string? path = null, string? search = null)
        {
            Codecs.Clear();

            AddCodec(new DicomRleLosslessCodec());

            AddCodec(new DicomJpegProcess1Codec());
            AddCodec(new DicomJpegProcess2_4Codec());
            AddCodec(new DicomJpegLossless14Codec());
            AddCodec(new DicomJpegLossless14SV1Codec());

            AddCodec(new DicomJpegLsLosslessCodec());
            AddCodec(new DicomJpegLsNearLosslessCodec());

            AddCodec(new DicomJpeg2000LosslessCodec());
            AddCodec(new DicomJpeg2000LossyCodec());
        }

        private void AddCodec(IDicomCodec codec)
        {
            Codecs[codec.TransferSyntax] = codec;
        }
    }
}
