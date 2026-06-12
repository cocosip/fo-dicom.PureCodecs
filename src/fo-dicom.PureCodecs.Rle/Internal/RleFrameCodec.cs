using System;
using System.Collections.Generic;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Rle.Internal
{
    internal static class RleFrameCodec
    {
        public static byte[] EncodeFrame(DicomPixelData pixelData, byte[] rawFrame)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (rawFrame == null)
            {
                throw new ArgumentNullException(nameof(rawFrame));
            }

            var layout = RlePixelLayout.From(pixelData);
            if (layout.SegmentCount > RleHeader.MaximumSegmentCount)
            {
                throw CreateException($"RLE Lossless encode requires {layout.SegmentCount} segments, which exceeds the supported maximum of 15.");
            }

            if (rawFrame.Length < layout.FrameLength)
            {
                throw CreateException($"RLE Lossless raw frame length {rawFrame.Length} is shorter than expected length {layout.FrameLength}.");
            }

            var segments = new List<byte[]>(layout.SegmentCount);
            var offsets = new int[layout.SegmentCount];
            var offset = RleHeader.Length;

            for (var segmentIndex = 0; segmentIndex < layout.SegmentCount; segmentIndex++)
            {
                offsets[segmentIndex] = offset;
                var segment = ExtractSegment(rawFrame, layout, segmentIndex);
                var encoded = RleSegmentCodec.Encode(segment);
                segments.Add(encoded);
                offset += encoded.Length + (encoded.Length % 2);
            }

            var frame = new byte[offset];
            var headerBytes = new RleHeader(offsets).ToBytes();
            Buffer.BlockCopy(headerBytes, 0, frame, 0, headerBytes.Length);

            offset = RleHeader.Length;
            foreach (var segment in segments)
            {
                Buffer.BlockCopy(segment, 0, frame, offset, segment.Length);
                offset += segment.Length;
                if (segment.Length % 2 != 0)
                {
                    frame[offset++] = 0;
                }
            }

            return frame;
        }

        public static byte[] DecodeFrame(DicomPixelData pixelData, byte[] rleFrame)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData));
            }

            if (rleFrame == null)
            {
                throw new ArgumentNullException(nameof(rleFrame));
            }

            var layout = RlePixelLayout.From(pixelData);
            var header = RleHeader.Parse(rleFrame);
            if (header.SegmentCount != layout.SegmentCount)
            {
                throw CreateException($"RLE Lossless segment count {header.SegmentCount} does not match expected segment count {layout.SegmentCount}.");
            }

            var rawFrame = new byte[layout.FrameLength];
            for (var segmentIndex = 0; segmentIndex < header.SegmentCount; segmentIndex++)
            {
                var start = header.SegmentOffsets[segmentIndex];
                var end = segmentIndex + 1 < header.SegmentCount
                    ? header.SegmentOffsets[segmentIndex + 1]
                    : rleFrame.Length;
                if (end <= start)
                {
                    throw CreateException("RLE Lossless segment offsets must be strictly increasing.");
                }

                var encodedSegment = new byte[end - start];
                Buffer.BlockCopy(rleFrame, start, encodedSegment, 0, encodedSegment.Length);
                var segment = DecodePossiblyPaddedSegment(encodedSegment, layout.PixelCount);
                CopySegmentToFrame(segment, rawFrame, layout, segmentIndex);
            }

            return rawFrame;
        }

        private static byte[] ExtractSegment(byte[] rawFrame, RlePixelLayout layout, int segmentIndex)
        {
            var segment = new byte[layout.PixelCount];
            var sampleIndex = segmentIndex / layout.BytesAllocated;
            var byteIndex = layout.BytesAllocated - 1 - (segmentIndex % layout.BytesAllocated);

            for (var pixelIndex = 0; pixelIndex < layout.PixelCount; pixelIndex++)
            {
                segment[pixelIndex] = rawFrame[layout.GetRawOffset(pixelIndex, sampleIndex, byteIndex)];
            }

            return segment;
        }

        private static void CopySegmentToFrame(byte[] segment, byte[] rawFrame, RlePixelLayout layout, int segmentIndex)
        {
            var sampleIndex = segmentIndex / layout.BytesAllocated;
            var byteIndex = layout.BytesAllocated - 1 - (segmentIndex % layout.BytesAllocated);

            for (var pixelIndex = 0; pixelIndex < layout.PixelCount; pixelIndex++)
            {
                rawFrame[layout.GetRawOffset(pixelIndex, sampleIndex, byteIndex)] = segment[pixelIndex];
            }
        }

        private static byte[] DecodePossiblyPaddedSegment(byte[] encodedSegment, int expectedLength)
        {
            try
            {
                return RleSegmentCodec.Decode(encodedSegment, expectedLength);
            }
            catch (DicomCodecException) when (encodedSegment.Length > 0 && encodedSegment[encodedSegment.Length - 1] == 0)
            {
                var unpadded = new byte[encodedSegment.Length - 1];
                Buffer.BlockCopy(encodedSegment, 0, unpadded, 0, unpadded.Length);
                return RleSegmentCodec.Decode(unpadded, expectedLength);
            }
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }

        private sealed class RlePixelLayout
        {
            private RlePixelLayout(
                int width,
                int height,
                int samplesPerPixel,
                int bytesAllocated,
                PlanarConfiguration planarConfiguration)
            {
                Width = width;
                Height = height;
                SamplesPerPixel = samplesPerPixel;
                BytesAllocated = bytesAllocated;
                PlanarConfiguration = planarConfiguration;
            }

            public int Width { get; }

            public int Height { get; }

            public int SamplesPerPixel { get; }

            public int BytesAllocated { get; }

            public PlanarConfiguration PlanarConfiguration { get; }

            public int PixelCount => Width * Height;

            public int SegmentCount => SamplesPerPixel * BytesAllocated;

            public int FrameLength => PixelCount * SamplesPerPixel * BytesAllocated;

            public static RlePixelLayout From(DicomPixelData pixelData)
            {
                if (pixelData.BitsAllocated % 8 != 0)
                {
                    throw CreateException($"RLE Lossless does not support BitsAllocated {pixelData.BitsAllocated}.");
                }

                return new RlePixelLayout(
                    pixelData.Width,
                    pixelData.Height,
                    pixelData.SamplesPerPixel,
                    pixelData.BitsAllocated / 8,
                    pixelData.PlanarConfiguration);
            }

            public int GetRawOffset(int pixelIndex, int sampleIndex, int byteIndex)
            {
                if (PlanarConfiguration == PlanarConfiguration.Planar && SamplesPerPixel > 1)
                {
                    return sampleIndex * PixelCount * BytesAllocated + pixelIndex * BytesAllocated + byteIndex;
                }

                return pixelIndex * SamplesPerPixel * BytesAllocated + sampleIndex * BytesAllocated + byteIndex;
            }
        }
    }
}
