# fo-dicom.PureCodecs.Tools

This tool project is for repository-local DICOM codec utilities. It is not part
of the `fo-dicom.PureCodecs` NuGet package.

## DICOM Automatic Compression Tool

The tool reads one DICOM file with full pixel data and writes compressed DICOM
copies for all registered phase 1 compression transfer syntaxes.

## Command

Run from this tool directory:

```powershell
cd D:\Code\dotnet\fo-dicom.PureCodecs\tools\fo-dicom.PureCodecs.Tools
dotnet run <input-file> [--output-dir <directory>]
```

You can also run the built executable after `dotnet build`:

```powershell
.\bin\Debug\net10.0\fo-dicom.PureCodecs.Tools.exe <input-file> [--output-dir <directory>]
```

## Parameters

| Parameter | Required | Description |
| --- | --- | --- |
| `<input-file>` | Yes | Path to the DICOM file to compress. Only one input file is accepted per run. |
| `--output-dir <directory>` | No | Directory where compressed files are written. |
| `-o <directory>` | No | Short form of `--output-dir`. |
| `--help`, `-h`, `/?` | No | Prints command usage. |

## Default Output Directory

When `--output-dir` is omitted, output files are written next to the input file
in a directory named:

```text
<input-file-name-without-extension>_compressed
```

Example:

```text
D:\dicom\study1.dcm
D:\dicom\study1_compressed\
```

## Examples

Compress one file and use the default output directory:

```powershell
dotnet run "D:\dicom\study1.dcm"
```

Compress one file into an explicit output directory:

```powershell
dotnet run "D:\dicom\study1.dcm" --output-dir "D:\dicom\study1-compressed"
```

Use the short output-directory option:

```powershell
dotnet run "D:\dicom\study1.dcm" -o "D:\dicom\study1-compressed"
```

Print help:

```powershell
.\bin\Debug\net10.0\fo-dicom.PureCodecs.Tools.exe --help
```

## Output File Names

The tool writes one output file per target format using this pattern:

```text
<input-file-name-without-extension>_<format-suffix>.dcm
```

For input file `study1.dcm`, output names include:

| Format | Output suffix |
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

## Target Transfer Syntaxes

The tool targets the transfer syntaxes registered by `PureTranscoderManager` for
phase 1 automatic compression:

| Format | Transfer Syntax UID |
| --- | --- |
| RLE Lossless | `1.2.840.10008.1.2.5` |
| JPEG Baseline Process 1 | `1.2.840.10008.1.2.4.50` |
| JPEG Extended Process 2/4 | `1.2.840.10008.1.2.4.51` |
| JPEG Lossless Process 14 | `1.2.840.10008.1.2.4.57` |
| JPEG Lossless Process 14 SV1 | `1.2.840.10008.1.2.4.70` |
| JPEG-LS Lossless | `1.2.840.10008.1.2.4.80` |
| JPEG-LS Near-Lossless | `1.2.840.10008.1.2.4.81` |
| JPEG 2000 Lossless | `1.2.840.10008.1.2.4.90` |
| JPEG 2000 Lossy | `1.2.840.10008.1.2.4.91` |
| HTJ2K Lossless | `1.2.840.10008.1.2.4.201` |
| HTJ2K Lossless RPCL | `1.2.840.10008.1.2.4.202` |
| HTJ2K Lossy | `1.2.840.10008.1.2.4.203` |

The tool does not target JPEG XL, JPEG 2000 Part 2 multi-component, JPIP, or JPT
transfer syntaxes because those syntaxes are outside phase 1 or are not
registered by `PureTranscoderManager`.

## Unsupported Rules

Some transfer syntaxes are registered but not valid for every input shape. The
tool reports these as `Unsupported` instead of failed conversions.

Current unsupported rules:

| Rule | Reason |
| --- | --- |
| Unsupported JPEG Baseline Process 1 when `BitsStored > 8` | JPEG Baseline Process 1 supports only 8-bit input. |
| Unsupported JPEG Extended Process 2/4 when `BitsStored > 8` | The current managed JPEG path does not implement 12-bit sequential DCT. |
| Unsupported JPEG sequential DCT when `BitsAllocated != 8` or `BitsStored != 8` | The current managed sequential DCT path supports only 8-bit samples. |
| Unsupported JPEG sequential DCT when `SamplesPerPixel` is not `1` or `3` | The current managed sequential DCT path supports grayscale and 3-component color only. |
| Unsupported JPEG sequential DCT for unsupported photometric interpretations | Supported values are `MONOCHROME1`, `MONOCHROME2`, `RGB`, `YBR_FULL`, and `YBR_FULL_422`. |

Other unsupported combinations may still fail with a managed exception and will
be reported as `Failed` for that format. A failed format does not stop the rest
of the compression run.

## Exit Codes

| Exit code | Meaning |
| --- | --- |
| `0` | The command ran and at least one target succeeded or was unsupported. |
| `1` | The input was invalid, command-line arguments were invalid, or every target conversion failed. |
