# fo-dicom.PureCodecs Development Checklist

> This is the project tracking document. Complete work by checking items off in this file. Keep the checklist current as development progresses.

## Goal

Build a pure C# `netstandard2.0` codec package that fully replaces the completed codec support in `fo-dicom.Codecs`.

## Tracking Rules

- Check an item only after code and tests for that item are complete.
- If an item changes scope, update the item text before checking it.
- Add child checklist items when a task is too large to finish in one focused pass.
- Keep implementation aligned with the design documents in `docs/design`.
- Do not add native codec dependencies, P/Invoke, or production target frameworks other than `netstandard2.0`.

## Design References

- [Overall design](../design/fo-dicom-pure-codecs-design.md)
- [Codec entry design](../design/codec-entry-design.md)
- [RLE design](../design/rle-codec-design.md)
- [JPEG design](../design/jpeg-codec-design.md)
- [JPEG-LS design](../design/jpegls-codec-design.md)
- [JPEG 2000 and HTJ2K design](../design/jpeg2000-codec-design.md)

## 0. Repository Foundation

- [x] Create `fo-dicom.PureCodecs.slnx`.
- [x] Create `src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj`.
- [x] Create `src/fo-dicom.PureCodecs.Rle/fo-dicom.PureCodecs.Rle.csproj`.
- [x] Create `src/fo-dicom.PureCodecs.Jpeg/fo-dicom.PureCodecs.Jpeg.csproj`.
- [x] Create `src/fo-dicom.PureCodecs.JpegLs/fo-dicom.PureCodecs.JpegLs.csproj`.
- [x] Create `src/fo-dicom.PureCodecs.Jpeg2000/fo-dicom.PureCodecs.Jpeg2000.csproj`.
- [x] Ensure all production projects use only `<TargetFramework>netstandard2.0</TargetFramework>`.
- [x] Add fo-dicom package reference to production projects.
- [x] Enable NuGet Central Package Management with `Directory.Packages.props`.
- [x] Move package versions out of project files and into `Directory.Packages.props`.
- [x] Add project references from `fo-dicom.PureCodecs` to all codec-family projects.
- [x] Create test project `tests/fo-dicom.PureCodecs.Tests/fo-dicom.PureCodecs.Tests.csproj`.
- [x] Add test project to the solution.
- [x] Create initial package metadata for one NuGet package.
- [x] Configure package output to include all codec-family DLLs under `lib/netstandard2.0`.
- [x] Add root README with minimal usage example.
- [x] Run `dotnet build` and confirm the empty solution builds.

## 1. Codec Entry Layer

- [x] Create `PureTranscoderManager`.
- [x] Make `PureTranscoderManager` inherit fo-dicom `TranscoderManager`.
- [x] Implement explicit `LoadCodecs` registration.
- [x] Add private `AddCodec(IDicomCodec codec)` helper.
- [x] Create shared codec exception wrapper helper.
- [x] Create shared pixel metadata snapshot helper.
- [x] Create shared frame validation helper.
- [x] Create shared `IByteBuffer` conversion helper.
- [x] Create stub codec base for unimplemented algorithms.
- [x] Add stub codec for RLE Lossless.
- [x] Add stub codec for JPEG Process 1.
- [x] Add stub codec for JPEG Process 2/4.
- [x] Add stub codec for JPEG Lossless Process 14.
- [x] Add stub codec for JPEG Lossless Process 14 SV1.
- [x] Add stub codec for JPEG-LS Lossless.
- [x] Add stub codec for JPEG-LS Near-Lossless.
- [x] Add stub codec for JPEG 2000 Lossless.
- [x] Add stub codec for JPEG 2000 Lossy.
- [x] Add stub codec for HTJ2K Lossless.
- [x] Add stub codec for HTJ2K Lossless RPCL.
- [x] Add stub codec for HTJ2K Lossy.
- [x] Test `PureTranscoderManager` construction.
- [x] Test `HasCodec` for all phase 1 transfer syntaxes.
- [x] Test `GetCodec` returns a codec for all phase 1 transfer syntaxes.
- [x] Test `CanTranscode(ExplicitVRLittleEndian, syntax)` for all phase 1 transfer syntaxes.
- [x] Test `CanTranscode(syntax, ExplicitVRLittleEndian)` for all phase 1 transfer syntaxes.
- [x] Test stub encode/decode throws `DicomCodecException`.
- [x] Update entry design doc if implementation changes any public class names.

## 2. Test Baseline and Fixtures

- [x] Copy or reference Efferent unit test fixtures from `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit`.
- [x] Copy or reference Efferent acceptance fixtures from `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance`.
- [x] Create helper for building raw 8-bit monochrome datasets.
- [x] Create helper for building raw 16-bit monochrome datasets.
- [x] Create helper for building RGB interleaved datasets.
- [x] Create helper for building RGB planar datasets.
- [x] Create helper for building multi-frame datasets.
- [x] Create exact byte equality assertion for lossless round-trips.
- [x] Create tolerance assertion for lossy round-trips.
- [x] Create frame count preservation assertion.
- [x] Create required compression tag assertion.
- [x] Create managed exception assertion for invalid streams.
- [x] Add acceptance matrix skeleton for raw -> compressed.
- [x] Add acceptance matrix skeleton for compressed -> raw.
- [x] Add acceptance matrix skeleton for compressed render tests where rendering dependencies are available.
- [x] Document any fixture that cannot be redistributed in this repo.

## 3. RLE Lossless

### 3.1 RLE Parser and Writer

- [x] Add RLE header model.
- [x] Test parsing a valid 64-byte RLE header.
- [x] Test rejecting a frame shorter than 64 bytes.
- [x] Test rejecting segment count less than 1.
- [x] Test rejecting segment count greater than 15.
- [x] Test rejecting non-increasing segment offsets.
- [x] Implement RLE header parser.
- [x] Implement RLE header writer.
- [x] Test writing segment count and offsets in little-endian order.

### 3.2 RLE Decoder

- [x] Test decoding a literal run.
- [x] Test decoding a repeat run.
- [x] Test decoding mixed literal and repeat runs.
- [x] Test rejecting literal run that exceeds input.
- [x] Test rejecting repeat run that exceeds output.
- [x] Implement segment decoder.
- [x] Implement frame decoder for 8-bit monochrome.
- [x] Implement frame decoder for 16-bit monochrome.
- [x] Implement frame decoder for RGB interleaved.
- [x] Implement frame decoder for RGB planar.
- [x] Wire decoder into `DicomRleLosslessCodec.Decode`.

### 3.3 RLE Encoder

- [x] Test encoding a literal run.
- [x] Test encoding a repeat run.
- [x] Test encoding mixed literal and repeat runs.
- [x] Test encoder does not emit unsupported segment counts.
- [x] Implement segment encoder.
- [x] Implement frame encoder for 8-bit monochrome.
- [x] Implement frame encoder for 16-bit monochrome.
- [x] Implement frame encoder for RGB interleaved.
- [x] Implement frame encoder for RGB planar.
- [x] Wire encoder into `DicomRleLosslessCodec.Encode`.

### 3.4 RLE Verification

- [x] Test 8-bit raw -> RLE -> raw exact round-trip.
- [x] Test 16-bit raw -> RLE -> raw exact round-trip.
- [x] Test RGB interleaved raw -> RLE -> raw exact round-trip.
- [x] Test RGB planar raw -> RLE -> raw exact round-trip.
- [x] Test multi-frame raw -> RLE -> raw exact round-trip.
- [x] Port Efferent `RLEissue.cs` behavior into local tests.
- [x] Test save and reopen RLE DICOM file.
- [x] Mark RLE stub complete and remove stub-only failure expectations.
- [x] Update [RLE design](../design/rle-codec-design.md) with implementation notes.

## 4. JPEG Family

### 4.1 JPEG Common Infrastructure

- [x] Add JPEG marker constants.
- [x] Add JPEG marker reader.
- [x] Add JPEG marker writer.
- [x] Test SOI and EOI parsing.
- [x] Test SOF0 parsing.
- [x] Test SOF1 parsing.
- [x] Test SOF3 parsing.
- [x] Test DHT parsing.
- [x] Test DQT parsing.
- [x] Test DRI parsing.
- [x] Test SOS parsing.
- [x] Test APPn and COM skipping.
- [x] Test invalid marker length failure.
- [x] Add entropy bit reader.
- [x] Add entropy bit writer.
- [x] Add Huffman table builder.
- [x] Test Huffman decode table construction.
- [x] Test Huffman encode table construction.
- [x] Add restart marker handling.

### 4.2 JPEG Lossless Core

- [x] Add lossless predictor functions.
- [x] Test predictor 1.
- [x] Test predictors 2 through 7 when supported.
- [x] Add lossless scan decoder.
- [x] Add lossless scan encoder.
- [x] Test 8-bit lossless scan exact round-trip.
- [x] Test 12-bit lossless scan exact round-trip.
- [x] Test 16-bit lossless scan exact round-trip.
- [x] Wire Process 14 decode.
- [x] Wire Process 14 encode.
- [x] Wire Process 14 SV1 decode.
- [x] Wire Process 14 SV1 encode.

### 4.3 JPEG DCT Core

- [x] Add block model for 8x8 DCT data.
- [x] Add quantization table model.
- [x] Add forward DCT implementation.
- [x] Add inverse DCT implementation.
- [x] Add zigzag order helper.
- [x] Test DCT inverse tolerance on known block.
- [x] Add baseline sequential decoder.
- [x] Add baseline sequential encoder.
- [x] Add extended sequential decoder.
- [x] Add extended sequential encoder.
- [x] Wire Process 1 decode.
- [x] Wire Process 1 encode.
- [x] Wire Process 2/4 decode.
- [x] Wire Process 2/4 encode.

### 4.4 JPEG DICOM Integration

- [x] Implement JPEG codec parameter type.
- [x] Preserve fo-dicom default color conversion behavior for Process 1 and Process 2/4.
- [x] Add `YBR_FULL` to RGB conversion path.
- [x] Add `YBR_FULL_422` to RGB conversion path.
- [x] Add planar to interleaved conversion path where required.
- [x] Add unsupported photometric interpretation failures.
- [x] Test Process 1 8-bit lossy round-trip with tolerance.
- [x] Test Process 2/4 8-bit lossy round-trip with tolerance.
- [x] Test Process 2/4 12-bit monochrome lossy round-trip with tolerance and native decoder interoperability.
- [x] Test Process 14 exact round-trip for 8-bit data.
- [x] Test Process 14 exact round-trip for 12-bit data.
- [x] Test Process 14 exact round-trip for 16-bit data.
- [x] Test Process 14 SV1 exact round-trip for 8-bit data.
- [x] Test Process 14 SV1 exact round-trip for 12-bit data.
- [x] Test Process 14 SV1 exact round-trip for 16-bit data.
- [x] Test available Efferent JPEG baseline YBRFull/YBR422 acceptance decode samples.
- [x] Mark JPEG stubs complete and remove stub-only failure expectations.
- [x] Update [JPEG design](../design/jpeg-codec-design.md) with implementation notes.

## 5. JPEG-LS Family

### 5.1 JPEG-LS Common Infrastructure

- [x] Add JPEG-LS marker constants.
- [x] Add JPEG-LS marker reader.
- [x] Add JPEG-LS marker writer.
- [x] Test SOI and EOI parsing.
- [x] Test SOF55 parsing.
- [x] Test SOS parsing.
- [x] Test LSE preset coding parameter parsing.
- [x] Test APPn and COM skipping.
- [x] Test invalid marker length failure.
- [x] Add JPEG-LS frame info model.
- [x] Add JPEG-LS preset coding parameter model.

### 5.2 JPEG-LS Coding Core

- [x] Add context model.
- [x] Add Golomb code reader.
- [x] Add Golomb code writer.
- [x] Test Golomb encode/decode.
- [x] Add regular mode decoder.
- [x] Add regular mode encoder.
- [x] Add run mode decoder.
- [x] Add run mode encoder.
- [x] Test regular mode sample reconstruction.
- [x] Test run mode sample reconstruction.
- [x] Add near-lossless sample clamping logic.
- [x] Test near-lossless tolerance helper.

### 5.3 JPEG-LS DICOM Integration

- [x] Implement JPEG-LS codec parameter type.
- [x] Implement interleave mode mapping.
- [x] Implement lossless decode.
- [x] Implement lossless encode.
- [x] Implement near-lossless decode.
- [x] Implement near-lossless encode.
- [x] Add unsupported interleave failures.
- [x] Add unsupported photometric interpretation failures.
- [x] Test lossless 8-bit exact round-trip.
- [x] Test lossless 16-bit exact round-trip.
- [x] Test lossless RGB exact round-trip where supported.
- [x] Test near-lossless 8-bit tolerance round-trip.
- [x] Test near-lossless 16-bit tolerance round-trip.
- [x] Test multi-frame JPEG-LS data.
- [x] Test Efferent JPEG-LS acceptance samples.
- [x] Mark JPEG-LS stubs complete and remove stub-only failure expectations.
- [x] Update [JPEG-LS design](../design/jpegls-codec-design.md) with implementation notes.

## 6. JPEG 2000 and HTJ2K Family

### 6.1 Codestream Infrastructure

- [x] Consolidate JPEG 2000 family shared infrastructure before further `.90`, `.91`, or HTJ2K repairs.
- [x] Ensure shared codestream marker, marker payload, byte I/O, metadata validation, DWT, and quantization helpers are used by both classic JPEG 2000 and HTJ2K where the standard syntax is the same.
- [x] Keep only entropy/block-coding implementations split between classic Tier-1 EBCOT/MQ and HTJ2K Part 15 HT block coding.
- [x] Remove production implementation type names that imply native/reference-library dependency, such as `OpenJpeg*` or `OpenJph*`, unless the file is a test/reference-vector adapter.
- [x] Add JPEG 2000 marker constants.
- [x] Add codestream marker reader.
- [x] Add codestream marker writer.
- [x] Test SOC parsing.
- [x] Test SIZ parsing.
- [x] Test COD parsing.
- [x] Test COC parsing and component-level COD override inheritance.
- [x] Test QCD parsing.
- [x] Test QCC parsing and component-level QCD override inheritance.
- [x] Test POC parsing and progression order change validation.
- [x] Test RGN parsing and explicitly document unsupported ROI behavior.
- [x] Test COM parsing and safe preservation or rejection behavior.
- [x] Test SOT parsing.
- [x] Test SOD parsing.
- [x] Test EOC parsing.
- [x] Test PLT parsing or explicit managed rejection.
- [x] Test PPM/PPT parsing or explicit managed rejection.
- [x] Test SOP/EPH parsing or explicit managed rejection.
- [x] Detect raw J2K codestream frames.
- [x] Detect JP2 wrapper frames and fail explicitly unless supported.
- [x] Test invalid marker length failure.
- [x] Test multi-tile codestream geometry.
- [x] Test multi-tile-part `Psot`, `TPsot`, and `TNsot` validation.
- [x] Add image model.
- [x] Add tile model.
- [x] Add component model.
- [x] Add precinct model.
- [x] Add code-block model.
- [x] Add packet model.
- [x] Add progression order iterator.
- [x] Test LRCP progression when supported.
- [x] Test RLCP progression when supported.
- [x] Test RPCL progression for HTJ2K Lossless RPCL.
- [x] Test PCRL progression when supported.
- [x] Test CPRL progression when supported.

### 6.2 Transform and Quantization

- [x] Add DC level shift for unsigned and signed samples.
- [x] Test JPEG 2000 `Ssiz` precision and sign mapping to DICOM pixel metadata.
- [x] Test `BitsAllocated`, `BitsStored`, and `PixelRepresentation` validation.
- [x] Add reversible color transform.
- [x] Add irreversible color transform.
- [x] Test `AllowMCT` enables and disables RCT/ICT for RGB data.
- [x] Add reversible wavelet transform.
- [x] Add inverse reversible wavelet transform.
- [x] Add irreversible wavelet transform.
- [x] Add inverse irreversible wavelet transform.
- [x] Test reversible transform exact round-trip.
- [x] Test irreversible transform tolerance round-trip.
- [x] Add guard-bit and effective bit-depth calculation for wavelet coefficients.
- [x] Test zero-bit-plane calculation across decomposition levels.
- [x] Add quantization model.
- [x] Add inverse quantization.
- [x] Test no-quantization path for lossless 5/3 coding.
- [x] Test scalar-derived quantization parsing.
- [x] Test scalar-expounded quantization parsing.
- [x] Test explicit lossy subband quantization steps.

### 6.3 Classic JPEG 2000 Coding

- [x] Add MQ arithmetic decoder state table.
- [x] Add MQ arithmetic encoder state table.
- [x] Test MQ byte-stuffing and marker-safe bitstream handling.
- [x] Add Tier-1 significance propagation pass.
- [x] Add Tier-1 magnitude refinement pass.
- [x] Add Tier-1 cleanup pass.
- [x] Test Tier-1 pass termination and pass-length accounting.
- [x] Add classic JPEG 2000 code-block decoder.
- [x] Add classic JPEG 2000 code-block encoder.
- [x] Add tag-tree decoder.
- [x] Add tag-tree encoder.
- [x] Add packet decoder.
- [x] Add packet encoder.
- [x] Test empty packet handling.
- [x] Test basic multi-layer packet contribution handling.
- [x] Align multi-layer packet contribution handling with OpenJPEG's PCRD
  quality-layer model, including early layer contribution distribution.
- [x] Add OpenJPEG-style cumulative Tier-1 pass `distortiondec` accounting.
- [x] Add rate-distortion pass model based on OpenJPEG pass `rate` and
  `distortiondec`, not heuristic pass weights.
- [x] Add PCRD-style layer allocation matching `opj_tcd_makelayer` and
  `opj_tcd_rateallocate`.
- [x] Test `Rate`, `RateLevels`, `TargetRatio`, and `NumLayers` parameter effects.
- [x] Ensure classic JPEG 2000 multi-layer encoding writes real packet contributions before the final layer, not only a COD layer count with empty early layers.
- [x] Compare classic JPEG 2000 quality-layer packet distribution against `fo-dicom.Codecs`/OpenJPEG for `D:\1.dcm` and the RGB unit8 fixture.
- [ ] Add a layer-truncated decode fixture proving early quality layers are independently decodable at lower quality.
- [x] Test optional final lossless layer behavior for lossless rate-controlled encoding.
- [x] Implement JPEG 2000 Lossless decode.
- [x] Implement JPEG 2000 Lossless encode.
- [x] Implement JPEG 2000 Lossy decode.
- [x] Implement JPEG 2000 Lossy encode.
- [x] Test JPEG 2000 Lossless 8-bit exact round-trip.
- [x] Test JPEG 2000 Lossless 16-bit exact round-trip.
- [x] Test JPEG 2000 Lossy tolerance round-trip.
- [x] Test Efferent JPEG 2000 acceptance samples.

### 6.4 HTJ2K Coding

- [x] Add MEL decoder.
- [x] Add MEL encoder.
- [x] Test MEL state-machine vectors.
- [x] Add HT VLC decoder with Annex C table validation.
- [x] Add HT VLC encoder with Annex C table validation.
- [x] Add MagSgn decoder.
- [x] Add MagSgn encoder.
- [x] Test HT three-segment code-block assembly and disassembly.
- [x] Add HT cleanup pass decoder.
- [x] Add HT cleanup pass encoder.
- [x] Test HT quad and quad-pair scanning behavior.
- [x] Add HT block decoder.
- [x] Add HT block encoder.
- [x] Cross-check HT block vectors against OpenJPH or OpenJPEG reference output.
- [x] Implement standard HTJ2K Lossless decode compatible with `fo-dicom.Codecs`/OpenJPH output.
- [x] Implement standard HTJ2K Lossless encode compatible with `fo-dicom.Codecs`/OpenJPH decoders.
- [x] Implement standard HTJ2K Lossless RPCL decode compatible with `fo-dicom.Codecs`/OpenJPH output.
- [x] Implement standard HTJ2K Lossless RPCL encode compatible with `fo-dicom.Codecs`/OpenJPH decoders.
- [x] Implement standard HTJ2K Lossy decode compatible with `fo-dicom.Codecs`/OpenJPH output.
- [x] Implement standard HTJ2K Lossy encode compatible with `fo-dicom.Codecs`/OpenJPH decoders.
- [x] Test HTJ2K Lossless exact round-trip.
- [x] Test HTJ2K Lossless RPCL exact round-trip.
- [x] Test HTJ2K Lossless RPCL codestream uses RPCL progression.
- [x] Test HTJ2K Lossy tolerance round-trip.
- [x] Add or import HTJ2K fixtures for acceptance tests.
- [x] Document any HTJ2K reference-library mismatch before marking support complete.
- [x] Enable public HTJ2K tool/manager output after standard codestream compatibility tests pass.

### 6.5 JPEG 2000 DICOM Integration

- [x] Implement JPEG 2000 codec parameter type.
- [x] Match `DicomJpeg2000Params.Irreversible` behavior.
- [x] Match `DicomJpeg2000Params.Rate` behavior.
- [x] Match `DicomJpeg2000Params.RateLevels` behavior.
- [x] Match `DicomJpeg2000Params.ProgressionOrder` behavior.
- [x] Match `DicomJpeg2000Params.AllowMCT` behavior.
- [x] Match `DicomJpeg2000Params.UpdatePhotometricInterpretation` behavior.
- [x] Match `DicomJpeg2000Params.EncodeSignedPixelValuesAsUnsigned` behavior.
- [x] Implement HTJ2K codec parameter type.
- [x] Match `DicomHtJpeg2000Params.ProgressionOrder` behavior.
- [x] Implement DICOM component layout mapping.
- [x] Implement planar and interleaved RGB input normalization.
- [x] Implement decoded output repacking to fo-dicom raw frame layout.
- [x] Implement monochrome output path.
- [x] Implement RGB output path.
- [x] Implement YBR-related output path where supported.
- [x] Add explicit Phase 1 exclusion for JPEG 2000 Part 2 Multi-component transfer syntaxes `.92` and `.93`.
- [x] Add explicit unsupported JPIP/JPT behavior.
- [x] Add unsupported component subsampling failures.
- [x] Add unsupported progression order failures.
- [x] Add unsupported photometric interpretation failures.
- [x] Test multi-frame JPEG 2000 data.
- [x] Test JPEG 2000 lossless preserves frame count and required compression tags.
- [x] Test JPEG 2000 lossy preserves frame count and required compression tags.
- [x] Test HTJ2K preserves frame count and required compression tags.
- [x] Test invalid codestream managed exceptions.
- [x] Test Efferent JPEG 2000 acceptance inverse transcode samples.
- [x] Test Efferent JPEG 2000 render samples where rendering dependencies are available.
- [x] Mark JPEG 2000 stubs complete and remove stub-only failure expectations.
- [x] Update [JPEG 2000 design](../design/jpeg2000-codec-design.md) with implementation notes.

## 7. Full Compatibility Matrix

- [x] Run `CanTranscode` matrix for all phase 1 transfer syntaxes.
- [x] Run raw 8-bit -> each codec -> raw.
- [x] Run raw 16-bit -> each supported codec -> raw.
- [x] Run RGB -> each supported codec -> raw.
- [x] Run multi-frame -> each supported codec -> raw.
- [x] Run Efferent unit compatibility tests.
- [x] Run Efferent acceptance transcode tests.
- [x] Run Efferent acceptance inverse transcode tests.
- [x] Run render tests where rendering dependencies are available.
- [x] Compare lossless outputs with exact byte equality after decode.
- [x] Compare lossy outputs with agreed tolerance after decode.
- [x] Verify invalid streams throw managed exceptions.
- [x] Document unsupported edge cases before release.

## 8. Packaging and Consumer Validation

- [x] Pack one `fo-dicom.PureCodecs` NuGet package.
- [x] Confirm package contains all codec-family DLLs under `lib/netstandard2.0`.
- [x] Confirm package does not contain native codec DLLs.
- [x] Create .NET Framework 4.7.2 consumer smoke test.
- [x] Register only `PureTranscoderManager` in .NET Framework smoke test.
- [x] Decode at least one compressed sample in .NET Framework smoke test.
- [x] Create modern .NET consumer smoke test.
- [x] Register only `PureTranscoderManager` in modern .NET smoke test.
- [x] Decode at least one compressed sample in modern .NET smoke test.
- [x] Verify package install does not require per-family registration.
- [x] Verify package install does not require native runtime dependencies.

## 9. Documentation and Release Readiness

- [x] Update README usage instructions.
- [x] Document phase 1 supported transfer syntaxes.
- [x] Document package assembly layout.
- [x] Document managed error behavior.
- [x] Document known limitations.
- [x] Document compatibility with `fo-dicom.Codecs`.
- [x] Update design docs with final implementation notes.
- [x] Update this checklist so completed items are checked.
- [x] Prepare release notes for first alpha package.

## 10. Real Tool Compression Regression Repairs

Tracked batch: [tool compression regression](tool-compression-regression-log.md).

- [x] Reproduce `fo-dicom.PureCodecs.Tools` output from a real DICOM input.
- [x] Preserve or regenerate the matching `fo-dicom.Codecs` reference output for the same input.
- [x] Add a fixture-backed regression harness that compares PureCodecs output against the `fo-dicom.Codecs` baseline for every available reference format.
- [x] RLE Lossless: explain the output file-size difference or fix the underlying DICOM/tag/encapsulation discrepancy.
- [x] JPEG Lossless Process 14: fix viewer-open/render compatibility.
- [x] JPEG Lossless Process 14 SV1: fix viewer-open/render compatibility.
- [x] JPEG-LS Lossless: fix viewer-open/render compatibility.
- [x] JPEG-LS Near-Lossless: fix viewer-open/render compatibility.
- [x] JPEG 2000 Lossless: fix viewer-open/render compatibility and the large output-size mismatch.
- [x] JPEG 2000 Lossy: fix viewer-open/render compatibility and the large output-size mismatch.
- [x] HTJ2K Lossless: create or obtain a reference baseline.
- [x] HTJ2K Lossless: replace the project-managed payload with standard HTJ2K codestream output and fix viewer-open/render compatibility.
- [x] HTJ2K Lossless RPCL: create or obtain a reference baseline, then fix viewer-open/render compatibility.
- [x] HTJ2K Lossy: create or obtain a reference baseline.
- [x] HTJ2K Lossy: replace the project-managed payload with standard HTJ2K codestream output and fix viewer-open/render compatibility.
- [x] Re-run the full solution test suite after the format-specific repairs.

JPEG 2000 regression note: `fo-dicom.Codecs` 5.16.5.1/OpenJPEG is the
compatibility baseline for classic `.90` and `.91`. Do not treat the local
`D:\1_transcoded\1_j2k_lossy.dcm` file as that baseline unless its provenance
is revalidated. The current known native baselines generated from `D:\1.dcm`
are `artifacts\fo-dicom-codecs-baseline\fo_dicom_codecs_j2k_lossless.dcm` with
a 173228-byte codestream frame, and
`artifacts\fo-dicom-codecs-baseline\fo_dicom_codecs_j2k_lossy.dcm` with a
40774-byte codestream frame and OpenJPEG QCD payload
`42 B7 20 B6 F0 B6 F0 B6 C0 AF 00 AF 00 AE E0 A7 50 A7 50 A7 68 90 05 90 05 90 47 97 D3 97 D3 97 62`.
Pure C# classic JPEG 2000 output must target those baseline codestream frame
sizes for this fixture; lossy tolerance is necessary for decoded pixels, not a
license to drift from OpenJPEG packet/layer behavior.

Current classic JPEG 2000 output generated by
`artifacts\tool-regression-current-20260616-openjpeg-aligned` is byte-for-byte
equal to those OpenJPEG baseline files:

- JPEG 2000 Lossless SHA-256:
  `E07E0A745C50C5243A3F68A013F3FA82BDEFECCB6DD26589BA63EB0EEACE65F3`.
- JPEG 2000 Lossy SHA-256:
  `1E62426443B734D9C6A6205F98EFEA3B4F4C795DDE2173787B5BB60D885047FA`.

Classic JPEG 2000 lossy repair note: OpenJPEG derives each irreversible band
bit-plane depth from the encoded QCD step-size exponent plus guard bits
(`band->numbps`), not from component precision and subband gain alone. Keep the
managed Tier-1 encoder and decoder on the same QCD-exponent-derived calculation;
otherwise signed 16-bit lossy fixtures can decode with a large DC-scale offset
even when `Rate = 0` disables packet truncation.

Classic JPEG 2000 irreversible transform note: OpenJPEG's lossy forward 9/7 DWT
and Tier-1 quantization path uses `OPJ_FLOAT32` coefficients and
`opj_lrintf((tiledp_f / band->stepsize) * 64)`. Do not replace that with a
`double` forward path; it can preserve coarse layers while drifting at lower
bit-planes, changing multi-layer packet contribution sizes.

Classic JPEG 2000 multi-layer repair note: writing `COD.Layers > 1` is not
enough. OpenJPEG compatibility requires actual packet contributions to be
distributed across quality layers according to `Rate` and `RateLevels`; early
layers must not all be empty packets with the full code-block contribution saved
for the final layer. Any future "J2K aligned" claim must include a test that
detects non-empty early-layer packet contribution and compares packet/layer
distribution against `fo-dicom.Codecs`/OpenJPEG. The current regression coverage
does this for `D:\1.dcm` lossless/lossy and the RGB unit8 lossy fixture; a
separate layer-truncated decode fixture remains required before broad
multi-layer acceptance is complete.

Classic JPEG 2000 Tier-1 repair note: PCRD must be driven from cumulative pass
length and cumulative `distortiondec`. The managed Tier-1 encoder now follows
OpenJPEG's `nmsedec` LUT behavior, including the `bitPlane == 0` significance
and refinement formulas from `t1_generate_luts.c`; otherwise RGB lossy packet
allocation can drift even when decoded pixels remain within tolerance.

Classic JPEG 2000 Tier-2 packet note: OpenJPEG 2.5.4 default builds do not use
`ENABLE_EMPTY_PACKET_OPTIMIZATION`, so a quality-layer packet with no new
code-block contribution still writes the packet-present bit and tag-tree header
state. Do not replace those packets with the optional single-byte `00`
empty-packet shortcut.

Classic JPEG 2000 DICOM padding note: when the EOC-terminated logical
codestream length is odd, the DICOM encapsulated item must include a trailing
`00` padding byte after EOC. This byte is outside the logical JPEG 2000
codestream and must not be included in SOT `Psot`.

Classic JPEG 2000 OpenJPEG alignment note: do not drive repairs from final
binary size first. The required order is DICOM sample mapping, 5/3 or 9/7 DWT,
QCD/step-size generation, Tier-1 pass `rate` and `distortiondec`, PCRD
threshold allocation, Tier-2 packet writing, and only then final codestream size
or binary comparison. Final size differences are compatibility signals, not the
root-cause model.

## Performance Benchmark Baseline

- [x] Add a standalone `benchmarks/fo-dicom.PureCodecs.Benchmarks` project.
- [x] Keep BenchmarkDotNet and benchmark validation out of the xUnit project-reference graph.
- [x] Add `--verify` to validate the six bundled benchmark fixtures with PureCodecs before timing.
- [x] Measure codec-boundary and `DicomTranscoder` encode/decode operations with allocation diagnostics.
- [x] Capture a complete short-run baseline for RLE, JPEG Baseline, JPEG Lossless SV1, JPEG-LS, JPEG 2000 Lossless, and JPEG 2000 Lossy.
- [x] Use the isolated C# baseline to select the first optimization hotspot; Go comparison is out of scope.

### JPEG Baseline Decode Optimization

- [x] Identify repeated inverse-DCT cosine evaluation as the first measured JPEG Baseline decode hotspot.
- [x] Cache the 64 coordinate/frequency cosine values while preserving the existing coefficient, rounding, color, and DICOM paths.
- [x] Add fixed DC/AC inverse-DCT sample coverage and run the Native JPEG integration checks.
- [x] Verify all six benchmark fixtures and the complete unit suite independently from BenchmarkDotNet.
- [x] Reduce the isolated `CodecDecode` JPEG Baseline short-run mean from 564.8 ms to 112.8 ms on .NET 10.0.8.

The ShortRun result is a directional comparison only; use a longer run before
claiming a precise performance regression threshold.

Run a bounded local baseline with:

```powershell
dotnet run -c Release --project benchmarks/fo-dicom.PureCodecs.Benchmarks -- --verify
dotnet run -c Release --project benchmarks/fo-dicom.PureCodecs.Benchmarks -- --job short
```

Generated `BenchmarkDotNet.Artifacts` remain machine-specific and are not committed.

## Completion Definition

The first replacement phase is complete only when:

- [x] All phase 1 transfer syntaxes are registered.
- [x] All phase 1 transfer syntaxes support encode and decode.
- [x] No production project targets anything except `netstandard2.0`.
- [x] No codec path uses native DLLs or P/Invoke.
- [x] One NuGet package contains all required DLLs.
- [x] Lossless round-trips pass exact byte equality checks.
- [x] Lossy round-trips pass agreed tolerance checks.
- [x] Compatibility tests based on `fo-dicom.Codecs` pass.
- [x] Consumer smoke tests pass on .NET Framework 4.7.2+ and modern .NET.
- [x] Documentation reflects the implemented behavior.
