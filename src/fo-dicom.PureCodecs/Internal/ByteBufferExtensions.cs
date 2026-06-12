using System;
using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Internal
{
    internal static class ByteBufferExtensions
    {
        public static byte[] ToArrayCopy(this IByteBuffer buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            var data = buffer.Data;
            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            return copy;
        }
    }
}
