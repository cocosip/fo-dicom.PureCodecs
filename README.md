# fo-dicom.PureCodecs

`fo-dicom.PureCodecs` is a pure C# replacement package for `fo-dicom.Codecs`.

The package ships as one NuGet package that contains separate managed codec-family assemblies for RLE, JPEG, JPEG-LS, and JPEG 2000 / HTJ2K. Production assemblies target `netstandard2.0` only and do not use native codec DLLs, P/Invoke, or native fallback paths.

NuGet package versions are managed centrally in `Directory.Packages.props`. Project files keep `PackageReference` items versionless; add or update package versions in `Directory.Packages.props`.

## Install

Use the published prerelease package when it is available:

```bash
dotnet add package fo-dicom.PureCodecs --prerelease
```

## Usage

Register the package once through fo-dicom's transcoder manager service. The codec-family assemblies are loaded through the main package reference; applications do not register each family DLL separately.

```csharp
using FellowOakDicom;
using FellowOakDicom.PureCodecs;

new DicomSetupBuilder()
    .RegisterServices(services => services.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

After registration, use fo-dicom's normal `DicomTranscoder` flow:

```csharp
var transcoder = new DicomTranscoder(
    sourceSyntax: DicomTransferSyntax.ExplicitVRLittleEndian,
    targetSyntax: DicomTransferSyntax.RLELossless);

DicomDataset compressed = transcoder.Transcode(dataset);
```

`DicomTranscoder` is provided by the `FellowOakDicom.Imaging.Codec` namespace.

## Supported Transfer Syntaxes

The first alpha package targets the completed codec support in `fo-dicom.Codecs`, excluding JPEG XL because upstream marks it as in development.

| Codec family | Transfer syntax | UID |
| --- | --- | --- |
| RLE | RLE Lossless | `1.2.840.10008.1.2.5` |
| JPEG | JPEG Baseline Process 1 | `1.2.840.10008.1.2.4.50` |
| JPEG | JPEG Extended Process 2/4 | `1.2.840.10008.1.2.4.51` |
| JPEG | JPEG Lossless Process 14 | `1.2.840.10008.1.2.4.57` |
| JPEG | JPEG Lossless Process 14 SV1 | `1.2.840.10008.1.2.4.70` |
| JPEG-LS | JPEG-LS Lossless | `1.2.840.10008.1.2.4.80` |
| JPEG-LS | JPEG-LS Near-Lossless | `1.2.840.10008.1.2.4.81` |
| JPEG 2000 | JPEG 2000 Lossless | `1.2.840.10008.1.2.4.90` |
| JPEG 2000 | JPEG 2000 Lossy | `1.2.840.10008.1.2.4.91` |
| JPEG 2000 / HTJ2K | HTJ2K Lossless | `1.2.840.10008.1.2.4.201` |
| JPEG 2000 / HTJ2K | HTJ2K Lossless RPCL | `1.2.840.10008.1.2.4.202` |
| JPEG 2000 / HTJ2K | HTJ2K Lossy | `1.2.840.10008.1.2.4.203` |

## Package Layout

The NuGet package contains one public package identity with five managed assemblies under `lib/netstandard2.0`:

```text
fo-dicom.PureCodecs.nupkg
  lib/netstandard2.0/
    fo-dicom.PureCodecs.dll
    fo-dicom.PureCodecs.Jpeg.dll
    fo-dicom.PureCodecs.JpegLs.dll
    fo-dicom.PureCodecs.Jpeg2000.dll
    fo-dicom.PureCodecs.Rle.dll
```

Responsibilities are split by codec family:

- `fo-dicom.PureCodecs.dll`: `PureTranscoderManager`, registration, shared helpers, and public entry point.
- `fo-dicom.PureCodecs.Rle.dll`: RLE Lossless.
- `fo-dicom.PureCodecs.Jpeg.dll`: JPEG Process 1, Process 2/4, Process 14, and Process 14 SV1.
- `fo-dicom.PureCodecs.JpegLs.dll`: JPEG-LS Lossless and JPEG-LS Near-Lossless.
- `fo-dicom.PureCodecs.Jpeg2000.dll`: JPEG 2000 Lossless, JPEG 2000 Lossy, and HTJ2K transfer syntaxes.

## Error Behavior

Codec failures are expected to stay inside managed code. Invalid compressed streams, unsupported pixel metadata, unsupported marker combinations, and encode/decode failures should surface as `DicomCodecException` or another managed exception wrapped by the codec boundary.

The package is intentionally not process-isolated and does not load native codec libraries. A malformed frame should fail the transcode operation rather than crash the hosting process through native codec execution.

## Compatibility Notes

The compatibility target is the public behavior of `fo-dicom.Codecs` for completed transfer syntaxes, not its native implementation details. Tests cover:

- `CanTranscode` registration for all phase 1 transfer syntaxes.
- Raw 8-bit, raw 16-bit, RGB, and multi-frame round-trips where each codec supports that data shape.
- Exact decoded frame equality for lossless transfer syntaxes.
- Tolerance-based decoded frame checks for lossy transfer syntaxes.
- Efferent `fo-dicom.Codecs` unit and acceptance fixtures included in the repository.
- Managed exceptions for invalid compressed streams.
- Consumer package installation on modern .NET and .NET Framework 4.7.2 on Windows.

Known phase 1 limitations are tracked in [`docs/development/phase-1-known-limitations.md`](docs/development/phase-1-known-limitations.md).

## Package Consumer Smoke Validation

The repository includes smoke scripts that pack `fo-dicom.PureCodecs`, install the generated NuGet package into sample consumer apps, and verify that the package contains only the expected managed `netstandard2.0` assemblies.

Use the platform-native script in CI:

```bash
bash ./eng/verify-package-consumer-smoke.sh
```

The shell script is intended for Linux and macOS jobs. It validates the package layout and the modern .NET consumer smoke test.

```powershell
.\eng\Verify-PackageConsumerSmoke.ps1 -RequireNet472
```

The PowerShell script is intended for Windows jobs. It validates the same package checks and modern consumer smoke test, and also runs the .NET Framework 4.7.2 consumer smoke test.
