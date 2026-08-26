using FellowOakDicom.IO.Buffer;

namespace FellowOakDicom.PureCodecs.Internal
{
    internal static class CodecOutputBuffer
    {
        private const long MemoryBufferThreshold = 1024 * 1024;

        public static IByteBuffer Create(byte[] data, int frameCount)
        {
            return data.LongLength >= MemoryBufferThreshold || frameCount > 1
                ? (IByteBuffer)new TempFileBuffer(data)
                : new MemoryByteBuffer(data);
        }
    }
}
