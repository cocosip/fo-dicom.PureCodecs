using System;
using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Internal
{
    internal static class CodecFailure
    {
        public static DicomCodecException NotImplemented(DicomTransferSyntax syntax, string operation)
        {
            return new DicomCodecException($"{syntax.UID.Name} {operation} is not implemented by fo-dicom.PureCodecs yet.");
        }

        public static DicomCodecException Wrap(DicomTransferSyntax syntax, string operation, int? frame, Exception exception)
        {
            if (exception is DicomCodecException codecException)
            {
                return codecException;
            }

            var frameText = frame.HasValue ? $" frame {frame.Value}" : string.Empty;
            return new DicomCodecException($"{syntax.UID.Name} {operation}{frameText} failed.", exception);
        }
    }
}
