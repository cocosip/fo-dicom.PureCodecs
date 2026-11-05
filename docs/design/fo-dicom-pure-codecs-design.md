# fo-dicom.PureCodecs Design

## Purpose

`fo-dicom.PureCodecs` is a pure C# replacement for `fo-dicom.Codecs`.

The first development phase must fully replace the codec capabilities currently provided by `fo-dicom.Codecs`, while removing the native C/C++ dependency that can crash the hosting process. All codec failures must surface as managed exceptions, normally `DicomCodecException` or a more specific managed exception wrapped by codec code.

This document is the baseline for architecture, scope, package shape, and
acceptance criteria. Future development should update this document when the
agreed direction changes.

## Document Set

Development must use this document together with the documents below:

- `docs/development/verification-baseline.md`: current verification results and
  the commands that reproduce them.
- `docs/design/codec-entry-design.md`: package entry point, `PureTranscoderManager`, registration, public codec types, and shared utilities.
- `docs/design/rle-codec-design.md`: RLE Lossless encode/decode design.
- `docs/design/jpeg-codec-design.md`: JPEG Process 1, Process 2/4, Process 14, and Process 14 SV1 design.
- `docs/design/jpegls-codec-design.md`: JPEG-LS Lossless and JPEG-LS Near-Lossless design.
- `docs/design/jpeg2000-codec-design.md`: JPEG 2000 Lossless, JPEG 2000 Lossy,
  HTJ2K design, and the classic/HT compatibility boundary.

## Hard Requirements

- Implement codec logic in C#.
- Do not use native C/C++ codec DLLs.
- Do not use P/Invoke for codec execution.
- Target `netstandard2.0` only for shipped libraries.
- Deliver one NuGet package.
- Package multiple codec-family DLLs inside that NuGet package.
- Integrate with fo-dicom through `IDicomCodec` and `ITranscoderManager`.
- First phase must fully replace the transfer syntaxes supported by `fo-dicom.Codecs`, excluding features that its README marks as in development.

## Target Framework

All production libraries target only:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

This keeps the package usable from .NET Framework 4.7.2+, .NET Core, and modern .NET runtimes.

Test, tool, benchmark, and modern consumer-smoke projects target `net10.0`; the
shipped codec assemblies must remain `netstandard2.0`.

## Package Shape

The final output is one NuGet package:

```text
FoDicom.PureCodecs.nupkg
  lib/netstandard2.0/
    fo-dicom.PureCodecs.dll
    fo-dicom.PureCodecs.Jpeg.dll
    fo-dicom.PureCodecs.JpegLs.dll
    fo-dicom.PureCodecs.Jpeg2000.dll
    fo-dicom.PureCodecs.Rle.dll
```

The split is by codec family:

- `fo-dicom.PureCodecs.dll`: public entry point, registration, shared contracts, shared utilities.
- `fo-dicom.PureCodecs.Jpeg.dll`: classic JPEG family.
- `fo-dicom.PureCodecs.JpegLs.dll`: JPEG-LS family.
- `fo-dicom.PureCodecs.Jpeg2000.dll`: JPEG 2000 and HTJ2K family.
- `fo-dicom.PureCodecs.Rle.dll`: RLE Lossless support required for full replacement of `fo-dicom.Codecs`.

The user installs one NuGet package and registers one transcoder manager. The internal DLL split must not add user-facing setup complexity.

## Package Version Management

NuGet dependency versions are managed centrally in `Directory.Packages.props` with `ManagePackageVersionsCentrally` enabled. Project files use versionless `PackageReference` items for centrally managed packages. The current production `fo-dicom` version is `5.2.6`; the interoperability reference uses `fo-dicom.Codecs` `6.0.0` through normal NuGet restore.

## fo-dicom Integration

fo-dicom codec integration is based on these existing interfaces:

- `FellowOakDicom.Imaging.Codec.IDicomCodec`
- `FellowOakDicom.Imaging.Codec.ITranscoderManager`
- `FellowOakDicom.Imaging.Codec.TranscoderManager`

The intended usage mirrors `fo-dicom.Codecs`:

```csharp
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

`PureTranscoderManager` is responsible for registering every codec supplied by the package.

## Supported Transfer Syntaxes

Phase 1 must support the same completed transfer syntaxes as `fo-dicom.Codecs`:

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

JPEG XL transfer syntaxes are not part of phase 1 because `fo-dicom.Codecs` currently marks them as in development.

## Public Assembly Responsibilities

### `fo-dicom.PureCodecs`

Responsibilities:

- `PureTranscoderManager`.
- Explicit codec-family registration through fo-dicom `IDicomCodec` implementations.
- Common codec parameter base helpers where needed.
- Shared buffer and frame utilities.
- Shared color conversion helpers when they are not family-specific.
- Common managed exception wrapping.

This assembly should depend on fo-dicom and the codec-family assemblies.

### `fo-dicom.PureCodecs.Rle`

Responsibilities:

- RLE Lossless encode.
- RLE Lossless decode.
- Segment parsing and validation.
- Multi-frame handling.
- 8-bit and 16-bit samples.
- Interleaved and planar color layout handling.

RLE is intentionally separate even though it is not one of the three JPEG families. Full replacement of `fo-dicom.Codecs` requires it.

### `fo-dicom.PureCodecs.Jpeg`

Responsibilities:

- JPEG Process 1 encode/decode.
- JPEG Process 2/4 encode/decode.
- JPEG Process 14 encode/decode.
- JPEG Process 14 SV1 encode/decode.
- JPEG marker parsing.
- Huffman decoding and encoding.
- Baseline and extended DCT handling.
- Lossless JPEG predictor handling.
- 8-bit, 12-bit, and 16-bit cases required by DICOM transfer syntaxes.
- DICOM photometric interpretation and planar conversion rules.

### `fo-dicom.PureCodecs.JpegLs`

Responsibilities:

- JPEG-LS Lossless encode/decode.
- JPEG-LS Near-Lossless encode/decode.
- JPEG-LS marker parsing.
- Preset coding parameters.
- Interleave modes.
- Near-lossless allowed error handling.
- Color transform behavior needed by DICOM datasets.

### `fo-dicom.PureCodecs.Jpeg2000`

Responsibilities:

- JPEG 2000 Lossless encode/decode.
- JPEG 2000 Lossy encode/decode.
- HTJ2K Lossless encode/decode.
- HTJ2K Lossless RPCL encode/decode.
- HTJ2K Lossy encode/decode.
- Codestream parsing.
- Tile, component, progression, packet, and precinct handling.
- Wavelet transform and inverse transform.
- Entropy coding and decoding required for JPEG 2000.
- HT block coding required for HTJ2K.
- DICOM photometric interpretation and component layout handling.

This is expected to be the highest-complexity module.

## Registration Design

fo-dicom already defines the codec extension boundary through `IDicomCodec` and `ITranscoderManager`. `fo-dicom.PureCodecs` should not introduce a second public codec registration abstraction.

Each codec-family assembly contains concrete `IDicomCodec` implementations for its supported transfer syntaxes. The public `PureTranscoderManager` explicitly registers those implementations in `LoadCodecs`.

Example shape:

```csharp
public sealed class PureTranscoderManager : TranscoderManager
{
    public PureTranscoderManager()
    {
        LoadCodecs(null, null);
    }

    public override void LoadCodecs(string path, string search)
    {
        AddCodec(new DicomRleLosslessCodec());
        AddCodec(new DicomJpegProcess1Codec());
        AddCodec(new DicomJpegProcess2_4Codec());
        AddCodec(new DicomJpegLossless14Codec());
        AddCodec(new DicomJpegLossless14SV1Codec());
        AddCodec(new DicomJpegLsLosslessCodec());
        AddCodec(new DicomJpegLsNearLosslessCodec());
        AddCodec(new DicomJpeg2000LosslessCodec());
        AddCodec(new DicomJpeg2000LossyCodec());
        AddCodec(new DicomHtJpeg2000LosslessCodec());
        AddCodec(new DicomHtJpeg2000LosslessRpclCodec());
        AddCodec(new DicomHtJpeg2000LossyCodec());
    }

    private void AddCodec(IDicomCodec codec)
    {
        Codecs[codec.TransferSyntax] = codec;
    }
}
```

This mirrors fo-dicom's current `DefaultTranscoderManager` style: deterministic explicit registration, no broad reflection, and no extra user-facing registration contract. The implementation may organize codec construction internally to keep the file readable, but the public extension model remains fo-dicom's own interfaces.

## Error Handling Rules

- Invalid compressed data must not crash the process.
- Codec implementation must validate bounds before reading or writing buffers.
- Decode/encode failures should throw managed exceptions.
- Public codec entry points should wrap unexpected implementation exceptions as `DicomCodecException`.
- Exceptions should include transfer syntax and frame index where possible.
- Partial output must not be added to `newPixelData` after a frame-level failure.

## Buffer and Memory Rules

- Avoid native memory.
- Prefer managed arrays, `ArrayPool<byte>`, and fo-dicom `IByteBuffer` abstractions.
- Return pooled arrays promptly.
- Do not expose pooled arrays after returning them to the pool.
- Multi-frame data should be processed frame by frame unless a codec algorithm truly requires cross-frame state.
- Large compressed or raw frames may use fo-dicom buffer abstractions, but codec logic itself must stay managed.

## Implementation Record

Phase 1 was implemented in this order:

| Step | Scope | Status |
| --- | --- | --- |
| 0 | Solution skeleton, five `netstandard2.0` production projects, test project, package configuration | Complete |
| 1 | `PureTranscoderManager` registration and `CanTranscode` coverage for every Phase 1 syntax | Complete |
| 2 | RLE Lossless encode/decode | Complete |
| 3 | JPEG Process 1, Process 2/4, Process 14, Process 14 SV1 | Complete |
| 4 | JPEG-LS Lossless and Near-Lossless | Complete |
| 5 | Classic JPEG 2000 Lossless and Lossy | Complete |
| 6 | HTJ2K Lossless, Lossless RPCL, and Lossy | Complete |
| 7 | NuGet package, consumer smoke tests, compatibility matrix | Complete |

The order was intentional: RLE proved the package layout, manager registration,
frame handling, and test harness before the harder codecs were implemented.

## Test Strategy

The test suite is built from four layers:

1. Unit tests for low-level codec primitives.
2. Codec tests for one frame and multi-frame datasets.
3. Compatibility tests based on `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests`.
4. Consumer smoke tests using the built NuGet package.

The `fo-dicom.Codecs` tests are useful but not sufficient because some of them only check that conversion does not throw and that pixel data exists. `fo-dicom.PureCodecs` adds stronger checks:

- Exact byte equality for lossless round-trips.
- Tolerance checks for lossy round-trips.
- Frame count preservation.
- Required DICOM tags after compression.
- Photometric interpretation changes when conversion is expected.
- Managed exception behavior for invalid input.

## Compatibility Baseline

Source references used for this design:

- `<FO_DICOM_SOURCE_ROOT>`: local checkout of `fo-dicom`.
- `<FO_DICOM_CODECS_SOURCE_ROOT>`: local checkout of `fo-dicom.Codecs`.

These roots are intentionally machine-specific. Do not hard-code a developer's absolute checkout path in this repository.

The key fo-dicom integration files are:

- `FO-DICOM.Core\Imaging\Codec\IDicomCodec.cs`
- `FO-DICOM.Core\Imaging\Codec\TranscoderManager.cs`
- `FO-DICOM.Core\Imaging\Codec\DicomTranscoder.cs`
- `FO-DICOM.Core\Imaging\DicomPixelData.cs`

The key Efferent implementation files are:

- `Codec\NativeTranscoderManager.cs`
- `Codec\DicomRleCodec.cs`
- `Codec\DicomJpegCodec.cs`
- `Codec\DicomJpegLsCodec.cs`
- `Codec\DicomJpeg2000Codec.cs`
- `Codec\DicomHtJpeg2000Codec.cs`
- `Tests\Unit\TranscodeUnitTest.cs`
- `Tests\Acceptance\AcceptanceTests.cs`

## Non-Goals for Phase 1

- JPEG XL support.
- Native fallback.
- P/Invoke fallback.
- Separate NuGet packages per codec family.
- Runtime-specific production assemblies.
- Replacing fo-dicom core APIs.

## Current Phase 1 Boundaries

- Progressive, arithmetic-coded, CMYK, and YCCK JPEG are unsupported. JPEG
  Process 1 accepts 8-bit samples; Process 2/4 also accepts 12-bit monochrome
  and 12-bit RGB SF444 in 16-bit containers, but not 12-bit SF422 encoding.
- JPEG-LS planar normalization is limited to three components with
  `BitsStored <= 8`; planar `YBR_FULL_422` is rejected.
- JPEG 2000 accepts raw codestreams only. Part 2 multi-component, JPIP, JPT,
  and component subsampling are out of scope. See the
  [JPEG 2000 and HTJ2K design](jpeg2000-codec-design.md) for HTJ2K marker
  exclusions.

## Packaging Decisions

- The NuGet package id is `FoDicom.PureCodecs`; the assemblies keep the
  `fo-dicom.PureCodecs*` names.
- The package contains the entry assembly and codec-family assemblies under
  `lib/netstandard2.0`.
- The package version is maintained manually in
  `src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj` and must match the release
  tag.
- The package targets fo-dicom 5.x through the centrally managed `fo-dicom`
  package reference. fo-dicom 4.x compatibility is not claimed.
- The assemblies are not strong-named.

Future release decisions still open:

- Performance goals for large multi-frame studies.
- Whether later releases should add strong-named assemblies.
- Whether later releases should add fo-dicom 4.x compatibility.
