# JPEG-LS Codec Design

## Purpose

This document defines the JPEG-LS codec family for `fo-dicom.PureCodecs`.

The JPEG-LS family covers lossless and near-lossless transfer syntaxes. It must be implemented in pure C# and must not call CharLS or any native wrapper.

## Assembly

Production assembly:

```text
fo-dicom.PureCodecs.JpegLs.dll
```

Target framework:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

## Supported Transfer Syntaxes

| Transfer syntax | UID | Encode | Decode |
| --- | --- | --- | --- |
| JPEG-LS Lossless | `1.2.840.10008.1.2.4.80` | Required | Required |
| JPEG-LS Near-Lossless | `1.2.840.10008.1.2.4.81` | Required | Required |

## Public Codec Types

The assembly provides these fo-dicom codec implementations:

```csharp
public sealed class DicomJpegLsLosslessCodec : IDicomCodec
public sealed class DicomJpegLsNearLosslessCodec : IDicomCodec
```

They are registered by `PureTranscoderManager` in the entry assembly.

## Family Architecture

The JPEG-LS assembly should be split internally into:

- JPEG-LS marker reader and writer.
- Frame information parser.
- Preset coding parameter model.
- Regular mode encoder and decoder.
- Run mode encoder and decoder.
- Golomb coding helpers.
- Context model.
- Interleave adapter.
- DICOM frame adapter.

The public codec classes should be thin wrappers.

## Marker Support

Decoder must handle at least:

- SOI.
- EOI.
- SOF55.
- SOS.
- LSE preset coding parameters.
- APPn markers by skipping them safely.
- COM markers by skipping them safely.

Unsupported marker combinations must fail with a managed codec exception.

## Parameters

Provide a managed JPEG-LS parameter type compatible with `DicomCodecParams`.

Required settings:

- `AllowedError`.
- `InterleaveMode`.
- `ColorTransform`.

Defaults should preserve `fo-dicom.Codecs` behavior unless a compatibility test proves a different default is required.

## Encoding Design

For each source frame:

1. Snapshot DICOM pixel metadata.
2. Validate width, height, bits stored, samples per pixel, and frame size.
3. Normalize planar or interleaved layout according to selected interleave mode.
4. Apply supported color transform only when requested and valid.
5. Emit one JPEG-LS codestream per DICOM frame.
6. Wrap output with even-length buffer rules.
7. Add one compressed frame to `newPixelData`.

For lossless syntax:

```text
AllowedError = 0
```

For near-lossless syntax:

```text
AllowedError = parameters.AllowedError
```

## Decoding Design

For each compressed frame:

1. Read the complete JPEG-LS codestream.
2. Parse markers and preset coding parameters.
3. Validate image metadata against DICOM pixel metadata.
4. Decode regular mode and run mode segments.
5. Reconstruct sample values.
6. Reapply output layout expected by fo-dicom pixel data.
7. Add one raw frame to `newPixelData`.

## Interleave Modes

JPEG-LS supports:

- None.
- Line.
- Sample.

The implementation must map these to DICOM `SamplesPerPixel` and `PlanarConfiguration` correctly.

Rules:

- Single-sample data should use no interleave.
- Multi-sample interleaved DICOM data should support sample interleave.
- Multi-sample planar DICOM data should support line interleave or explicit conversion when required.
- Unsupported combinations must fail explicitly.

## Near-Lossless Behavior

Near-lossless round-trip tests must not require exact byte equality.

Validation should check:

```text
abs(originalSample - decodedSample) <= AllowedError
```

for each sample, accounting for pixel representation and bit depth.

## Validation Rules

Decode must reject:

- Missing SOI.
- Missing SOF55.
- Missing SOS.
- Missing EOI when the stream requires it.
- Unsupported bit depth.
- Unsupported component count.
- Invalid preset coding parameters.
- Marker segment lengths outside input bounds.
- Decoded dimensions that conflict with DICOM metadata.

Encode must reject:

- Unsupported bit depth.
- Unsupported samples per pixel.
- Unsupported photometric interpretation.
- Unsupported interleave mode.
- Invalid near-lossless `AllowedError`.

## Error Handling

All failures must remain managed.

Error messages should include:

- JPEG-LS family.
- Lossless or near-lossless transfer syntax.
- Encode or decode.
- Frame index.
- Marker or coding stage that failed when known.

## Tests

### Unit Tests

- Marker parsing and writing.
- Preset coding parameter defaults.
- Golomb code encode/decode.
- Regular mode context update.
- Run mode encode/decode.
- Near-lossless sample tolerance checks.
- Invalid marker length handling.

### Codec Tests

- Lossless 8-bit monochrome exact round-trip.
- Lossless 16-bit monochrome exact round-trip.
- Lossless RGB exact round-trip where supported.
- Near-lossless 8-bit tolerance round-trip.
- Near-lossless 16-bit tolerance round-trip.
- Multi-frame data.
- Interleave mode coverage.

### Compatibility Tests

Use sample coverage from:

- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit\TranscodeUnitTest.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\PM5644-960x540_JPEG-LS_Lossless.dcm`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\PM5644-960x540_JPEG-LS_NearLossless.dcm`

## Completion Criteria

JPEG-LS is complete when:

- Both JPEG-LS transfer syntaxes are registered by `PureTranscoderManager`.
- Encode and decode are implemented without native dependencies.
- Lossless paths pass exact byte equality tests.
- Near-lossless paths pass allowed-error tolerance tests.
- Efferent JPEG-LS acceptance samples transcode, inverse transcode, and render.
- Invalid JPEG-LS streams fail with managed exceptions.
