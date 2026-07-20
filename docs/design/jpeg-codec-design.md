# JPEG Codec Design

## Purpose

This document defines the classic JPEG codec family for `fo-dicom.PureCodecs`.

The JPEG family covers DICOM JPEG Baseline, JPEG Extended, and JPEG Lossless transfer syntaxes. It must be implemented in pure C# and must not call libjpeg, IJG, or any native wrapper.

## Assembly

Production assembly:

```text
fo-dicom.PureCodecs.Jpeg.dll
```

Target framework:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

## Supported Transfer Syntaxes

| Transfer syntax | UID | Encode | Decode |
| --- | --- | --- | --- |
| JPEG Baseline Process 1 | `1.2.840.10008.1.2.4.50` | Required | Required |
| JPEG Extended Process 2/4 | `1.2.840.10008.1.2.4.51` | Required | Required |
| JPEG Lossless Process 14 | `1.2.840.10008.1.2.4.57` | Required | Required |
| JPEG Lossless Process 14 SV1 | `1.2.840.10008.1.2.4.70` | Required | Required |

## Public Codec Types

The assembly provides these fo-dicom codec implementations:

```csharp
public sealed class DicomJpegProcess1Codec : IDicomCodec
public sealed class DicomJpegProcess2_4Codec : IDicomCodec
public sealed class DicomJpegLossless14Codec : IDicomCodec
public sealed class DicomJpegLossless14SV1Codec : IDicomCodec
```

They are registered by `PureTranscoderManager` in the entry assembly.

## Family Architecture

The JPEG assembly should be split internally into these implementation areas:

- Marker reader and writer.
- Entropy bit reader and writer.
- Huffman table model and codec.
- Quantization table model.
- Baseline and extended DCT codec.
- Lossless JPEG predictor codec.
- Color conversion helpers.
- DICOM frame adapter.

The public `IDicomCodec` classes should be thin wrappers over reusable internal codecs.

## Marker Support

Decoder must handle at least:

- SOI.
- EOI.
- SOF0 for baseline sequential DCT.
- SOF1 for extended sequential DCT.
- SOF3 for lossless sequential.
- DHT.
- DQT.
- DRI.
- SOS.
- APPn markers by skipping them safely.
- COM markers by skipping them safely.
- RST markers where restart intervals are present.

Unsupported marker combinations must fail with a managed codec exception.

## Process Mapping

### JPEG Baseline Process 1

Expected characteristics:

- 8-bit samples.
- Sequential DCT.
- Huffman entropy coding.
- Lossy.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEGProcess1
```

### JPEG Extended Process 2/4

Expected characteristics:

- Sequential DCT.
- Huffman entropy coding.
- Usually 8-bit or 12-bit in DICOM usage.
- Lossy.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEGProcess2_4
```

### JPEG Lossless Process 14

Expected characteristics:

- Lossless predictive coding.
- Huffman entropy coding.
- Predictor configurable through `DicomJpegParams`.
- Supports 8-bit, 12-bit, and 16-bit cases required by DICOM data.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEGProcess14
```

### JPEG Lossless Process 14 SV1

Expected characteristics:

- Lossless predictive coding.
- Selection value 1.
- Huffman entropy coding.
- Supports 8-bit, 12-bit, and 16-bit cases required by DICOM data.

Primary DICOM syntax:

```text
DicomTransferSyntax.JPEGProcess14SV1
```

## Parameters

Provide a managed equivalent of the fo-dicom JPEG parameter behavior:

- Quality for lossy encoding.
- Predictor for lossless Process 14.
- Point transform.
- Convert color space to RGB on decode when requested.

The concrete parameter type should be compatible with fo-dicom's `DicomCodecParams` model and should not require native-specific settings.

## Encoding Design

For each source frame:

1. Snapshot DICOM pixel metadata.
2. Normalize planar layout if the selected JPEG process requires interleaved input.
3. Convert photometric interpretation when the codec requires a specific component model.
4. Encode one independent JPEG codestream per DICOM frame.
5. Wrap output with even-length buffer rules.
6. Add one compressed frame to `newPixelData`.

The encoder must not write partial output if a frame fails.

## Decoding Design

For each compressed frame:

1. Read the complete frame codestream.
2. Parse JPEG markers and frame metadata.
3. Validate frame dimensions and sample precision against DICOM metadata.
4. Decode entropy-coded data.
5. Reconstruct samples using DCT inverse or lossless predictor path.
6. Convert color space if requested or required for fo-dicom rendering compatibility.
7. Add one raw frame to `newPixelData`.

## Color and Photometric Handling

The implementation must account for DICOM photometric interpretations commonly used with JPEG:

- `MONOCHROME1`
- `MONOCHROME2`
- `RGB`
- `YBR_FULL`
- `YBR_FULL_422`

Rules:

- Decode should preserve DICOM semantics unless fo-dicom default behavior expects conversion.
- For `JPEGProcess1` and `JPEGProcess2_4`, default input codec parameters in fo-dicom convert colorspace to RGB. The replacement should preserve that behavior.
- Unsupported photometric interpretations must fail explicitly.

## Validation Rules

Decode must reject:

- Missing SOI.
- Missing SOF.
- Missing SOS.
- Missing Huffman table.
- Missing quantization table for DCT processes.
- Unsupported arithmetic coding.
- Unsupported progressive coding unless explicitly implemented.
- Marker segment lengths outside the input buffer.
- Decoded dimensions that conflict with DICOM metadata.

Encode must reject:

- Unsupported bit depth for selected transfer syntax.
- Unsupported samples per pixel.
- Unsupported photometric interpretation.
- Unsupported planar conversion.

## Error Handling

All errors must be managed.

Error messages should include:

- JPEG family.
- Specific transfer syntax.
- Encode or decode.
- Frame index.
- Marker or stage that failed when known.

## Tests

### Unit Tests

- Marker parsing.
- Huffman table construction.
- Bit reader and bit writer.
- Restart marker handling.
- Lossless predictor functions.
- DCT block encode/decode primitives.
- Invalid marker length handling.

### Codec Tests

- Process 1 8-bit raw -> JPEG -> raw round-trip with lossy tolerance.
- Process 2/4 8-bit raw -> JPEG -> raw round-trip with lossy tolerance.
- Process 2/4 12-bit monochrome coverage where sample data is available.
- Process 14 8-bit, 12-bit, and 16-bit lossless exact round-trip.
- Process 14 SV1 8-bit, 12-bit, and 16-bit lossless exact round-trip.
- `YBR_FULL` and `YBR_FULL_422` decode behavior.
- RGB planar and interleaved layout coverage where supported.

### Compatibility Tests

Use sample coverage from:

- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit\TranscodeUnitTest.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\AcceptanceTests.cs`
- fo-dicom JPEG lossless samples under `<FO_DICOM_SOURCE_ROOT>\Tests`

## Completion Criteria

JPEG is complete when:

- All four JPEG transfer syntaxes are registered by `PureTranscoderManager`.
- Encode and decode are implemented without native dependencies.
- Lossless transfer syntaxes pass exact byte equality tests for supported data.
- Lossy transfer syntaxes pass agreed tolerance tests.
- Efferent JPEG acceptance samples transcode, inverse transcode, and render.
- Invalid JPEG streams fail with managed exceptions.

## Implementation Notes

Current implementation status:

- JPEG Process 1 and Process 2/4 use a managed sequential DCT path with marker parsing, DQT, DHT, SOF0/SOF1, SOS, Huffman entropy coding, quantization, zigzag, forward DCT, and inverse DCT.
- The DCT path supports 8-bit monochrome and three-component interleaved data, plus 12-bit monochrome data in a 16-bit DICOM sample container for Process 2/4. Decode handles standard Efferent JPEG baseline `YBR_FULL` and `YBR_FULL_422` acceptance samples.
- JPEG Process 14 and Process 14 SV1 use a managed lossless predictive path with predictors 1 through 7, Huffman-coded differences, and exact 8-bit, 12-bit, and 16-bit monochrome round-trips.
- `JpegCodecParams` provides quality, predictor, point transform, and `ConvertColorspaceToRGB`, with color conversion enabled by default for Process 1 and Process 2/4.
- RGB planar input is normalized to interleaved layout for sequential DCT encode, and decoded output can be converted back to planar when the target pixel data requires it.

Known limitations before full JPEG release readiness:

- Process 2/4 high-bit support is monochrome-only for `BitsStored=12` in a 16-bit DICOM sample container; high-bit colour data remains unsupported.
- The sequential DCT implementation prioritizes correctness and compatibility coverage over optimized performance.
- Progressive JPEG, arithmetic coding, CMYK/YCCK, and restart interval MCU resynchronization beyond marker recognition are not implemented.
- Efferent acceptance coverage currently verifies JPEG baseline YBRFull/YBR422 decode; broader transcode/render parity still belongs to the full compatibility matrix.
