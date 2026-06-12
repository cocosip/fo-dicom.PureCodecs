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
- SOT.
- SOD.
- EOC.
- PLT/PPM/PPT where supported by target samples.
- Tile-part markers needed by DICOM datasets.

Unsupported legal markers should fail with a clear managed exception until implemented. Markers must never cause out-of-bounds reads.

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
- Rate or quality target.
- Progression order where supported.

HTJ2K parameters should cover:

- Lossless vs lossy mode.
- RPCL selection for the RPCL transfer syntax.
- Quality or rate target for lossy encoding.

Do not expose native-library concepts.

## Encoding Design

For each source frame:

1. Snapshot DICOM pixel metadata.
2. Validate dimensions, bit depth, signedness, components, and frame size.
3. Normalize component layout for encoder internals.
4. Apply reversible or irreversible transform according to transfer syntax and parameters.
5. Partition into tiles, precincts, and code-blocks.
6. Encode code-blocks using classic or HTJ2K path.
7. Write packets and marker segments with the requested progression order.
8. Add one compressed frame to `newPixelData`.

The first implementation may use a conservative single-tile strategy if it remains compatible with DICOM and acceptance requirements. Any limitation must be documented before release.

## Decoding Design

For each compressed frame:

1. Read the complete codestream.
2. Parse marker segments and build the image/tile/component model.
3. Validate codestream metadata against DICOM pixel metadata.
4. Decode tile-parts and packets in codestream order.
5. Decode code-blocks using classic JPEG 2000 or HTJ2K path.
6. Inverse quantize.
7. Apply inverse wavelet transform.
8. Apply inverse component transform.
9. Repack samples into fo-dicom raw pixel layout.
10. Add one raw frame to `newPixelData`.

## Component and Photometric Handling

The implementation must account for common DICOM image data:

- Monochrome.
- RGB.
- YBR-related photometric interpretations used by JPEG 2000 transfer syntaxes.
- Signed and unsigned samples.
- 8-bit and 16-bit allocated samples.

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
- Codestream dimensions that conflict with DICOM metadata.
- Packets or code-blocks that exceed declared bounds.

Encode must reject:

- Unsupported bit depth.
- Unsupported samples per pixel.
- Unsupported photometric interpretation.
- Unsupported progression order for the selected transfer syntax.
- Invalid rate or quality parameters.

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
- Classic entropy coding primitives.
- HT block coding primitives.
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

## Implementation Risk

This is the highest-risk codec family in phase 1.

Risks:

- Full JPEG 2000 and HTJ2K implementation complexity is high.
- DICOM datasets may contain progression orders, tile layouts, or marker combinations not covered by initial samples.
- Lossy tolerance must be defined carefully to avoid false failures.
- Performance may require iterative optimization after correctness is achieved.

Mitigation:

- Build marker and codestream parser tests first.
- Add fixtures incrementally.
- Keep classic JPEG 2000 and HTJ2K entropy paths isolated.
- Prefer correctness over speed until compatibility tests are stable.
