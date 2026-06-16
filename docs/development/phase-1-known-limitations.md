# Phase 1 Known Limitations

This document records compatibility edges that are intentionally outside the phase 1 replacement matrix or are not covered by available external fixtures.

## Outside Phase 1 Scope

- JPEG XL transfer syntaxes are not implemented in phase 1 because `fo-dicom.Codecs` marks them as in development.
- JPEG 2000 Part 2 multi-component and JPIP referenced transfer syntaxes are not registered by `PureTranscoderManager`.
- Native codec fallback is intentionally unsupported. Codec execution is pure managed C# only.

## Explicit Managed Rejections

- JPEG Process 2/4 12-bit sequential DCT codestream encode/decode is not implemented by the current managed JPEG path. The test suite records this as an explicit managed exception until fixture-backed support is added.
- Progressive JPEG, arithmetic-coded JPEG, CMYK/YCCK JPEG color spaces, and broader restart interval MCU resynchronization are not implemented.
- JPEG 2000 JP2 wrapper frames are detected and rejected unless a raw J2K codestream is supplied.
- JPEG 2000 packed packet headers, unsupported ROI behavior, unsupported component subsampling, and unsupported progression order combinations fail with managed exceptions.

## Fixture Availability

- The Efferent acceptance fixture set included in the test support data does not include HTJ2K compressed DICOM samples. HTJ2K interoperability is covered by local OpenJPH codestream fixtures and `fo-dicom.Codecs` native decoder compatibility tests.
- The Efferent unit fixture matrix is represented by the available 8-bit and 16-bit raw unit samples. Lossy byte tolerance is applied to 8-bit unit samples only; 16-bit lossy behavior is validated by the smaller raw fixture matrix where byte-level tolerance is meaningful.

## Codec Behavior Notes

- Lossy transfer syntaxes are validated with tolerance checks after decode, not exact byte equality.
- Lossless transfer syntaxes are validated with exact decoded frame equality.
- Invalid compressed inputs are expected to throw managed `DicomCodecException` failures and must not require native process isolation.
