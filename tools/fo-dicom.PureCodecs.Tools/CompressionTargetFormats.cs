using FellowOakDicom;

namespace FellowOakDicom.PureCodecs.Tools;

public static class CompressionTargetFormats
{
    public static IReadOnlyList<CompressionTargetFormat> All { get; } =
        new[]
        {
            new CompressionTargetFormat("RLE Lossless", DicomTransferSyntax.RLELossless, "rle", isLossless: true),
            new CompressionTargetFormat("JPEG Baseline Process 1", DicomTransferSyntax.JPEGProcess1, "jpeg_baseline", isLossless: false),
            new CompressionTargetFormat("JPEG Extended Process 2/4", DicomTransferSyntax.JPEGProcess2_4, "jpeg_process2_4", isLossless: false),
            new CompressionTargetFormat("JPEG Lossless Process 14", DicomTransferSyntax.JPEGProcess14, "jpeg_lossless_14", isLossless: true),
            new CompressionTargetFormat("JPEG Lossless Process 14 SV1", DicomTransferSyntax.JPEGProcess14SV1, "jpeg_lossless_sv1", isLossless: true),
            new CompressionTargetFormat("JPEG-LS Lossless", DicomTransferSyntax.JPEGLSLossless, "jpegls_lossless", isLossless: true),
            new CompressionTargetFormat("JPEG-LS Near-Lossless", DicomTransferSyntax.JPEGLSNearLossless, "jpegls_near_lossless", isLossless: false),
            new CompressionTargetFormat("JPEG 2000 Lossless", DicomTransferSyntax.JPEG2000Lossless, "j2k_lossless", isLossless: true),
            new CompressionTargetFormat("JPEG 2000 Lossy", DicomTransferSyntax.JPEG2000Lossy, "j2k_lossy", isLossless: false),
            new CompressionTargetFormat("HTJ2K Lossless", DicomTransferSyntax.HTJ2KLossless, "htj2k_lossless", isLossless: true),
            new CompressionTargetFormat("HTJ2K Lossless RPCL", DicomTransferSyntax.HTJ2KLosslessRPCL, "htj2k_lossless_rpcl", isLossless: true),
            new CompressionTargetFormat("HTJ2K Lossy", DicomTransferSyntax.HTJ2K, "htj2k_lossy", isLossless: false),
        };
}
