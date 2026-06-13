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
- [x] Test Process 2/4 12-bit unsupported path with managed exception until fixture-backed support exists.
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
- [x] Test multi-layer packet contribution handling.
- [x] Add rate-distortion pass model for lossy encoding.
- [x] Add PCRD-style layer allocation.
- [x] Test `Rate`, `RateLevels`, `TargetRatio`, and `NumLayers` parameter effects.
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
- [x] Implement HTJ2K Lossless decode.
- [x] Implement HTJ2K Lossless encode.
- [x] Implement HTJ2K Lossless RPCL decode.
- [x] Implement HTJ2K Lossless RPCL encode.
- [x] Implement HTJ2K Lossy decode.
- [x] Implement HTJ2K Lossy encode.
- [x] Test HTJ2K Lossless exact round-trip.
- [x] Test HTJ2K Lossless RPCL exact round-trip.
- [x] Test HTJ2K Lossless RPCL codestream uses RPCL progression.
- [x] Test HTJ2K Lossy tolerance round-trip.
- [x] Add or import HTJ2K fixtures for acceptance tests.
- [x] Document any HTJ2K reference-library mismatch before marking support complete.

### 6.5 JPEG 2000 DICOM Integration

- [ ] Implement JPEG 2000 codec parameter type.
- [ ] Match `DicomJpeg2000Params.Irreversible` behavior.
- [ ] Match `DicomJpeg2000Params.Rate` behavior.
- [ ] Match `DicomJpeg2000Params.RateLevels` behavior.
- [ ] Match `DicomJpeg2000Params.ProgressionOrder` behavior.
- [ ] Match `DicomJpeg2000Params.AllowMCT` behavior.
- [ ] Match `DicomJpeg2000Params.UpdatePhotometricInterpretation` behavior.
- [ ] Match `DicomJpeg2000Params.EncodeSignedPixelValuesAsUnsigned` behavior.
- [x] Implement HTJ2K codec parameter type.
- [ ] Match `DicomHtJpeg2000Params.ProgressionOrder` behavior.
- [ ] Implement DICOM component layout mapping.
- [ ] Implement planar and interleaved RGB input normalization.
- [ ] Implement decoded output repacking to fo-dicom raw frame layout.
- [ ] Implement monochrome output path.
- [ ] Implement RGB output path.
- [ ] Implement YBR-related output path where supported.
- [ ] Add explicit Phase 1 exclusion for JPEG 2000 Part 2 Multi-component transfer syntaxes `.92` and `.93`.
- [ ] Add explicit unsupported JPIP/JPT behavior.
- [ ] Add unsupported component subsampling failures.
- [ ] Add unsupported progression order failures.
- [ ] Add unsupported photometric interpretation failures.
- [ ] Test multi-frame JPEG 2000 data.
- [ ] Test JPEG 2000 lossless preserves frame count and required compression tags.
- [ ] Test JPEG 2000 lossy preserves frame count and required compression tags.
- [ ] Test HTJ2K preserves frame count and required compression tags.
- [ ] Test invalid codestream managed exceptions.
- [ ] Test Efferent JPEG 2000 acceptance inverse transcode samples.
- [ ] Test Efferent JPEG 2000 render samples where rendering dependencies are available.
- [ ] Mark JPEG 2000 stubs complete and remove stub-only failure expectations.
- [ ] Update [JPEG 2000 design](../design/jpeg2000-codec-design.md) with implementation notes.

## 7. Full Compatibility Matrix

- [ ] Run `CanTranscode` matrix for all phase 1 transfer syntaxes.
- [ ] Run raw 8-bit -> each codec -> raw.
- [ ] Run raw 16-bit -> each supported codec -> raw.
- [ ] Run RGB -> each supported codec -> raw.
- [ ] Run multi-frame -> each supported codec -> raw.
- [ ] Run Efferent unit compatibility tests.
- [ ] Run Efferent acceptance transcode tests.
- [ ] Run Efferent acceptance inverse transcode tests.
- [ ] Run render tests where rendering dependencies are available.
- [ ] Compare lossless outputs with exact byte equality after decode.
- [ ] Compare lossy outputs with agreed tolerance after decode.
- [ ] Verify invalid streams throw managed exceptions.
- [ ] Document unsupported edge cases before release.

## 8. Packaging and Consumer Validation

- [ ] Pack one `fo-dicom.PureCodecs` NuGet package.
- [ ] Confirm package contains all codec-family DLLs under `lib/netstandard2.0`.
- [ ] Confirm package does not contain native codec DLLs.
- [ ] Create .NET Framework 4.7.2 consumer smoke test.
- [ ] Register only `PureTranscoderManager` in .NET Framework smoke test.
- [ ] Decode at least one compressed sample in .NET Framework smoke test.
- [ ] Create modern .NET consumer smoke test.
- [ ] Register only `PureTranscoderManager` in modern .NET smoke test.
- [ ] Decode at least one compressed sample in modern .NET smoke test.
- [ ] Verify package install does not require per-family registration.
- [ ] Verify package install does not require native runtime dependencies.

## 9. Documentation and Release Readiness

- [ ] Update README usage instructions.
- [ ] Document phase 1 supported transfer syntaxes.
- [ ] Document package assembly layout.
- [ ] Document managed error behavior.
- [ ] Document known limitations.
- [ ] Document compatibility with `fo-dicom.Codecs`.
- [ ] Update design docs with final implementation notes.
- [ ] Update this checklist so completed items are checked.
- [ ] Prepare release notes for first alpha package.

## Completion Definition

The first replacement phase is complete only when:

- [ ] All phase 1 transfer syntaxes are registered.
- [ ] All phase 1 transfer syntaxes support encode and decode.
- [ ] No production project targets anything except `netstandard2.0`.
- [ ] No codec path uses native DLLs or P/Invoke.
- [ ] One NuGet package contains all required DLLs.
- [ ] Lossless round-trips pass exact byte equality checks.
- [ ] Lossy round-trips pass agreed tolerance checks.
- [ ] Compatibility tests based on `fo-dicom.Codecs` pass.
- [ ] Consumer smoke tests pass on .NET Framework 4.7.2+ and modern .NET.
- [ ] Documentation reflects the implemented behavior.
