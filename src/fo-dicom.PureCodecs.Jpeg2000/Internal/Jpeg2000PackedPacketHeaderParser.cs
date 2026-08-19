using System;
using System.Collections.Generic;

namespace FellowOakDicom.PureCodecs.Jpeg2000.Internal
{
    internal static class Jpeg2000PackedPacketHeaderParser
    {
        public static IReadOnlyList<byte[]> ParsePpm(IReadOnlyList<Jpeg2000MarkerSegment> segments)
        {
            var ordered = OrderSegments(segments, Jpeg2000Marker.PPM, "PPM");
            var chunks = new List<byte[]>();
            byte[]? current = null;
            var currentOffset = 0;

            foreach (var payload in ordered)
            {
                var offset = 1;
                if (current != null)
                {
                    offset += Copy(payload, offset, current, ref currentOffset);
                    if (currentOffset == current.Length)
                    {
                        chunks.Add(current);
                        current = null;
                        currentOffset = 0;
                    }
                }

                while (offset < payload.Length)
                {
                    if (payload.Length - offset < 4)
                    {
                        throw Jpeg2000Binary.CreateException("JPEG 2000 PPM marker does not contain a complete Nppm length.");
                    }

                    var length = Jpeg2000Binary.ReadUInt32(payload, offset);
                    offset += 4;
                    if (length > int.MaxValue)
                    {
                        throw Jpeg2000Binary.CreateException("JPEG 2000 PPM packet header length is too large.");
                    }

                    current = new byte[(int)length];
                    currentOffset = 0;
                    offset += Copy(payload, offset, current, ref currentOffset);
                    if (currentOffset == current.Length)
                    {
                        chunks.Add(current);
                        current = null;
                        currentOffset = 0;
                    }
                }
            }

            if (current != null)
            {
                throw Jpeg2000Binary.CreateException("JPEG 2000 PPM marker data ends before the declared Nppm length.");
            }

            return chunks;
        }

        public static byte[] ParsePpt(IReadOnlyList<Jpeg2000MarkerSegment> segments)
        {
            var ordered = OrderSegments(segments, Jpeg2000Marker.PPT, "PPT");
            var length = 0;
            foreach (var payload in ordered)
            {
                length = checked(length + payload.Length - 1);
            }

            var result = new byte[length];
            var offset = 0;
            foreach (var payload in ordered)
            {
                var count = payload.Length - 1;
                Buffer.BlockCopy(payload, 1, result, offset, count);
                offset += count;
            }

            return result;
        }

        private static IReadOnlyList<byte[]> OrderSegments(
            IReadOnlyList<Jpeg2000MarkerSegment> segments,
            byte expectedMarker,
            string markerName)
        {
            var indexed = new SortedDictionary<byte, byte[]>();
            foreach (var segment in segments)
            {
                if (segment.Code != expectedMarker)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 " + markerName + " marker segment expected.");
                }

                if (segment.Payload.Length < 2)
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 " + markerName + " marker payload is too short.");
                }

                var index = segment.Payload[0];
                if (indexed.ContainsKey(index))
                {
                    throw Jpeg2000Binary.CreateException("JPEG 2000 " + markerName + " marker index " + index + " is duplicated.");
                }

                indexed.Add(index, segment.Payload);
            }

            return new List<byte[]>(indexed.Values);
        }

        private static int Copy(byte[] source, int sourceOffset, byte[] destination, ref int destinationOffset)
        {
            var count = Math.Min(source.Length - sourceOffset, destination.Length - destinationOffset);
            if (count > 0)
            {
                Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count);
                destinationOffset += count;
            }

            return count;
        }
    }
}
