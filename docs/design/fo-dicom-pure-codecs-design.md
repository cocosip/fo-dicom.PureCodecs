# fo-dicom.PureCodecs Design and Development Plan

## Purpose

`fo-dicom.PureCodecs` is a pure C# replacement for `fo-dicom.Codecs`.

The first development phase must fully replace the codec capabilities currently provided by `fo-dicom.Codecs`, while removing the native C/C++ dependency that can crash the hosting process. All codec failures must surface as managed exceptions, normally `DicomCodecException` or a more specific managed exception wrapped by codec code.

This document is the baseline for architecture, scope, implementation order, and acceptance criteria. Future development should update this document when the agreed direction changes.

## Design Document Set

Development must use this document together with the focused design documents below:

- `docs/development/development-checklist.md`: overall development workflow and checkbox progress tracker.
- `docs/design/codec-entry-design.md`: package entry point, `PureTranscoderManager`, registration, shared utilities, and stub codec rules.
- `docs/design/rle-codec-design.md`: RLE Lossless encode/decode design.
- `docs/design/jpeg-codec-design.md`: JPEG Process 1, Process 2/4, Process 14, and Process 14 SV1 design.
- `docs/design/jpegls-codec-design.md`: JPEG-LS Lossless and JPEG-LS Near-Lossless design.
- `docs/design/jpeg2000-codec-design.md`: JPEG 2000 Lossless, JPEG 2000 Lossy, and HTJ2K design.

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

Test projects may target modern runtimes such as `net8.0`, but the shipped codec assemblies must remain `netstandard2.0`.

## Package Shape

The final output is one NuGet package:

```text
fo-dicom.PureCodecs.nupkg
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

## Development Order

Development should proceed in this order:

1. Repository and solution skeleton.
2. Public package and transcoder registration.
3. Compatibility test harness based on `fo-dicom.Codecs` tests.
4. RLE Lossless.
5. JPEG family.
6. JPEG-LS family.
7. JPEG 2000 classic.
8. HTJ2K.
9. Package, install, and runtime smoke tests.

The order is intentional. RLE proves the package layout, manager registration, frame handling, and test harness before the harder codecs are implemented.

## Phase Plan

### Phase 0: Project Skeleton

Deliverables:

- Solution file.
- Five production projects, all `netstandard2.0`.
- Test project.
- NuGet package project or packaging configuration.
- Initial README with usage.
- `PureTranscoderManager` stub.

Exit criteria:

- Package builds.
- Test project references the package projects.
- Registration code compiles.

### Phase 1: Codec Registration Compatibility

Deliverables:

- Explicit `PureTranscoderManager` registration using fo-dicom's `IDicomCodec` implementations.
- Stub codecs for all supported transfer syntaxes.
- Tests for `HasCodec`, `GetCodec`, and `CanTranscode`.

Exit criteria:

- `CanTranscode(ExplicitVRLittleEndian, syntax)` returns true for all phase 1 syntaxes.
- `CanTranscode(syntax, ExplicitVRLittleEndian)` returns true for all phase 1 syntaxes.
- Stub encode/decode throws clear `DicomCodecException` until implemented.

### Phase 2: RLE Lossless

Deliverables:

- RLE encoder.
- RLE decoder.
- Segment table validation.
- Multi-frame support.
- 8-bit and 16-bit tests.

Exit criteria:

- Raw to RLE succeeds.
- RLE to raw succeeds.
- Raw to RLE to raw round-trip passes byte-level checks for supported layouts.
- Efferent RLE issue tests are represented in the local test suite.

### Phase 3: JPEG Family

Deliverables:

- JPEG marker parser.
- JPEG Process 1 codec.
- JPEG Process 2/4 codec.
- JPEG Process 14 codec.
- JPEG Process 14 SV1 codec.
- Color and planar conversion support.

Exit criteria:

- 8-bit JPEG Process 1 round-trip works where Efferent supports it.
- 8-bit and 12-bit JPEG Process 2/4 paths work where Efferent supports them.
- Lossless Process 14 and 14 SV1 handle 8-bit, 12-bit, and 16-bit cases required by test data.
- Existing fo-dicom JPEG lossless sample tests still pass.

### Phase 4: JPEG-LS Family

Deliverables:

- JPEG-LS bitstream parser.
- Lossless encode/decode.
- Near-lossless encode/decode.
- Interleave mode support.
- Allowed error support.

Exit criteria:

- JPEG-LS Lossless raw to compressed to raw works.
- JPEG-LS Near-Lossless raw to compressed to raw works within expected tolerance.
- Efferent JPEG-LS acceptance samples decode and render.

### Phase 5: JPEG 2000 Classic

Deliverables:

- JPEG 2000 codestream parser.
- Lossless decode/encode.
- Lossy decode/encode.
- Wavelet transform paths.
- Packet/progression handling needed by DICOM samples.

Exit criteria:

- JPEG 2000 Lossless round-trip works.
- JPEG 2000 Lossy round-trip works with expected lossy tolerance.
- Efferent JPEG 2000 acceptance samples decode and render.

### Phase 6: HTJ2K

Deliverables:

- HTJ2K codestream support.
- HTJ2K Lossless.
- HTJ2K Lossless RPCL.
- HTJ2K Lossy.

Exit criteria:

- All three HTJ2K transfer syntaxes encode and decode.
- Acceptance samples pass.
- Invalid streams fail with managed exceptions.

### Phase 7: Final Replacement Validation

Deliverables:

- NuGet package.
- Usage docs.
- Compatibility matrix.
- Known limitations document if any edge cases remain.

Exit criteria:

- One NuGet package installs into a consumer app.
- Consumer app registers only `PureTranscoderManager`.
- No native DLLs are copied or required.
- All phase 1 transfer syntaxes pass `CanTranscode`.
- Raw to compressed to raw tests pass for 8-bit and 16-bit fixtures where applicable.
- Acceptance DICOM samples transcode, inverse transcode, and render.
- .NET Framework 4.7.2+ smoke test passes.
- Modern .NET smoke test passes.

## Test Strategy

The test suite should be built from four layers:

1. Unit tests for low-level codec primitives.
2. Codec tests for one frame and multi-frame datasets.
3. Compatibility tests based on `D:\Code\dotnet-source\fo-dicom.Codecs\Tests`.
4. Consumer smoke tests using the built NuGet package.

The `fo-dicom.Codecs` tests are useful but not sufficient because some of them only check that conversion does not throw and that pixel data exists. `fo-dicom.PureCodecs` should add stronger checks:

- Exact byte equality for lossless round-trips.
- Tolerance checks for lossy round-trips.
- Frame count preservation.
- Required DICOM tags after compression.
- Photometric interpretation changes when conversion is expected.
- Managed exception behavior for invalid input.

## Compatibility Baseline

Source references used for this design:

- `D:\Code\dotnet-source\fo-dicom`
- `D:\Code\dotnet-source\fo-dicom.Codecs`

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
- `net8.0`-specific production assemblies.
- Replacing fo-dicom core APIs.

## Open Decisions

These decisions must be resolved before or during implementation planning:

- Exact NuGet package id.
- Whether assemblies should be strong-named.
- Minimum fo-dicom package version for phase 1.
- Whether fo-dicom 4.x compatibility is required in the first NuGet release.
- Whether to vendor or port any existing managed codec implementation, subject to license review.
- Performance goals for large multi-frame studies.
