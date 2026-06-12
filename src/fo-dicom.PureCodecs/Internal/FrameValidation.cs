using System;
using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Internal
{
    internal static class FrameValidation
    {
        public static void EnsureFrameIndex(DicomTransferSyntax syntax, int frame, int frameCount)
        {
            if (frame < 0 || frame >= frameCount)
            {
                throw new DicomCodecException($"{syntax.UID.Name} frame {frame} is outside the available frame range 0..{Math.Max(0, frameCount - 1)}.");
            }
        }
    }
}
