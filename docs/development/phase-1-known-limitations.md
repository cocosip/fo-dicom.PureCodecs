# Phase 1 Known Limitations

This document records compatibility edges that are intentionally outside the phase 1 replacement matrix or are not covered by available external fixtures.

## Outside Phase 1 Scope

- JPEG XL transfer syntaxes are not implemented in phase 1 because `fo-dicom.Codecs` marks them as in development.
- JPEG 2000 Part 2 multi-component and JPIP referenced transfer syntaxes are not registered by `PureTranscoderManager`.
- Native codec fallback is intentionally unsupported. Codec execution is pure managed C# only.

## Explicit Managed Rejections

- Progressive JPEG, arithmetic-coded JPEG, CMYK/YCCK JPEG color spaces, and broader restart interval MCU resynchronization are not implemented.
- JPEG Process 1 is limited to 8-bit samples. JPEG Process 2/4 supports 12-bit monochrome in a 16-bit container, but high-bit color remains unsupported.
- JPEG-LS planar normalization is limited to three-component images with `BitsStored <= 8`; planar `YBR_FULL_422` is rejected because the subsampled layout cannot be represented by the supported planar converter.
- JPEG 2000 JP2 wrapper frames are detected and rejected unless a raw J2K codestream is supplied.
- JPEG 2000 packed packet headers, unsupported ROI behavior, unsupported component subsampling, and unsupported progression order combinations fail with managed exceptions.

## Fixture Availability

- The Efferent acceptance fixture set included in the test support data does not include HTJ2K compressed DICOM samples. HTJ2K interoperability is covered by local OpenJPH codestream fixtures and `fo-dicom.Codecs` native decoder compatibility tests.
- The Efferent unit fixture matrix is represented by the available 8-bit and 16-bit raw unit samples. Lossy byte tolerance is applied to 8-bit unit samples only; 16-bit lossy behavior is validated by the smaller raw fixture matrix where byte-level tolerance is meaningful.

## Codec Behavior Notes

- Lossy transfer syntaxes are validated with tolerance checks after decode, not exact byte equality.
- Lossless transfer syntaxes are validated with exact decoded frame equality when no precision-reducing parameter is requested. JPEG Lossless Process 14/14 SV1 with a non-zero point transform follows the JPEG standard and `fo-dicom.Codecs`: discarded low bits decode as zero.
- Classic JPEG 2000 decoding accepts multi-tile codestreams with tile parts grouped by SOT tile index. JPEG-LS decoding accepts standard non-interleaved color codestreams split into one SOS scan per component and applies the effective LSE preset to each scan.
- Invalid compressed inputs are expected to throw managed `DicomCodecException` failures and must not require native process isolation.
