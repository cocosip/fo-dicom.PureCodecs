# Phase 1 Known Limitations

This document records compatibility edges that are intentionally outside the phase 1 replacement matrix or are not covered by available external fixtures.

## Outside Phase 1 Scope

- JPEG XL transfer syntaxes are not implemented in phase 1 because `fo-dicom.Codecs` marks them as in development.
- JPEG 2000 Part 2 multi-component and JPIP referenced transfer syntaxes are not registered by `PureTranscoderManager`.
- Native codec fallback is intentionally unsupported. Codec execution is pure managed C# only.

## Fixture Availability

- The Efferent acceptance fixture set included in the test support data does not include HTJ2K compressed DICOM samples. HTJ2K is validated through raw round-trip, RGB, multi-frame, compression tag, and invalid stream matrix tests.
- The Efferent unit fixture matrix is represented by the available 8-bit and 16-bit raw unit samples. Lossy byte tolerance is applied to 8-bit unit samples only; 16-bit lossy behavior is validated by the smaller raw fixture matrix where byte-level tolerance is meaningful.

## Codec Behavior Notes

- Lossy transfer syntaxes are validated with tolerance checks after decode, not exact byte equality.
- Lossless transfer syntaxes are validated with exact decoded frame equality.
- Invalid compressed inputs are expected to throw managed `DicomCodecException` failures and must not require native process isolation.
