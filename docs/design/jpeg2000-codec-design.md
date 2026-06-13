# JPEG 2000 and HTJ2K Codec Design

## Purpose

This document defines the JPEG 2000 codec family for `fo-dicom.PureCodecs`.

This family covers classic JPEG 2000 and High-Throughput JPEG 2000 transfer syntaxes. It must be implemented in pure C# and must not call OpenJPEG, OpenJPH, or any native wrapper.

## Assembly

Production assembly:

```text
fo-dicom.PureCodecs.Jpeg2000.dll
```

Target framework:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

## Supported Transfer Syntaxes

| Transfer syntax | UID | Encode | Decode |
| --- | --- | --- | --- |
| JPEG 2000 Lossless | `1.2.840.10008.1.2.4.90` | Required | Required |
| JPEG 2000 Lossy | `1.2.840.10008.1.2.4.91` | Required | Required |
| HTJ2K Lossless | `1.2.840.10008.1.2.4.201` | Required | Required |
| HTJ2K Lossless RPCL | `1.2.840.10008.1.2.4.202` | Required | Required |
| HTJ2K Lossy | `1.2.840.10008.1.2.4.203` | Required | Required |

JPEG 2000 Part 2 Multi-component transfer syntaxes are intentionally outside phase 1:

- `1.2.840.10008.1.2.4.92`.
- `1.2.840.10008.1.2.4.93`.

The Go reference codec contains useful Part 2 structures, but these UIDs must not expand the phase 1 scope unless the project plan changes.

## Public Codec Types

The assembly provides these fo-dicom codec implementations:

```csharp
public sealed class DicomJpeg2000LosslessCodec : IDicomCodec
public sealed class DicomJpeg2000LossyCodec : IDicomCodec
public sealed class DicomHtJpeg2000LosslessCodec : IDicomCodec
public sealed class DicomHtJpeg2000LosslessRpclCodec : IDicomCodec
public sealed class DicomHtJpeg2000LossyCodec : IDicomCodec
```

They are registered by `PureTranscoderManager` in the entry assembly.

## Family Architecture

The JPEG 2000 assembly should be split internally into:

- Codestream marker reader and writer.
- Image, tile, component, precinct, code-block, and packet models.
- Progression order logic.
- Quantization model.
- Discrete wavelet transform and inverse transform.
- Classic JPEG 2000 entropy coding.
- HTJ2K block coding.
- Multi-component transform support.
- DICOM frame adapter.

Classic JPEG 2000 and HTJ2K should share codestream infrastructure where the standard allows it, but their block coding paths should remain separate.

## Marker Support

Decoder must handle required JPEG 2000 codestream markers, including:

- SOC.
- SIZ.
- COD.
- COC where needed.
- QCD.
- QCC where needed.
- POC when progression order changes are supported by target samples.
- RGN and COM with explicit supported, ignored, or rejected behavior.
- SOT.
- SOD.
- EOC.
- SOP/EPH where supported by target samples.
- PLT/PPM/PPT where supported by target samples.
- Tile-part markers needed by DICOM datasets.

Unsupported legal markers should fail with a clear managed exception until implemented. Markers must never cause out-of-bounds reads.

The decoder must explicitly distinguish raw J2K codestream frames from JP2-wrapped frames. JP2 support is optional for phase 1, but unsupported JP2 wrappers must fail with a clear managed exception instead of being misparsed as raw codestreams.

## Transfer Syntax Mapping

### JPEG 2000 Lossless

Expected characteristics:

- Reversible color transform when applicable.
- Reversible wavelet transform.
- Lossless reconstruction.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEG2000Lossless
```

### JPEG 2000 Lossy

Expected characteristics:

- Irreversible transform and quantization when requested.
- Lossy reconstruction.
- Rate or quality parameters should be represented in managed codec params.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEG2000Lossy
```

### HTJ2K Lossless

Expected characteristics:

- HT block coding.
- Lossless reconstruction.

Primary DICOM syntax:

```text
DicomTransferSyntax.HTJ2KLossless
```

### HTJ2K Lossless RPCL

Expected characteristics:

- HT block coding.
- Lossless reconstruction.
- RPCL progression order.

Primary DICOM syntax:

```text
DicomTransferSyntax.HTJ2KLosslessRPCL
```

### HTJ2K Lossy

Expected characteristics:

- HT block coding.
- Lossy reconstruction.

Primary DICOM syntax:

```text
DicomTransferSyntax.HTJ2K
```

## Parameters

Provide managed parameter types compatible with `DicomCodecParams`.

JPEG 2000 parameters should cover:

- Lossless vs lossy mode.
- Irreversible transform selection.
- OpenJPEG-compatible `Rate` and `RateLevels`.
- Progression order where supported.
- Multi-component transform enablement through `AllowMCT`.
- Photometric update behavior through `UpdatePhotometricInterpretation`.
- Signed-pixel encoding behavior through `EncodeSignedPixelValuesAsUnsigned`.
- Quality-layer count and target-ratio semantics where exposed by managed parameters.

HTJ2K parameters should cover:

- Lossless vs lossy mode.
- `ProgressionOrder`, including mandatory RPCL behavior for the RPCL transfer syntax.
- Quality or rate target for lossy encoding.

The compatibility baseline is the public behavior of `fo-dicom.Codecs`, not its native implementation details. Do not expose native-library concepts such as OpenJPEG handles, OpenJPH tables, or native stream objects.

## Encoding Design

For each source frame:

1. Snapshot DICOM pixel metadata.
2. Validate dimensions, bit depth, signedness, components, and frame size.
3. Map DICOM `BitsAllocated`, `BitsStored`, `PixelRepresentation`, and component layout to JPEG 2000 `Ssiz` precision and sign.
4. Normalize component layout for encoder internals.
5. Apply DC level shift for unsigned and signed samples.
6. Apply RCT/ICT only when the transfer syntax and `AllowMCT` permit it.
7. Apply reversible or irreversible wavelet transform according to transfer syntax and parameters.
8. Quantize for lossy paths.
9. Partition into tiles, precincts, and code-blocks.
10. Encode code-blocks using classic or HTJ2K path.
11. Write packets and marker segments with the requested progression order.
12. Add one compressed frame to `newPixelData`.

The first implementation may use a conservative single-tile strategy if it remains compatible with DICOM and acceptance requirements. Any limitation must be documented before release.

## Decoding Design

For each compressed frame:

1. Read the complete codestream.
2. Parse marker segments and build the image/tile/component model.
3. Validate codestream metadata against DICOM pixel metadata.
4. Decode tile-parts and packets in codestream order.
5. Decode code-blocks using classic JPEG 2000 or HTJ2K path.
6. Inverse quantize when needed.
7. Apply inverse wavelet transform.
8. Apply inverse component transform when the codestream uses RCT, ICT, or a supported component transform.
9. Undo DC level shift.
10. Repack samples into fo-dicom raw pixel layout.
11. Add one raw frame to `newPixelData`.

## Component and Photometric Handling

The implementation must account for common DICOM image data:

- Monochrome.
- RGB.
- YBR-related photometric interpretations used by JPEG 2000 transfer syntaxes.
- Signed and unsigned samples.
- 8-bit and 16-bit allocated samples.
- Planar and interleaved RGB input layouts.
- Component precision and sign from JPEG 2000 `Ssiz`.
- Component subsampling. Unsupported subsampling must fail explicitly.

Unsupported component transforms or photometric interpretations must fail explicitly.

## Validation Rules

Decode must reject:

- Missing SOC.
- Missing SIZ.
- Missing COD.
- Missing tile-part data.
- Marker lengths outside input bounds.
- Unsupported progression order.
- Unsupported number of decomposition levels.
- Unsupported component precision.
- Unsupported component subsampling.
- Unsupported JP2 wrapper when JP2 support is not implemented.
- Codestream dimensions that conflict with DICOM metadata.
- Packets or code-blocks that exceed declared bounds.
- Tile-part lengths or indexes that conflict with declared tile structure.

Encode must reject:

- Unsupported bit depth.
- Unsupported samples per pixel.
- Unsupported photometric interpretation.
- Unsupported progression order for the selected transfer syntax.
- Invalid rate or quality parameters.
- JPEG 2000 Part 2 transfer syntaxes `.92` and `.93` during phase 1.

## Error Handling

All failures must remain managed.

Error messages should include:

- JPEG 2000 or HTJ2K family.
- Specific transfer syntax.
- Encode or decode.
- Frame index.
- Tile and component where known.
- Marker or coding stage that failed when known.

## Tests

### Unit Tests

- Marker parsing and writing.
- SIZ/COD/QCD validation.
- Progression order iteration.
- Tile and packet indexing.
- Reversible wavelet transform round-trip.
- Irreversible wavelet transform tolerance checks.
- DC level shift and signedness mapping.
- RCT/ICT behavior with `AllowMCT`.
- Classic entropy coding primitives.
- Tier-1 pass accounting and tag-tree packet header behavior.
- HT block coding primitives.
- MEL, VLC, MagSgn, and HT cleanup primitives.
- Invalid marker length handling.

### Codec Tests

- JPEG 2000 Lossless 8-bit exact round-trip.
- JPEG 2000 Lossless 16-bit exact round-trip.
- JPEG 2000 Lossy tolerance round-trip.
- HTJ2K Lossless exact round-trip.
- HTJ2K Lossless RPCL exact round-trip with RPCL progression.
- HTJ2K Lossy tolerance round-trip.
- Multi-frame data.
- Monochrome and RGB sample data.
- `DicomJpeg2000Params` compatibility behavior.
- `DicomHtJpeg2000Params.ProgressionOrder` compatibility behavior.

### Compatibility Tests

Use sample coverage from:

- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit\TranscodeUnitTest.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\PM5644-960x540_JPEG2000-Lossless.dcm`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\PM5644-960x540_JPEG2000-Lossy.dcm`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\PM5644-960x540_JPEG2000-Lossy50.dcm`
- Any HTJ2K fixtures added to the local test suite.

## Completion Criteria

JPEG 2000 is complete when:

- All five JPEG 2000 and HTJ2K transfer syntaxes are registered by `PureTranscoderManager`.
- Encode and decode are implemented without native dependencies.
- Lossless paths pass exact byte equality tests.
- Lossy paths pass agreed tolerance tests.
- Efferent JPEG 2000 acceptance samples transcode, inverse transcode, and render.
- HTJ2K fixtures pass encode/decode tests.
- Invalid codestreams fail with managed exceptions.

## Current HTJ2K Reference Coverage

The current pure C# HTJ2K path implements the project-managed HT block
pipeline used by the local codec tests: MEL event coding, a validated HT VLC
table subset based on OpenJPH Annex C table entries, MagSgn coding, cleanup
quad scanning, three-segment block assembly, and DICOM encode/decode
round-trips for the three phase 1 HTJ2K transfer syntaxes.

The test suite also includes a standard HT cleanup-pass vector generated by
OpenJPH `ojph_encode_codeblock32` and verified with OpenJPH
`ojph_decode_codeblock32`. The vector exercises CUP-only Scup parsing,
OpenJPH/Annex C VLC lookup entries, UVLC decoding, and standard MagSgn
forward bit reading.

This is still not full byte-for-byte interoperability with arbitrary OpenJPH
or OpenJPEG HTJ2K codestream output. The independent vector covers the first
standard block path; broader standard codestream import, non-initial quad
rows, and refinement passes remain future compatibility work.

## Implementation Risk

This is the highest-risk codec family in phase 1.

Risks:

- Full JPEG 2000 and HTJ2K implementation complexity is high.
- DICOM datasets may contain progression orders, tile layouts, or marker combinations not covered by initial samples.
- Lossy tolerance must be defined carefully to avoid false failures.
- `fo-dicom.Codecs` uses native codec behavior as the compatibility baseline; public parameter semantics must be matched without copying native implementation concepts.
- The Go JPEG 2000 reference is useful for structure and tests, but its Part 2 support is outside phase 1 and its HTJ2K notes still identify partial VLC and cleanup-pass work.
- Performance may require iterative optimization after correctness is achieved.

Mitigation:

- Build marker and codestream parser tests first.
- Add fixtures incrementally.
- Keep classic JPEG 2000 and HTJ2K entropy paths isolated.
- Cross-check HTJ2K block coding against OpenJPH or OpenJPEG reference vectors before marking support complete.
- Prefer correctness over speed until compatibility tests are stable.
