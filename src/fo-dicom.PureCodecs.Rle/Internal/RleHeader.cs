using System;
using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.Rle.Internal
{
    internal sealed class RleHeader
    {
        public const int Length = 64;
        public const int MaximumSegmentCount = 15;

        public RleHeader(int[] segmentOffsets)
        {
            if (segmentOffsets == null)
            {
                throw new ArgumentNullException(nameof(segmentOffsets));
            }

            if (segmentOffsets.Length < 1 || segmentOffsets.Length > MaximumSegmentCount)
            {
                throw CreateException($"RLE Lossless segment count {segmentOffsets.Length} is outside the valid range 1..15.");
            }

            SegmentOffsets = new int[segmentOffsets.Length];
            Array.Copy(segmentOffsets, SegmentOffsets, segmentOffsets.Length);
        }

        public int SegmentCount => SegmentOffsets.Length;

        public int[] SegmentOffsets { get; }

        public static RleHeader Parse(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < Length)
            {
                throw CreateException($"RLE Lossless frame is shorter than the required 64 byte header.");
            }

            var segmentCount = ReadInt32LittleEndian(frame, 0);
            if (segmentCount < 1 || segmentCount > MaximumSegmentCount)
            {
                throw CreateException($"RLE Lossless segment count {segmentCount} is outside the valid range 1..15.");
            }

            var offsets = new int[segmentCount];
            var previous = 0;
            for (var index = 0; index < segmentCount; index++)
            {
                var offset = ReadInt32LittleEndian(frame, 4 + index * 4);
                if (offset < Length || offset >= frame.Length)
                {
                    throw CreateException($"RLE Lossless segment offset {offset} is outside the frame.");
                }

                if (index > 0 && offset <= previous)
                {
                    throw CreateException("RLE Lossless segment offsets must be strictly increasing.");
                }

                offsets[index] = offset;
                previous = offset;
            }

            return new RleHeader(offsets);
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[Length];
            WriteInt32LittleEndian(bytes, 0, SegmentCount);

            for (var index = 0; index < SegmentOffsets.Length; index++)
            {
                WriteInt32LittleEndian(bytes, 4 + index * 4, SegmentOffsets[index]);
            }

            return bytes;
        }

        private static int ReadInt32LittleEndian(byte[] bytes, int offset)
        {
            return bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24);
        }

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static DicomCodecException CreateException(string message)
        {
            return new DicomCodecException(message);
        }
    }
}
