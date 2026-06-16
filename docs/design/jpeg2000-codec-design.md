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

### Shared Family Boundary

Classic JPEG 2000 and HTJ2K are part of the same JPEG 2000 codestream family.
They must not be implemented as two unrelated mini-codecs. The implementation
must share code for:

- Raw codestream marker reading and writing.
- SIZ, COD, QCD, SOT, SOD, EOC, COM, and related marker payload construction
  and parsing when the marker syntax is the same.
- Image, tile, component, precinct, code-block, packet, and progression-order
  model types.
- DICOM pixel metadata validation, Ssiz precision/sign mapping, frame-size
  validation, and decoded-metadata validation.
- Reversible 5/3 and irreversible 9/7 wavelet transform helpers.
- Quantization step-size encode/decode, guard-bit handling, subband indexing,
  and inverse quantization helpers.
- Big-endian byte I/O and marker-safe payload utilities.

The implementation must keep only the entropy/block-coding layer split:

- Classic JPEG 2000 `.90` and `.91` use classic Tier-1 EBCOT/MQ code-block
  coding and classic packet contribution handling.
- HTJ2K `.201`, `.202`, and `.203` use Part 15 HT block coding: MEL, VLC,
  MagSgn, cleanup/refinement handling, and HT-specific segment assembly.

Shared code must be named after the JPEG 2000 standard concept, not after a
reference implementation. Names such as `OpenJpeg*` or `OpenJph*` are allowed
only in tests, fixture provenance, comments that cite reference vectors, or
adapter code for inspecting external baselines. Production implementation types
must use names such as `Jpeg2000StandardWavelet`,
`Jpeg2000StandardIrreversibleWavelet`, `Jpeg2000QuantizationTable`, or
`Jpeg2000MarkerPayloadBuilder`.

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

For classic JPEG 2000 `.90` and `.91`, `fo-dicom.Codecs` currently reaches
that public behavior through OpenJPEG. When validating output compatibility,
prefer a locally generated `fo-dicom.Codecs`/OpenJPEG DICOM baseline over
third-party managed or Go codec output. Reference-library names belong in
tests, diagnostics, and provenance notes only; production implementation names
must stay standard-oriented.

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

### Classic OpenJPEG Alignment Order

For classic JPEG 2000 `.90` and `.91`, compatibility work must follow the
`fo-dicom.Codecs`/OpenJPEG encode pipeline before interpreting final binary
differences:

1. Match DICOM-to-component sample mapping, including signed extension,
   unsigned masking, `BitsStored`, `BitsAllocated`, and
   `EncodeSignedPixelValuesAsUnsigned`.
2. Match OpenJPEG encoder parameters: six resolutions, 64 x 64 code-blocks,
   LRCP by default, optional MCT, and `RateLevels`/`Rate` layer construction.
3. Match reversible 5/3 and irreversible 9/7 DWT geometry and coefficient
   placement. The irreversible forward 9/7 path must follow OpenJPEG's
   `OPJ_FLOAT32` arithmetic before Tier-1 quantization; a managed `double`
   substitute changes low bit-plane pass payloads.
4. Match lossless no-quantization QCD and lossy scalar-expounded QCD step-size
   generation.
5. Match Tier-1 pass coding, cumulative pass lengths, and cumulative
   `distortiondec`.
6. Match PCRD layer allocation using pass `dd/dr` and OpenJPEG-style packet
   byte budget checks.
7. Match Tier-2 packet header/body state across quality layers.
   OpenJPEG 2.5.4 default builds do not use the optional
   `ENABLE_EMPTY_PACKET_OPTIMIZATION` path, so even packets with no new
   code-block contribution write the packet-present bit and tag-tree header
   state instead of a single `00` empty-packet byte.
8. Use final codestream size and binary comparison only as terminal
   compatibility signals after the stages above are aligned.

Multi-layer support means real quality-layer packet contribution distribution.
`COD.Layers`, `NumLayers`, or a larger layer count alone is not sufficient.

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

The current pure C# HTJ2K path writes a standard JPEG 2000 codestream envelope
with SIZ, CAP, COD, QCD, SOT, SOD, and EOC markers, and uses Part 15 HT block
coding in packet tile data instead of a project-managed payload. The HT block
path includes MEL event coding, OpenJPH/Annex C VLC table validation, UVLC,
MagSgn coding, cleanup quad scanning, HT packet header handling, and reversible
and scalar-expounded irreversible block reconstruction.

The test suite includes standard HT cleanup-pass vectors generated by OpenJPH
`ojph_encode_codeblock32` and verified with OpenJPH
`ojph_decode_codeblock32`. It also includes an OpenJPH irreversible HTJ2K
codestream fixture decoded by the pure decoder.

Public interoperability coverage is represented by tests that encode HTJ2K
Lossless, HTJ2K Lossless RPCL, and HTJ2K Lossy frames with the pure C# encoder
and decode them through `fo-dicom.Codecs`/OpenJPH native codecs. The lossless
paths assert exact decoded frame equality; the lossy path asserts decoded pixel
tolerance and irreversible COD/QCD marker shape.

## Current DICOM Integration Coverage

The DICOM adapter now matches the `fo-dicom.Codecs` public JPEG 2000
parameter contract for phase 1 integration:

- `DicomJpeg2000Params` derives from fo-dicom's JPEG 2000 parameter type and
  preserves `Irreversible`, `Rate`, `RateLevels`, `AllowMCT`,
  `UpdatePhotometricInterpretation`, and
  `EncodeSignedPixelValuesAsUnsigned` behavior.
- The pure parameter type adds managed `Jpeg2000ProgressionOrder`; HTJ2K
  parameters derive from fo-dicom's HTJ2K type and map core
  `ProgressionOrder` values to the internal progression enum.
- Classic JPEG 2000 encoding writes COD progression order, layer count,
  multiple-component transform use, transform type, and SIZ component
  signedness from the requested parameters. Multi-layer support must be real
  packet-layer contribution support: the encoder must distribute code-block
  passes across quality layers, not only write a larger COD layer count and
  leave early layers empty.
- Classic JPEG 2000 encoding pads an odd-length EOC-terminated codestream with
  a trailing `00` byte for the DICOM encapsulated item. This padding is outside
  the logical JPEG 2000 codestream and must not be counted in SOT `Psot` or
  packet tile-data length.
- Classic JPEG 2000 lossy encoding uses OpenJPEG-compatible irreversible QCD
  step-size generation. The managed rate-control must target
  `DicomJpeg2000Params.Rate` and `RateLevels` using OpenJPEG-compatible
  quality-layer behavior for the public `fo-dicom.Codecs` contract. Decoded
  pixels use lossy tolerance, but the `D:\1.dcm` classic JPEG 2000 regression
  fixture must also compare codestream frame size against the generated
  `fo-dicom.Codecs`/OpenJPEG baseline.
- Classic lossy Tier-1 bit-plane depth must be derived from the encoded QCD
  step-size exponent plus guard bits, matching OpenJPEG's band `numbps`
  calculation. Do not infer irreversible band depth only from component
  precision and subband gain; signed 16-bit fixtures expose that error as a
  large reconstruction offset even when packet truncation is disabled.
- RGB input is normalized to interleaved component order for encoding and
  decoded frames are repacked to the target fo-dicom raw layout, including
  planar RGB targets. Monochrome, RGB, and supported YBR-related photometric
  interpretations have explicit paths; unsupported photometric values fail
  with managed `DicomCodecException`.
- Component subsampling and unsupported standard progression orders fail with
  explicit managed exceptions.
- JPEG 2000 Part 2 multi-component syntaxes `.92` and `.93`, JPIP, and JPT
  transfer syntaxes are explicitly outside phase 1 and are not registered by
  `PureTranscoderManager`.

The project-managed classic encoder writes a raw codestream envelope with SIZ,
COD, QCD, SOT, SOD, and EOC markers and stores standard-compatible tile data.
The HTJ2K encoder writes the corresponding HTJ2K codestream envelope with SIZ,
CAP, COD, QCD, SOT, SOD, and EOC markers and standard Part 15 HT packet data.
`PureTranscoderManager` registers HTJ2K Lossless, HTJ2K Lossless RPCL, and
HTJ2K Lossy; JPIP, JPT, JPEG XL, and JPEG 2000 Part 2 multi-component syntaxes
remain outside phase 1.

The standard reader now honors SOT tile-part lengths before falling back to
EOC scanning for unknown-length tile-parts, and stops parsing a frame at EOC
so DICOM item padding is not interpreted as another marker.

Verified DICOM integration coverage includes:

- Multi-frame JPEG 2000 round-trips with frame count preservation.
- Required DICOM compression tag checks for JPEG 2000 lossless and JPEG 2000
  lossy.
- Managed exceptions for invalid JPEG 2000 and HTJ2K codestreams.
- Efferent JPEG 2000 acceptance sample decode baselines, inverse transcode
  round-trips for unit and RGB samples, and render smoke tests where rendering
  dependencies are available.
- Pure HTJ2K output decoded by `fo-dicom.Codecs`/OpenJPH for lossless,
  lossless RPCL, and lossy transfer syntaxes.

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
