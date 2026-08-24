# Phase 1 Known Limitations

This document records compatibility edges that are intentionally outside the phase 1 replacement matrix or are not covered by available external fixtures.

## Outside Phase 1 Scope

- JPEG XL transfer syntaxes are not implemented in phase 1 because `fo-dicom.Codecs` marks them as in development.
- JPEG 2000 Part 2 multi-component and JPIP referenced transfer syntaxes are not registered by `PureTranscoderManager`.
- Native codec fallback is intentionally unsupported. Codec execution is pure managed C# only.

## Explicit Managed Rejections

- Progressive JPEG, arithmetic-coded JPEG, CMYK/YCCK JPEG color spaces, and
  restart interval MCU resynchronization are not implemented. JPEG and JPEG-LS
  DRI/RST structures are recognized and rejected explicitly.
- JPEG Process 1 is limited to 8-bit samples. JPEG Process 2/4 supports 12-bit monochrome in a 16-bit container, but high-bit color remains unsupported.
- JPEG-LS planar normalization is limited to three-component images with `BitsStored <= 8`; planar `YBR_FULL_422` is rejected because the subsampled layout cannot be represented by the supported planar converter.
- JPEG 2000 JP2 wrapper frames are detected and rejected unless a raw J2K codestream is supplied.
- JPEG 2000 decoding supports POC progression changes, RGN Maxshift, and PPM/PPT packed packet headers for classic codestreams. The managed encoder still emits LRCP without ROI or packed packet headers; component subsampling remains unsupported.
- HTJ2K decoding does not currently accept RGN or PPM/PPT semantics.
- `fo-dicom.Codecs 5.16.7` complete-dataset decode of the bundled 12-bit
  multi-frame fixture differs at frame 1 byte 32400 for Pure `.201/.202`
  output. Each identical Pure codestream decodes byte-exact through an
  individual native call, and complete native-to-Pure decoding is byte-exact;
  this is tracked as a reference wrapper limitation rather than a managed
  codestream defect.

## Fixture Availability

- The Efferent acceptance fixture set included in the test support data does not
  include HTJ2K compressed DICOM samples. The process-isolated interoperability
  runner nevertheless exercises HTJ2K `.201`, `.202`, and `.203` against the
  public `fo-dicom.Codecs` 5.16.7 API using the bundled raw DICOM fixtures.
  Completion remains gated on the exact reference and bidirectional rows listed
  in the development checklist.
- The Efferent unit fixture matrix is represented by the available 8-bit and 16-bit raw unit samples. Lossy byte tolerance is applied to 8-bit unit samples only; 16-bit lossy behavior is validated by the smaller raw fixture matrix where byte-level tolerance is meaningful.

## Codec Behavior Notes

- Lossy transfer syntaxes are validated with tolerance checks after decode, not exact byte equality.
- Lossless transfer syntaxes are validated with exact decoded frame equality when no precision-reducing parameter is requested. JPEG Lossless Process 14/14 SV1 with a non-zero point transform follows the JPEG standard and `fo-dicom.Codecs`: discarded low bits decode as zero.
- Classic JPEG 2000 decoding accepts multi-tile codestreams with tile parts grouped by SOT tile index. JPEG-LS decoding accepts standard non-interleaved color codestreams split into one SOS scan per component and applies the effective LSE preset to each scan.
- The process-isolated bidirectional interoperability matrix covers RLE, all
  four JPEG syntaxes, both JPEG-LS syntaxes, classic JPEG 2000 lossless/lossy,
  and all three HTJ2K transfer syntaxes. HTJ2K completion additionally requires
  the frozen exact-reference and invalid-input gates in the development checklist.
- Invalid compressed inputs are expected to throw managed `DicomCodecException` failures and must not require native process isolation.
