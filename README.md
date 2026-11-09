# fo-dicom.PureCodecs

`fo-dicom.PureCodecs` is a pure C# replacement for `fo-dicom.Codecs`.

The NuGet package ID is `FoDicom.PureCodecs`. It ships as one package that contains separate managed codec-family assemblies for RLE, JPEG, JPEG-LS, and JPEG 2000 / HTJ2K. Production assemblies target `netstandard2.0` only and do not use native codec DLLs, P/Invoke, or native fallback paths.

NuGet dependency versions are managed centrally in `Directory.Packages.props`. Project files keep `PackageReference` items versionless; add or update dependency versions in `Directory.Packages.props`.

The package version is maintained manually in `src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj`.

## Install

Use the published package when it is available:

```bash
dotnet add package FoDicom.PureCodecs
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

The phase 1 package targets the completed codec support in `fo-dicom.Codecs`, excluding JPEG XL because upstream marks it as in development.

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
FoDicom.PureCodecs.nupkg
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
- Consumer app execution on modern .NET and, on Windows, .NET Framework 4.7.2, plus independent inspection of the packed package layout.

Current Phase 1 exclusions are maintained in the
[overall design](docs/design/fo-dicom-pure-codecs-design.md) and the
[JPEG 2000 and HTJ2K design](docs/design/jpeg2000-codec-design.md). Current
verification results and test commands are recorded in the
[verification baseline](docs/development/verification-baseline.md).

## Native Interoperability Validation

The Native/Pure codec interoperability matrix is a standalone process-isolated runner, not an xUnit test. It covers twelve RLE, JPEG, JPEG-LS, classic JPEG 2000, and HTJ2K transfer syntaxes, including HTJ2K `.201`, `.202`, and `.203`. Workers use the public C# API of the normally restored `fo-dicom.Codecs` 6.0.0 NuGet package as the reference side. Each format runs in its own process, defaults to four concurrent formats, and terminates a worker process tree after five minutes. A failing row is reported as a matrix failure; it is not skipped or hidden. A worker process that dies from a signal is restarted once and logged as `INTEROP|worker-abort`, because the reference native code aborts intermittently; dataset rows are never retried, so a real codec mismatch always fails the matrix.

```powershell
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation -- --parallel 4 --worker-timeout-seconds 300
```

Run one format directly while diagnosing a codec:

```powershell
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation -- --worker jpeg-ls-lossless
```

## Performance Benchmarks

The managed-codec performance suite measures codec-boundary and
`DicomTranscoder` encode/decode costs for the bundled RLE, JPEG, JPEG-LS, and
JPEG 2000 acceptance samples. Fixture validation remains part of the normal
test suite; the benchmark process excludes setup, file I/O, and validation
from timed operations.

```powershell
dotnet run -c Release --project benchmarks/fo-dicom.PureCodecs.Benchmarks -- --verify
dotnet run -c Release --project benchmarks/fo-dicom.PureCodecs.Benchmarks -- --job short
```

BenchmarkDotNet writes machine-specific Markdown and JSON reports to
`BenchmarkDotNet.Artifacts/`, which is intentionally ignored. Compare benchmark
results only when the source pixels or compressed DICOM frame, transfer syntax,
codec settings, and operation boundary match; record the runtime, CPU, and
operating system alongside the result.

## Package Consumer Smoke Validation

The repository includes smoke scripts that run sample consumer apps through direct project references, pack `FoDicom.PureCodecs`, and independently verify that the package contains only the expected managed `netstandard2.0` assemblies.

Use the platform-native script:

```bash
bash ./eng/verify-package-consumer-smoke.sh
```

The shell script is intended for Linux and macOS jobs. It validates the package layout and the modern .NET consumer smoke test.

```powershell
.\eng\Verify-PackageConsumerSmoke.ps1 -RequireNet472
```

The PowerShell script is intended for Windows jobs. It validates the same package checks and modern consumer smoke test, and also runs the .NET Framework 4.7.2 consumer smoke test.

CI runs both scripts: the shell script on every `master` push and release tag, the PowerShell script with `-RequireNet472` on release tags before publishing.

## Release and Publishing

The workflow is [.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml).

| Event | What runs |
| --- | --- |
| `pull_request` to `master` | restore, build, test, fo-dicom interoperability validation |
| `push` to `master` | the above, plus pack and package/consumer smoke verification (no publish) |
| `push` of a `v<Version>` tag | the above, plus the .NET Framework 4.7.2 consumer smoke, then NuGet.org publish and GitHub Release |
| `workflow_dispatch` | same as a pull request |

Packaging on a `master` push proves that a releasable package is good before a tag exists; only a version tag publishes.

The package version is not generated from the tag and is never overridden during packing. Before publishing a release, update `<Version>` in `src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj`, commit that change, and create a matching tag:

```bash
git tag v0.5.2
git push origin v0.5.2
```

The workflow reads the project `<Version>` before the first build and fails when the pushed tag does not match it: `<Version>0.5.2</Version>` must be released with tag `v0.5.2`. Pre-release versions such as `v0.5.2-rc.1` are published as NuGet pre-releases and GitHub pre-releases.

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): the publish job requests `id-token: write` and `NuGet/login@v1` exchanges the GitHub OIDC token for a one-hour nuget.org API key. No long-lived `NUGET_API_KEY` secret is required. Configure the policy once on [nuget.org](https://www.nuget.org/) under **Trusted Publishing**:

- **Repository owner**: `cocosip`
- **Repository**: `fo-dicom.PureCodecs`
- **Workflow file**: `ci-cd.yml`
- **Environment**: empty

The login uses the nuget.org profile `virtual`, which owns `FoDicom.PureCodecs`. The former `NUGET_API_KEY` repository secret and the `release` GitHub environment are no longer used and can be removed.
