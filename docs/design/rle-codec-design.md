# RLE Codec Design

## Purpose

This document defines the RLE Lossless codec family for `fo-dicom.PureCodecs`.

RLE is the first algorithm to implement because it is pure byte-level DICOM logic, has no external transform dependency, and proves the codec entry layer before the JPEG families are built.

## Assembly

Production assembly:

```text
fo-dicom.PureCodecs.Rle.dll
```

Target framework:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

## Supported Transfer Syntax

| Transfer syntax | UID | Encode | Decode |
| --- | --- | --- | --- |
| RLE Lossless | `1.2.840.10008.1.2.5` | Required | Required |

## Public Codec Type

The assembly provides one fo-dicom codec implementation:

```csharp
public sealed class DicomRleLosslessCodec : IDicomCodec
```

It is registered by `PureTranscoderManager` in the entry assembly.

## DICOM RLE Frame Structure

Each compressed frame contains:

- A 64-byte RLE header.
- A little-endian segment count in the first 4 bytes.
- Fifteen little-endian 32-bit segment offsets.
- One to fifteen encoded segments.

The segment count must equal:

```text
BytesAllocated * SamplesPerPixel
```

Valid DICOM RLE supports at most 15 segments. Inputs requiring more than 15 segments must fail with a managed codec exception.

## Encoding Design

For each source frame:

1. Read raw frame bytes through `DicomPixelData.GetFrame(frame)`.
2. Compute `pixelCount = Width * Height`.
3. Compute `segmentCount = BytesAllocated * SamplesPerPixel`.
4. Write the 64-byte placeholder header.
5. For each segment:
   - Record the current output offset.
   - Emit one RLE segment using PackBits-style runs.
   - Segment order is high byte first for each sample.
   - Honor planar vs interleaved source layout.
   - Pad segment to even length when needed.
6. Rewrite the header with segment count and offsets.
7. Add one compressed frame to `newPixelData`.

### Run Encoding Rules

Literal runs:

- Control byte `0` to `127`.
- Followed by `control + 1` literal bytes.

Repeat runs:

- Control byte `-1` to `-127`.
- Followed by one repeated byte.
- Repeat count is `1 - control`.

No-op:

- Control byte `-128`.
- Should not be emitted by encoder.

## Decoding Design

For each compressed frame:

1. Read the RLE frame bytes through `DicomPixelData.GetFrame(frame)`.
2. Parse the 64-byte header.
3. Validate segment count.
4. Validate segment offsets are within frame bounds and monotonically increasing.
5. Allocate one raw output frame sized to `newPixelData.UncompressedFrameSize`, rounded to even length if needed.
6. Decode each segment into the correct sample byte position.
7. Add the raw frame to `newPixelData`.

## Planar and Interleaved Layout

For interleaved output:

```text
pos = sample * BytesAllocated + byteIndexWithinSample
stride = SamplesPerPixel * BytesAllocated
```

For planar output:

```text
pos = sample * BytesAllocated * pixelCount + byteIndexWithinSample
stride = BytesAllocated
```

RLE segment byte order uses most significant byte first for each sample. The implementation must map DICOM RLE segment order back to fo-dicom raw frame layout.

## Validation Rules

Decode must reject:

- Frame shorter than 64 bytes.
- Segment count less than 1.
- Segment count greater than 15.
- Segment count not equal to expected segment count.
- Segment offset outside the frame.
- Segment offsets not increasing.
- Literal runs that exceed the segment input.
- Repeat runs that exceed output frame size.
- Decoded segments that cannot fill the expected output layout.

Encode must reject:

- `BytesAllocated * SamplesPerPixel > 15`.
- Raw frame shorter than required.
- Unsupported pixel metadata.

## Error Handling

All validation failures must throw managed exceptions, preferably `DicomCodecException` at the public codec boundary.

Error messages should include:

- `RLE Lossless`.
- Encode or decode.
- Frame index.
- Specific validation failure.

## Tests

### Unit Tests

- Header parse and write.
- Literal run encoding.
- Repeat run encoding.
- Mixed literal and repeat runs.
- Segment offset validation.
- Invalid segment count validation.
- Truncated input validation.

### Codec Tests

- 8-bit monochrome raw -> RLE -> raw exact byte equality.
- 16-bit monochrome raw -> RLE -> raw exact byte equality.
- RGB interleaved raw -> RLE -> raw exact byte equality.
- RGB planar raw -> RLE -> raw exact byte equality.
- Multi-frame raw -> RLE -> raw exact byte equality.
- Compatibility coverage for `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit\RLEissue.cs`.

### Integration Tests

- `PureTranscoderManager.CanTranscode(ExplicitVRLittleEndian, RLELossless)`.
- `PureTranscoderManager.CanTranscode(RLELossless, ExplicitVRLittleEndian)`.
- Save and reopen RLE DICOM file.
- Render decoded RLE sample through fo-dicom imaging stack when rendering dependencies are available.

## Completion Criteria

RLE is complete when:

- It has no native dependency.
- Encode and decode pass exact byte equality round-trips for all supported test layouts.
- Efferent RLE compatibility tests are represented and pass.
- Invalid RLE streams fail with managed exceptions.
- RLE participates in the single NuGet package through `PureTranscoderManager`.

## Implementation Notes

- Implemented in `fo-dicom.PureCodecs.Rle` with managed header, segment, and frame codecs.
- `DicomRleLosslessCodec` now implements encode and decode directly instead of inheriting the unimplemented stub base.
- Segment coding uses DICOM PackBits-style literal and repeat runs and omits `-128` no-op bytes during encode.
- Frame mapping supports 8-bit monochrome, 16-bit monochrome, RGB interleaved, RGB planar, and multi-frame round-trips.
- Segment order is sample-major with most-significant byte first within each sample.
- The encoder rejects images whose `BitsAllocated / 8 * SamplesPerPixel` requires more than 15 RLE segments.
- Efferent `RLEissue.cs` behavior is covered by a deterministic local regression test for 16-bit signed MONOCHROME2 single-row frames whose low-entropy bytes repeatedly encode and decode through RLE without byte changes.
