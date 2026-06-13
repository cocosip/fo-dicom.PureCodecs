using System;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    public abstract class DicomJpeg2000ClassicCodecBase : IDicomCodec
    {
        private readonly bool _defaultIrreversible;
        private readonly Jpeg2000ClassicFrameCodec _frameCodec = new Jpeg2000ClassicFrameCodec();

        protected DicomJpeg2000ClassicCodecBase(DicomTransferSyntax transferSyntax, bool defaultIrreversible)
        {
            TransferSyntax = transferSyntax ?? throw new ArgumentNullException(nameof(transferSyntax));
            _defaultIrreversible = defaultIrreversible;
        }

        public string Name
        {
            get { return TransferSyntax.UID.Name; }
        }

        public DicomTransferSyntax TransferSyntax { get; }

        public DicomCodecParams GetDefaultParameters()
        {
            return new DicomJpeg2000Params { Irreversible = _defaultIrreversible };
        }

        public void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            var jpeg2000Parameters = DicomJpeg2000Params.From(parameters ?? GetDefaultParameters());
            var irreversible = jpeg2000Parameters.Irreversible;
            var tolerance = ResolveTolerance(jpeg2000Parameters);
            var layerCount = ResolveLayerCount(jpeg2000Parameters);
            var usesMct = oldPixelData.SamplesPerPixel == 3 && jpeg2000Parameters.AllowMCT;

            ValidateSupportedPixelData(oldPixelData);
            UpdateCompressedPixelDataMetadata(oldPixelData, newPixelData, jpeg2000Parameters, irreversible, usesMct);

            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var encoded = _frameCodec.EncodeFrame(
                        oldPixelData,
                        NormalizeFrameForEncode(oldPixelData, ToArray(oldPixelData.GetFrame(frame))),
                        irreversible,
                        tolerance,
                        jpeg2000Parameters.ProgressionOrder,
                        layerCount,
                        usesMct,
                        jpeg2000Parameters.EncodeSignedPixelValuesAsUnsigned);
                    newPixelData.AddFrame(new MemoryByteBuffer(encoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("encode", frame, exception);
                }
            }
        }

        public void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData, DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                try
                {
                    var decoded = _frameCodec.DecodeFrame(newPixelData, ToArray(oldPixelData.GetFrame(frame)));
                    decoded = NormalizeFrameForDecode(newPixelData, decoded);
                    newPixelData.AddFrame(new MemoryByteBuffer(decoded));
                }
                catch (Exception exception)
                {
                    throw Wrap("decode", frame, exception);
                }
            }
        }

        private static int ResolveTolerance(DicomJpeg2000Params parameters)
        {
            if (parameters.TargetRatio > 1)
            {
                return Math.Max(1, (int)Math.Ceiling(parameters.TargetRatio - 1));
            }

            if (parameters.Rate > 0 && parameters.Rate < 1)
            {
                return Math.Max(1, (int)Math.Ceiling(1d / parameters.Rate));
            }

            if (parameters.Rate > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(20d / parameters.Rate));
            }

            return 1;
        }

        private int ResolveLayerCount(DicomJpeg2000Params parameters)
        {
            var layerCount = 1;
            if (parameters.RateLevels != null)
            {
                layerCount = 0;
                foreach (var rateLevel in parameters.RateLevels)
                {
                    if (rateLevel > parameters.Rate)
                    {
                        layerCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                layerCount++;
            }

            if (TransferSyntax == DicomTransferSyntax.JPEG2000Lossless && parameters.Rate > 0)
            {
                layerCount++;
            }

            return Math.Max(1, layerCount);
        }

        private static void ValidateSupportedPixelData(DicomPixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (pixelData.SamplesPerPixel != 1 && pixelData.SamplesPerPixel != 3)
            {
                throw new DicomCodecException($"JPEG 2000 classic codec does not support SamplesPerPixel {pixelData.SamplesPerPixel}.");
            }

            if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
            {
                throw new DicomCodecException("JPEG 2000 classic codec supports only 8-bit and 16-bit allocated samples.");
            }

            var photometric = pixelData.PhotometricInterpretation?.Value;
            if (photometric != PhotometricInterpretation.Monochrome1.Value
                && photometric != PhotometricInterpretation.Monochrome2.Value
                && photometric != PhotometricInterpretation.Rgb.Value
                && photometric != PhotometricInterpretation.YbrIct.Value
                && photometric != PhotometricInterpretation.YbrRct.Value
                && photometric != "YBR_FULL"
                && photometric != "YBR_FULL_422")
            {
                throw new DicomCodecException($"JPEG 2000 classic codec does not support photometric interpretation {photometric ?? "<missing>"}.");
            }
        }

        private static void UpdateCompressedPixelDataMetadata(
            DicomPixelData oldPixelData,
            DicomPixelData newPixelData,
            DicomJpeg2000Params parameters,
            bool irreversible,
            bool usesMct)
        {
            if (oldPixelData.SamplesPerPixel != 3)
            {
                return;
            }

            newPixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
            if (!usesMct || !parameters.UpdatePhotometricInterpretation)
            {
                return;
            }

            newPixelData.PhotometricInterpretation = irreversible
                ? PhotometricInterpretation.YbrIct
                : PhotometricInterpretation.YbrRct;
        }

        private static byte[] NormalizeFrameForEncode(DicomPixelData pixelData, byte[] frame)
        {
            if (pixelData.SamplesPerPixel == 3 && pixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                return PlanarRgbToInterleaved(frame, pixelData.Width * pixelData.Height);
            }

            return frame;
        }

        private static byte[] NormalizeFrameForDecode(DicomPixelData targetPixelData, byte[] frame)
        {
            if (targetPixelData.SamplesPerPixel == 3 && targetPixelData.PlanarConfiguration == PlanarConfiguration.Planar)
            {
                return InterleavedRgbToPlanar(frame, targetPixelData.Width * targetPixelData.Height);
            }

            return frame;
        }

        private static byte[] PlanarRgbToInterleaved(byte[] planar, int pixelCount)
        {
            if (planar.Length != pixelCount * 3)
            {
                throw new DicomCodecException("JPEG 2000 planar RGB frame length does not match pixel count.");
            }

            var interleaved = new byte[planar.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                interleaved[pixel * 3] = planar[pixel];
                interleaved[pixel * 3 + 1] = planar[pixelCount + pixel];
                interleaved[pixel * 3 + 2] = planar[pixelCount * 2 + pixel];
            }

            return interleaved;
        }

        private static byte[] InterleavedRgbToPlanar(byte[] interleaved, int pixelCount)
        {
            if (interleaved.Length != pixelCount * 3)
            {
                throw new DicomCodecException("JPEG 2000 interleaved RGB frame length does not match pixel count.");
            }

            var planar = new byte[interleaved.Length];
            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                planar[pixel] = interleaved[pixel * 3];
                planar[pixelCount + pixel] = interleaved[pixel * 3 + 1];
                planar[pixelCount * 2 + pixel] = interleaved[pixel * 3 + 2];
            }

            return planar;
        }

        private DicomCodecException Wrap(string operation, int frame, Exception exception)
        {
            if (exception is DicomCodecException codecException)
            {
                return codecException;
            }

            return new DicomCodecException($"{TransferSyntax.UID.Name} {operation} frame {frame} failed.", exception);
        }

        private static byte[] ToArray(IByteBuffer buffer)
        {
            var bytes = new byte[buffer.Size];
            Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
