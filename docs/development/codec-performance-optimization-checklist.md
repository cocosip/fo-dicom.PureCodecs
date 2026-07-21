# Codec Performance Optimization Checklist

This checklist is the execution order for Phase 1 codec performance work. It
is intentionally separate from unit tests and from generated BenchmarkDotNet
reports. `BenchmarkDotNet.Artifacts` remain local-only.

## Non-Negotiable Correctness Gate

Data correctness takes priority over speed. No optimization is accepted merely
because it lowers elapsed time or allocation. Before changing codec execution,
the affected direction must be checked against `fo-dicom.Native` using the same
real DICOM fixture, transfer syntax, and codec parameters. The optimized path
must preserve the Native-observable pixel data, frame count, required DICOM
tags, and managed failure behavior.

For lossless syntaxes this means exact decoded samples. For lossy syntaxes,
measure the Native/external tolerance before the change and retain that bound;
never invent or widen a tolerance to accommodate a performance change. A Pure
self-round-trip, compressed size, or structurally readable codestream is not
sufficient evidence of compatibility.

## Rules For Every Optimization

- [x] Keep the benchmark project outside the xUnit project-reference graph.
- [x] Use only pure C# codec execution and keep production targets on `netstandard2.0`.
- [x] Treat `fo-dicom.Native`/`fo-dicom.Codecs` fixture behavior as the compatibility baseline when available.
- [ ] Compare the affected encode and decode direction with `fo-dicom.Native` before implementation.
- [ ] Repeat the same Native comparison after implementation and reject the change on any unexplained difference.
- [ ] Capture the relevant encode and decode baseline before selecting a hotspot.
- [ ] Identify one measured hotspot; do not optimize from inspection alone.
- [ ] Add a focused regression that constrains pixels, tags, frame count, and managed failures affected by the change.
- [ ] Make one isolated implementation change.
- [ ] Run the focused regression, Native/external fixture checks, the full unit suite, `--verify`, and the same benchmark.
- [ ] Record before/after time and allocations before marking a step complete.

## Benchmark Coverage

- [x] Isolated encode/decode benchmarks exist for RLE Lossless, JPEG Baseline,
  JPEG Lossless SV1, JPEG-LS Lossless, JPEG 2000 Lossless, and JPEG 2000 Lossy.
- [ ] Add representative benchmarks for JPEG Extended Process 2/4.
- [ ] Add representative benchmarks for JPEG Lossless Process 14.
- [ ] Add representative benchmarks for JPEG-LS Near-Lossless.
- [ ] Add representative benchmarks for HTJ2K Lossless, Lossless RPCL, and Lossy.
- [ ] Run a longer benchmark job for completed candidates before assigning a durable performance threshold.

## Optimization Queue

### 1. JPEG Baseline Process 1 (`1.2.840.10008.1.2.4.50`)

- [x] Capture the isolated codec-decode baseline for the YBR 4:2:2 acceptance fixture.
- [x] Prove that repeated inverse-DCT cosine evaluation is a decode hotspot.
- [x] Cache the 64 inverse-DCT cosine values without changing arithmetic order, color conversion, or DICOM framing.
- [x] Add inverse-DCT reference-sample coverage and run Native JPEG integration checks.
- [x] Verify `706/706` unit tests, all benchmark fixtures, and the matched short benchmark.
- [x] Reduce the measured short-run codec-decode mean from 564.8 ms to 112.8 ms on .NET 10.0.8.
- [x] Match Native libjpeg-turbo 4:2:2 fancy chroma upsampling, constrain the real-fixture RGB output to the measured Native maximum difference of 3, and verify 4x8 and 5x8 cropped Native 2:1 frames match exactly.
- [x] Remove decode block zigzag and dequantization intermediates; reduce codec-decode allocation from 47.54 MB/op to 30.49 MB/op.
- [x] Profile baseline encode separately and establish Native decoding of Pure re-encoded output within the measured maximum difference of 33.
- [x] Remove per-row and per-column integer-DCT temporary arrays; reduce codec-encode allocation from 180.62 MB/op to 114.88 MB/op.
- [x] Reuse per-block sample, DCT, quantization, zigzag, and DCT workspace buffers across each encode traversal; reduce codec-encode allocation to 11.41 MB/op.
- [x] Re-run the matched JPEG Baseline encode ShortRun: 43.79 ms and 11.41 MB/op; decode allocation remained 30.49 MB/op while time was host-variable.
- [x] Complete the current JPEG Baseline optimization pass. Further work requires a new measured hotspot and the same Native correctness gate.

### 2. JPEG Extended Process 2/4 (`1.2.840.10008.1.2.4.51`)

- [ ] Add an 8-bit and a 12-bit representative benchmark fixture.
- [ ] Measure encode and decode separately, including the 12-bit-in-16-bit DICOM container conversion.
- [ ] Profile the selected path without sharing an unverified Baseline optimization.
- [ ] Add Native fixture and 12-bit sample-preservation regressions before implementation.
- [ ] Optimize one measured path and rerun exact 12-bit/container/tag checks.
- [ ] Record verified time and allocation deltas.

### 3. JPEG Lossless Process 14 (`1.2.840.10008.1.2.4.57`)

- [ ] Add a Process 14 benchmark fixture distinct from SV1.
- [ ] Measure predictor, entropy, and DICOM-frame work for encode and decode.
- [ ] Lock exact decoded sample bytes and Native/external codestream behavior before changing the path.
- [ ] Optimize one measured hotspot without changing predictor selection or point-transform semantics.
- [ ] Verify exact round trips, invalid-stream behavior, full tests, and matched benchmarks.

### 4. JPEG Lossless Process 14 SV1 (`1.2.840.10008.1.2.4.70`)

- [x] Capture an initial isolated benchmark fixture.
- [ ] Profile encode and decode independently; do not infer Process 14 results from SV1.
- [ ] Add exact predictor-1 fixture guards and Native/external decode checks.
- [ ] Optimize one measured scan or buffer hotspot.
- [ ] Verify exact round trips, multi-frame behavior, full tests, and matched benchmarks.

### 5. JPEG-LS Lossless (`1.2.840.10008.1.2.4.80`)

- [x] Capture an initial isolated benchmark fixture.
- [ ] Profile regular mode, run mode, Golomb coding, and frame-buffer work separately.
- [ ] Lock exact samples, interleave behavior, EOI framing, and Native/external fixture decode before changing code.
- [ ] Optimize one measured hotspot without changing JPEG-LS context or threshold semantics.
- [ ] Verify exact round trips, multi-frame fixtures, full tests, and matched benchmarks.

### 6. JPEG-LS Near-Lossless (`1.2.840.10008.1.2.4.81`)

- [ ] Add a Near-Lossless benchmark fixture with an explicit `AllowedError`/NEAR value.
- [ ] Measure encode and decode separately for the same NEAR value.
- [ ] Lock the measured Native/external tolerance and marker/interleave behavior.
- [ ] Optimize one measured hotspot without changing NEAR parameter mapping.
- [ ] Verify bounded pixel error, fixture decode, full tests, and matched benchmarks.

### 7. JPEG 2000 Lossless (`1.2.840.10008.1.2.4.90`)

- [x] Capture an initial isolated benchmark fixture.
- [ ] Profile DWT, Tier-1 coding, Tier-2 packet work, and buffer allocation before selecting a change.
- [ ] Lock exact samples plus Native/OpenJPEG fixture decoding and required DICOM tags.
- [ ] Optimize one measured stage while preserving reversible transform and progression behavior.
- [ ] Verify exact round trips, invalid codestream handling, full tests, and matched benchmarks.

### 8. JPEG 2000 Lossy (`1.2.840.10008.1.2.4.91`)

- [x] Capture an initial isolated benchmark fixture.
- [ ] Profile DWT, quantization, rate allocation, Tier-1, and Tier-2 work independently.
- [ ] Lock the measured Native/OpenJPEG pixel tolerance and rate/quality parameter mapping.
- [ ] Optimize one measured stage without changing irreversible transform or rate semantics.
- [ ] Verify fixture decoding, bounded error, full tests, and matched benchmarks.

### 9. HTJ2K Lossless (`1.2.840.10008.1.2.4.201`)

- [ ] Add a standard HTJ2K Lossless benchmark fixture.
- [ ] Profile DWT, HT block coding, packet writing, and decoding separately.
- [ ] Lock exact samples and OpenJPH/Native fixture behavior before changing code.
- [ ] Optimize one measured path while preserving HT block syntax.
- [ ] Verify exact round trips, external decode, full tests, and matched benchmarks.

### 10. HTJ2K Lossless RPCL (`1.2.840.10008.1.2.4.202`)

- [ ] Add an RPCL fixture and benchmark it separately from non-RPCL HTJ2K.
- [ ] Profile progression-order packet assembly as well as block coding.
- [ ] Lock exact samples, RPCL marker/progression behavior, and OpenJPH/Native fixture decode.
- [ ] Optimize one measured path without changing RPCL ordering.
- [ ] Verify exact round trips, progression assertions, full tests, and matched benchmarks.

### 11. HTJ2K Lossy (`1.2.840.10008.1.2.4.203`)

- [ ] Add a standard HTJ2K Lossy benchmark fixture with explicit codec parameters.
- [ ] Profile irreversible DWT, quantization, HT coding, and rate allocation separately.
- [ ] Lock measured Native/OpenJPH tolerance and parameter mapping.
- [ ] Optimize one measured path without changing codestream syntax or rate semantics.
- [ ] Verify bounded error, external decode, full tests, and matched benchmarks.

### 12. RLE Lossless (`1.2.840.10008.1.2.5`)

- [x] Capture an initial isolated benchmark fixture.
- [ ] Profile PackBits scan, segment assembly, and DICOM frame-buffer copies.
- [ ] Lock exact samples, segment offsets, multi-frame behavior, and Native fixture decode.
- [ ] Optimize one measured path only if it remains material after higher-cost codecs are addressed.
- [ ] Verify exact round trips, malformed-input exceptions, full tests, and matched benchmarks.

## Completed Optimization Summary

- [x] JPEG Baseline decode: inverse-DCT cosine cache, committed as `136a718`.
- [x] JPEG Baseline decode: Native 4:2:2 fancy upsampling and allocation-only block pipeline.
- [x] JPEG Baseline encode: integer-DCT and block-workspace allocation removal.
- [ ] All remaining Phase 1 syntax-specific optimization passes.
