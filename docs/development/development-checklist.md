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
- [x] Add project references from `fo-dicom.PureCodecs` to all codec-family projects.
- [x] Create test project `tests/fo-dicom.PureCodecs.Tests/fo-dicom.PureCodecs.Tests.csproj`.
- [x] Add test project to the solution.
- [x] Create initial package metadata for one NuGet package.
- [x] Configure package output to include all codec-family DLLs under `lib/netstandard2.0`.
- [x] Add root README with minimal usage example.
- [x] Run `dotnet build` and confirm the empty solution builds.

## 1. Codec Entry Layer

- [ ] Create `PureTranscoderManager`.
- [ ] Make `PureTranscoderManager` inherit fo-dicom `TranscoderManager`.
- [ ] Implement explicit `LoadCodecs` registration.
- [ ] Add private `AddCodec(IDicomCodec codec)` helper.
- [ ] Create shared codec exception wrapper helper.
- [ ] Create shared pixel metadata snapshot helper.
- [ ] Create shared frame validation helper.
- [ ] Create shared `IByteBuffer` conversion helper.
- [ ] Create stub codec base for unimplemented algorithms.
- [ ] Add stub codec for RLE Lossless.
- [ ] Add stub codec for JPEG Process 1.
- [ ] Add stub codec for JPEG Process 2/4.
- [ ] Add stub codec for JPEG Lossless Process 14.
- [ ] Add stub codec for JPEG Lossless Process 14 SV1.
- [ ] Add stub codec for JPEG-LS Lossless.
- [ ] Add stub codec for JPEG-LS Near-Lossless.
- [ ] Add stub codec for JPEG 2000 Lossless.
- [ ] Add stub codec for JPEG 2000 Lossy.
- [ ] Add stub codec for HTJ2K Lossless.
- [ ] Add stub codec for HTJ2K Lossless RPCL.
- [ ] Add stub codec for HTJ2K Lossy.
- [ ] Test `PureTranscoderManager` construction.
- [ ] Test `HasCodec` for all phase 1 transfer syntaxes.
- [ ] Test `GetCodec` returns a codec for all phase 1 transfer syntaxes.
- [ ] Test `CanTranscode(ExplicitVRLittleEndian, syntax)` for all phase 1 transfer syntaxes.
- [ ] Test `CanTranscode(syntax, ExplicitVRLittleEndian)` for all phase 1 transfer syntaxes.
- [ ] Test stub encode/decode throws `DicomCodecException`.
- [ ] Update entry design doc if implementation changes any public class names.

## 2. Test Baseline and Fixtures

- [ ] Copy or reference Efferent unit test fixtures from `D:\Code\dotnet-source\fo-dicom.Codecs\Tests\Unit`.
- [ ] Copy or reference Efferent acceptance fixtures from `D:\Code\dotnet-source\fo-dicom.Codecs\Tests\Acceptance`.
- [ ] Create helper for building raw 8-bit monochrome datasets.
- [ ] Create helper for building raw 16-bit monochrome datasets.
- [ ] Create helper for building RGB interleaved datasets.
- [ ] Create helper for building RGB planar datasets.
- [ ] Create helper for building multi-frame datasets.
- [ ] Create exact byte equality assertion for lossless round-trips.
- [ ] Create tolerance assertion for lossy round-trips.
- [ ] Create frame count preservation assertion.
- [ ] Create required compression tag assertion.
- [ ] Create managed exception assertion for invalid streams.
- [ ] Add acceptance matrix skeleton for raw -> compressed.
- [ ] Add acceptance matrix skeleton for compressed -> raw.
- [ ] Add acceptance matrix skeleton for compressed render tests where rendering dependencies are available.
- [ ] Document any fixture that cannot be redistributed in this repo.

## 3. RLE Lossless

### 3.1 RLE Parser and Writer

- [ ] Add RLE header model.
- [ ] Test parsing a valid 64-byte RLE header.
- [ ] Test rejecting a frame shorter than 64 bytes.
- [ ] Test rejecting segment count less than 1.
- [ ] Test rejecting segment count greater than 15.
- [ ] Test rejecting non-increasing segment offsets.
- [ ] Implement RLE header parser.
- [ ] Implement RLE header writer.
- [ ] Test writing segment count and offsets in little-endian order.

### 3.2 RLE Decoder

- [ ] Test decoding a literal run.
- [ ] Test decoding a repeat run.
- [ ] Test decoding mixed literal and repeat runs.
- [ ] Test rejecting literal run that exceeds input.
- [ ] Test rejecting repeat run that exceeds output.
- [ ] Implement segment decoder.
- [ ] Implement frame decoder for 8-bit monochrome.
- [ ] Implement frame decoder for 16-bit monochrome.
- [ ] Implement frame decoder for RGB interleaved.
- [ ] Implement frame decoder for RGB planar.
- [ ] Wire decoder into `DicomRleLosslessCodec.Decode`.

### 3.3 RLE Encoder

- [ ] Test encoding a literal run.
- [ ] Test encoding a repeat run.
- [ ] Test encoding mixed literal and repeat runs.
- [ ] Test encoder does not emit unsupported segment counts.
- [ ] Implement segment encoder.
- [ ] Implement frame encoder for 8-bit monochrome.
- [ ] Implement frame encoder for 16-bit monochrome.
- [ ] Implement frame encoder for RGB interleaved.
- [ ] Implement frame encoder for RGB planar.
- [ ] Wire encoder into `DicomRleLosslessCodec.Encode`.

### 3.4 RLE Verification

- [ ] Test 8-bit raw -> RLE -> raw exact round-trip.
- [ ] Test 16-bit raw -> RLE -> raw exact round-trip.
- [ ] Test RGB interleaved raw -> RLE -> raw exact round-trip.
- [ ] Test RGB planar raw -> RLE -> raw exact round-trip.
- [ ] Test multi-frame raw -> RLE -> raw exact round-trip.
- [ ] Port Efferent `RLEissue.cs` behavior into local tests.
- [ ] Test save and reopen RLE DICOM file.
- [ ] Mark RLE stub complete and remove stub-only failure expectations.
- [ ] Update [RLE design](../design/rle-codec-design.md) with implementation notes.

## 4. JPEG Family

### 4.1 JPEG Common Infrastructure

- [ ] Add JPEG marker constants.
- [ ] Add JPEG marker reader.
- [ ] Add JPEG marker writer.
- [ ] Test SOI and EOI parsing.
- [ ] Test SOF0 parsing.
- [ ] Test SOF1 parsing.
- [ ] Test SOF3 parsing.
- [ ] Test DHT parsing.
- [ ] Test DQT parsing.
- [ ] Test DRI parsing.
- [ ] Test SOS parsing.
- [ ] Test APPn and COM skipping.
- [ ] Test invalid marker length failure.
- [ ] Add entropy bit reader.
- [ ] Add entropy bit writer.
- [ ] Add Huffman table builder.
- [ ] Test Huffman decode table construction.
- [ ] Test Huffman encode table construction.
- [ ] Add restart marker handling.

### 4.2 JPEG Lossless Core

- [ ] Add lossless predictor functions.
- [ ] Test predictor 1.
- [ ] Test predictors 2 through 7 when supported.
- [ ] Add lossless scan decoder.
- [ ] Add lossless scan encoder.
- [ ] Test 8-bit lossless scan exact round-trip.
- [ ] Test 12-bit lossless scan exact round-trip.
- [ ] Test 16-bit lossless scan exact round-trip.
- [ ] Wire Process 14 decode.
- [ ] Wire Process 14 encode.
- [ ] Wire Process 14 SV1 decode.
- [ ] Wire Process 14 SV1 encode.

### 4.3 JPEG DCT Core

- [ ] Add block model for 8x8 DCT data.
- [ ] Add quantization table model.
- [ ] Add forward DCT implementation.
- [ ] Add inverse DCT implementation.
- [ ] Add zigzag order helper.
- [ ] Test DCT inverse tolerance on known block.
- [ ] Add baseline sequential decoder.
- [ ] Add baseline sequential encoder.
- [ ] Add extended sequential decoder.
- [ ] Add extended sequential encoder.
- [ ] Wire Process 1 decode.
- [ ] Wire Process 1 encode.
- [ ] Wire Process 2/4 decode.
- [ ] Wire Process 2/4 encode.

### 4.4 JPEG DICOM Integration

- [ ] Implement JPEG codec parameter type.
- [ ] Preserve fo-dicom default color conversion behavior for Process 1 and Process 2/4.
- [ ] Add `YBR_FULL` to RGB conversion path.
- [ ] Add `YBR_FULL_422` to RGB conversion path.
- [ ] Add planar to interleaved conversion path where required.
- [ ] Add unsupported photometric interpretation failures.
- [ ] Test Process 1 8-bit lossy round-trip with tolerance.
- [ ] Test Process 2/4 8-bit lossy round-trip with tolerance.
- [ ] Test Process 2/4 12-bit coverage when fixture exists.
- [ ] Test Process 14 exact round-trip for 8-bit data.
- [ ] Test Process 14 exact round-trip for 12-bit data.
- [ ] Test Process 14 exact round-trip for 16-bit data.
- [ ] Test Process 14 SV1 exact round-trip for 8-bit data.
- [ ] Test Process 14 SV1 exact round-trip for 12-bit data.
- [ ] Test Process 14 SV1 exact round-trip for 16-bit data.
- [ ] Test Efferent JPEG acceptance samples.
- [ ] Mark JPEG stubs complete and remove stub-only failure expectations.
- [ ] Update [JPEG design](../design/jpeg-codec-design.md) with implementation notes.

## 5. JPEG-LS Family

### 5.1 JPEG-LS Common Infrastructure

- [ ] Add JPEG-LS marker constants.
- [ ] Add JPEG-LS marker reader.
- [ ] Add JPEG-LS marker writer.
- [ ] Test SOI and EOI parsing.
- [ ] Test SOF55 parsing.
- [ ] Test SOS parsing.
- [ ] Test LSE preset coding parameter parsing.
- [ ] Test APPn and COM skipping.
- [ ] Test invalid marker length failure.
- [ ] Add JPEG-LS frame info model.
- [ ] Add JPEG-LS preset coding parameter model.

### 5.2 JPEG-LS Coding Core

- [ ] Add context model.
- [ ] Add Golomb code reader.
- [ ] Add Golomb code writer.
- [ ] Test Golomb encode/decode.
- [ ] Add regular mode decoder.
- [ ] Add regular mode encoder.
- [ ] Add run mode decoder.
- [ ] Add run mode encoder.
- [ ] Test regular mode sample reconstruction.
- [ ] Test run mode sample reconstruction.
- [ ] Add near-lossless sample clamping logic.
- [ ] Test near-lossless tolerance helper.

### 5.3 JPEG-LS DICOM Integration

- [ ] Implement JPEG-LS codec parameter type.
- [ ] Implement interleave mode mapping.
- [ ] Implement lossless decode.
- [ ] Implement lossless encode.
- [ ] Implement near-lossless decode.
- [ ] Implement near-lossless encode.
- [ ] Add unsupported interleave failures.
- [ ] Add unsupported photometric interpretation failures.
- [ ] Test lossless 8-bit exact round-trip.
- [ ] Test lossless 16-bit exact round-trip.
- [ ] Test lossless RGB exact round-trip where supported.
- [ ] Test near-lossless 8-bit tolerance round-trip.
- [ ] Test near-lossless 16-bit tolerance round-trip.
- [ ] Test multi-frame JPEG-LS data.
- [ ] Test Efferent JPEG-LS acceptance samples.
- [ ] Mark JPEG-LS stubs complete and remove stub-only failure expectations.
- [ ] Update [JPEG-LS design](../design/jpegls-codec-design.md) with implementation notes.

## 6. JPEG 2000 and HTJ2K Family

### 6.1 Codestream Infrastructure

- [ ] Add JPEG 2000 marker constants.
- [ ] Add codestream marker reader.
- [ ] Add codestream marker writer.
- [ ] Test SOC parsing.
- [ ] Test SIZ parsing.
- [ ] Test COD parsing.
- [ ] Test QCD parsing.
- [ ] Test SOT parsing.
- [ ] Test SOD parsing.
- [ ] Test EOC parsing.
- [ ] Test invalid marker length failure.
- [ ] Add image model.
- [ ] Add tile model.
- [ ] Add component model.
- [ ] Add precinct model.
- [ ] Add code-block model.
- [ ] Add packet model.
- [ ] Add progression order iterator.
- [ ] Test LRCP progression when supported.
- [ ] Test RPCL progression for HTJ2K Lossless RPCL.

### 6.2 Transform and Quantization

- [ ] Add reversible color transform.
- [ ] Add irreversible color transform.
- [ ] Add reversible wavelet transform.
- [ ] Add inverse reversible wavelet transform.
- [ ] Add irreversible wavelet transform.
- [ ] Add inverse irreversible wavelet transform.
- [ ] Test reversible transform exact round-trip.
- [ ] Test irreversible transform tolerance round-trip.
- [ ] Add quantization model.
- [ ] Add inverse quantization.

### 6.3 Classic JPEG 2000 Coding

- [ ] Add classic JPEG 2000 code-block decoder.
- [ ] Add classic JPEG 2000 code-block encoder.
- [ ] Add packet decoder.
- [ ] Add packet encoder.
- [ ] Implement JPEG 2000 Lossless decode.
- [ ] Implement JPEG 2000 Lossless encode.
- [ ] Implement JPEG 2000 Lossy decode.
- [ ] Implement JPEG 2000 Lossy encode.
- [ ] Test JPEG 2000 Lossless 8-bit exact round-trip.
- [ ] Test JPEG 2000 Lossless 16-bit exact round-trip.
- [ ] Test JPEG 2000 Lossy tolerance round-trip.
- [ ] Test Efferent JPEG 2000 acceptance samples.

### 6.4 HTJ2K Coding

- [ ] Add HT block decoder.
- [ ] Add HT block encoder.
- [ ] Implement HTJ2K Lossless decode.
- [ ] Implement HTJ2K Lossless encode.
- [ ] Implement HTJ2K Lossless RPCL decode.
- [ ] Implement HTJ2K Lossless RPCL encode.
- [ ] Implement HTJ2K Lossy decode.
- [ ] Implement HTJ2K Lossy encode.
- [ ] Test HTJ2K Lossless exact round-trip.
- [ ] Test HTJ2K Lossless RPCL exact round-trip.
- [ ] Test HTJ2K Lossy tolerance round-trip.
- [ ] Add or import HTJ2K fixtures for acceptance tests.

### 6.5 JPEG 2000 DICOM Integration

- [ ] Implement JPEG 2000 codec parameter type.
- [ ] Implement HTJ2K codec parameter type.
- [ ] Implement DICOM component layout mapping.
- [ ] Implement monochrome output path.
- [ ] Implement RGB output path.
- [ ] Implement YBR-related output path where supported.
- [ ] Add unsupported progression order failures.
- [ ] Add unsupported photometric interpretation failures.
- [ ] Test multi-frame JPEG 2000 data.
- [ ] Test invalid codestream managed exceptions.
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
