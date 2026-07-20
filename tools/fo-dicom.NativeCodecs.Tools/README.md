# fo-dicom.NativeCodecs.Tools

This repository-local command-line tool uses `fo-dicom.Codecs` and
`NativeTranscoderManager` to produce native-codec DICOM compression baselines.
It is not part of the `fo-dicom.PureCodecs` NuGet package.

## Command

Run from this tool directory:

```powershell
dotnet run <input-file> [--output-dir <directory>] [--format <format>]
```

Arguments match `fo-dicom.PureCodecs.Tools`:

| Parameter | Required | Description |
| --- | --- | --- |
| `<input-file>` | Yes | One DICOM file to compress. |
| `--output-dir <directory>` | No | Directory for compressed output. |
| `-o <directory>` | No | Short form of `--output-dir`. |
| `--format <suffix>` | No | Compress only the named target suffix. |
| `--help`, `-h`, `/?` | No | Print command usage. |

Without `--output-dir`, output is written next to the input in
`<input-file-name>_compressed`. Each output file is named
`<input-file-name>_<format-suffix>.dcm`.

## Target Formats

The tool uses the same twelve Phase 1 targets as `fo-dicom.PureCodecs.Tools`:

| Format | Suffix |
| --- | --- |
| RLE Lossless | `rle` |
| JPEG Baseline Process 1 | `jpeg_baseline` |
| JPEG Extended Process 2/4 | `jpeg_process2_4` |
| JPEG Lossless Process 14 | `jpeg_lossless_14` |
| JPEG Lossless Process 14 SV1 | `jpeg_lossless_sv1` |
| JPEG-LS Lossless | `jpegls_lossless` |
| JPEG-LS Near-Lossless | `jpegls_near_lossless` |
| JPEG 2000 Lossless | `j2k_lossless` |
| JPEG 2000 Lossy | `j2k_lossy` |
| HTJ2K Lossless | `htj2k_lossless` |
| HTJ2K Lossless RPCL | `htj2k_lossless_rpcl` |
| HTJ2K Lossy | `htj2k_lossy` |

Each target is attempted independently. A native codec exception is reported
for that target and does not stop later conversions. JPEG 2000 Lossy uses the
native codec parameter `Rate = 16`; other targets use their native defaults.
