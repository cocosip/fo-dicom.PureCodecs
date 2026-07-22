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
- [x] Compare the affected encode and decode direction with `fo-dicom.Native` before implementation.
- [x] Repeat the same Native comparison after implementation and reject the change on any unexplained difference.
- [x] Capture the relevant encode and decode baseline before selecting a hotspot.
- [x] Identify one measured hotspot; do not optimize from inspection alone.
- [x] Add a focused regression that constrains pixels, tags, frame count, and managed failures affected by the change.
- [x] Make one isolated implementation change.
- [x] Run the focused regression, Native/external fixture checks, the full unit suite, `--verify`, and the same benchmark.
- [x] Record before/after time and allocations before marking a step complete.

## Benchmark Coverage

- [x] Isolated encode/decode benchmarks exist for RLE Lossless, JPEG Baseline,
  JPEG Lossless SV1, JPEG-LS Lossless, JPEG 2000 Lossless, and JPEG 2000 Lossy.
- [x] Add representative benchmarks for JPEG Extended Process 2/4.
- [x] Add a representative JPEG Lossless Process 14 benchmark fixture, generated separately from the SV1 acceptance fixture and verified against Native on five bundled real DICOM fixtures.
- [x] Add representative benchmarks for JPEG-LS Near-Lossless.
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

- [x] Add an 8-bit and a 12-bit representative benchmark fixture.
- [x] Measure encode and decode separately, including the 12-bit-in-16-bit DICOM container conversion.
- [x] Profile the selected path without sharing an unverified Baseline optimization.
- [x] Add Native fixture and 12-bit sample-preservation regressions before implementation.
- [x] Optimize one measured path and rerun exact 12-bit/container/tag checks.
- [x] Record verified time and allocation deltas.
- [x] Native `.51` frame-alignment and DCT round-trip checks passed `19/19` before and after the change. The 12-bit regression constrains Native decoding, SOF1 precision, frame count, `BitsAllocated=16`, `BitsStored=12`, `HighBit=11`, and required compression tags.
- [x] EventPipe identified `JpegDct.Inverse` as the 8-bit decode hotspot; the isolated change caches its eight constant inverse-DCT scale values while preserving multiplication order.
- [x] Verification completed: focused DCT codec regression `6/6`, full unit suite `706/706`, and benchmark fixture verification `8/8`.
- [x] Matched ShortRun direct codec measurements: 8-bit encode `37.382 ms / 11.33 MB` to `39.110 ms / 11.33 MB`; decode `176.564 ms / 41.19 MB` to `169.171 ms / 41.19 MB`. 12-bit encode `6.042 ms / 1.41 MB` to `6.829 ms / 1.41 MB`; decode `11.386 ms / 2.47 MB` to `10.030 ms / 2.47 MB`. This is a decode-only optimization; the short-run encode variation is recorded without claiming an encode improvement.

### 3. JPEG Lossless Process 14 (`1.2.840.10008.1.2.4.57`)

- [x] Add a Process 14 benchmark fixture distinct from SV1.
- [x] Measure predictor, entropy, and DICOM-frame work for encode and decode. The matched in-process ShortRun isolated decode allocation to the scan result and its validation workspace; predictor selection and entropy arithmetic were not changed.
- [x] Lock exact decoded sample bytes and Native/external codestream behavior before changing the path. The baseline and post-change `jpeg-lossless-14` interop worker passed all five bundled real DICOM fixtures in both directions.
- [x] Optimize one measured hotspot without changing predictor selection or point-transform semantics. Decode now rents its scan-sample workspace and no longer allocates a validation-only sample array.
- [x] Verify exact round trips, invalid-stream behavior, full tests, and matched benchmarks. Focused workspace coverage passed `5/5`, benchmark fixture verification passed `9/9`, and the complete unit suite passed `707/707`.
- [x] Matched in-process ShortRun on .NET 10.0.8: codec decode allocation fell from `10.01 MB/op` to `2.10 MB/op`; decode mean changed from `21.900 ms` to `20.710 ms`. Transcoder decode allocation also fell from `10.01 MB/op` to `2.10 MB/op`, with the ShortRun mean changing from `27.580 ms` to `17.233 ms`. Encode allocation remained about `12.34 MB/op`; its short-run time variation is not claimed as an encode improvement.

### 4. JPEG Lossless Process 14 SV1 (`1.2.840.10008.1.2.4.70`)

- [x] Capture an initial isolated benchmark fixture.
- [x] Profile encode and decode independently; do not infer Process 14 results from SV1. The isolated SV1 ShortRun identified encode sample conversion as the remaining allocation hotspot at `12.34 MB/op`; decode was independently measured at `2.10 MB/op` after its prior shared workspace improvement.
- [x] Add exact predictor-1 fixture guards and Native/external decode checks. The predictor-1 RGB codestream remains byte-identical to Native, and the baseline and post-change `jpeg-lossless-14-sv1` interop worker passed all five bundled real DICOM fixtures in both directions.
- [x] Optimize one measured scan or buffer hotspot. Encode now rents and returns the raw sample workspace while the scan processes only the frame's actual sample count.
- [x] Verify exact round trips, multi-frame behavior, full tests, and matched benchmarks. Focused workspace coverage passed `6/6`, benchmark fixture verification passed `9/9`, and the complete unit suite passed `708/708`, including the three-frame SV1 matrix case.
- [x] Matched in-process ShortRun on .NET 10.0.8: codec encode allocation fell from `12.34 MB/op` to `6.41 MB/op`, with the directional mean changing from `36.746 ms` to `22.837 ms`. Transcoder encode allocation also fell from `12.34 MB/op` to `6.41 MB/op`; its mean remained host-variable (`23.807 ms` to `23.862 ms`) and is not claimed as a time improvement.

### 5. JPEG-LS Lossless (`1.2.840.10008.1.2.4.80`)

- [x] Capture an initial isolated benchmark fixture.
- [x] Profile regular mode, run mode, Golomb coding, and frame-buffer work separately. A repeated representative-fixture EventPipe CPU trace attributed 64.7% of scan-encode CPU time to sample-interleaved run mode, 34.3% to regular mode, 0.4% to visible Golomb helpers, and 0.6% to frame-buffer work.
- [x] Lock exact samples, interleave behavior, EOI framing, and Native/external fixture decode before changing code. The `jpeg-ls-lossless` interop worker passed all five bundled fixtures in both directions before and after the change; the Pure-to-Native `sample-05` multi-frame direction remains explicitly skipped because the Native baseline corrupts it.
- [x] Optimize one measured hotspot without changing JPEG-LS context or threshold semantics. Sample-interleaved run detection now reads only the left neighbor it consumes instead of calculating all four neighbors for every component and candidate run sample.
- [x] Verify exact round trips, multi-frame fixtures, full tests, and matched benchmarks. Focused JPEG-LS coverage passed `47/47`, the complete unit suite passed `709/709`, and benchmark fixture verification passed `9/9`. Matched .NET 10.0.8 ShortRun codec encode fell from `16.005 ms / 15.35 MB` to `9.549 ms / 15.35 MB`; decode and allocation behavior were not changed intentionally.

### 6. JPEG-LS Near-Lossless (`1.2.840.10008.1.2.4.81`)

- [x] Add a Near-Lossless benchmark fixture with an explicit `AllowedError`/NEAR value. The representative acceptance fixture uses `NEAR=2` and line interleave (`ILV=1`); the benchmark re-encodes its RGB pixels as the codec's sample-interleaved path.
- [x] Measure encode and decode separately for the same NEAR value. The matched pre-change ShortRun codec means were `14.297 ms / 15.27 MB` for encode and `13.897 ms / 10.99 MB` for decode.
- [x] Lock the measured Native/external tolerance and marker/interleave behavior. The external fixture's error bound is 2, and the standalone `jpeg-ls-near-lossless` worker now applies `NEAR=2` to both Pure and Native encoders; it checks all five bundled fixtures in both directions, except the documented Native-corrupting Pure-to-Native `sample-05` multi-frame case.
- [x] Optimize one measured hotspot without changing NEAR parameter mapping. Repeated EventPipe CPU traces attributed 64.6% of sample-interleaved scan encoding to regular mode and 33.7% to run mode; regular encoding now reuses the first component's neighbors already calculated during shared sample-run detection.
- [x] Verify bounded pixel error, fixture decode, full tests, and matched benchmarks. Focused JPEG-LS coverage passed `48/48`, the complete unit suite passed `710/710`, and benchmark fixture verification passed `10/10`. The matched post-change ShortRun codec encode mean was `11.572 ms / 15.27 MB`; allocation was unchanged. Decode was not intentionally optimized.

### 7. JPEG 2000 Lossless (`1.2.840.10008.1.2.4.90`)

- [x] Capture an initial isolated benchmark fixture.
- [x] Profile DWT, Tier-1 coding, Tier-2 packet work, and buffer allocation before selecting a change. A bounded EventPipe sampled-thread-time trace of 40 in-process codec-encode operations attributed 49.4% of observed codec time to Tier-1, 19.5% to frame/buffer orchestration, 18.4% to Tier-2/rate allocation, and 9.4% to DWT. The largest Tier-1 leaves were significance propagation and cleanup.
- [x] Lock exact samples plus Native/OpenJPEG fixture decoding and required DICOM tags. The pre-change and post-change `jpeg2000-lossless` Native interop worker passed all five applicable bundled real DICOM fixtures in both directions; the focused suite constrains exact samples, compression tags, frame counts, and managed invalid-codestream failures.
- [x] Optimize one measured stage while preserving reversible transform and progression behavior. Tier-1's zero-coding, sign-coding, and sign-prediction lookup tables are immutable and now shared as static readonly tables instead of being regenerated for every code block encoder.
- [x] Verify exact round trips, invalid codestream handling, full tests, and matched benchmarks. The focused JPEG 2000 suite passed `204/204`, the full unit suite passed `711/711`, and benchmark fixture verification passed `10/10`. Matched in-process ShortRun codec encode allocation fell from `156.70 MB/op` to `155.77 MB/op`; the mean changed from `181.361 ms` to `178.191 ms`, but the three-iteration host variability is too high to claim a stable time improvement.
- [x] Remove the reversible Tier-1 pre-scaling array by applying its existing six-bit shift while copying samples into the Tier-1 workspace. The input-shift regression constrains exact Tier-1 bytes; the JPEG 2000 suite passed `206/206`, the complete suite passed `714/714`, the `jpeg2000-lossless` Native/OpenJPEG worker passed all five fixtures, and benchmark fixture verification passed `10/10`. In a bounded ten-iteration in-process measurement, lossless codec-encode allocation fell from `155.76 MB/op` to `149.81 MB/op`; the pre-change timing variation was too high to claim a throughput improvement.

### 8. JPEG 2000 Lossy (`1.2.840.10008.1.2.4.91`)

- [x] Capture an initial isolated benchmark fixture.
- [x] Profile DWT, quantization, rate allocation, Tier-1, and Tier-2 work independently. A bounded EventPipe trace of 40 in-process `Rate=16` codec encodes attributed 44.1% of observed codec time to Tier-1, 24.8% to Tier-2/rate allocation, and 18.4% to quantization/irreversible DWT. The largest DWT leaf was `Forward97_2D`.
- [x] Lock the measured Native/OpenJPEG pixel tolerance and rate/quality parameter mapping. The interop worker now applies the benchmark's `Irreversible=true, Rate=16` parameters to both implementations. Measured maximum errors across five bundled fixtures were 8, 15, 34, 58, and 3 in both directions, so its bounded-error threshold is 58 rather than the stale `Rate=0` threshold of 6.
- [x] Optimize one measured stage without changing irreversible transform or rate semantics. The irreversible 9/7 DWT now reuses one row workspace and one column workspace per transform level instead of allocating a temporary array for every row and column; the lifting arithmetic order is unchanged.
- [x] Verify fixture decoding, bounded error, full tests, and matched benchmarks. Focused JPEG 2000 coverage passed `205/205`, the `Rate=16` Native/OpenJPEG worker passed all five fixtures in both directions, the complete unit suite passed `712/712`, and benchmark fixture verification passed `10/10`. Matched 40-operation EventPipe traces were host-variable (`7.68 s` before, `8.38 s` after), so no throughput improvement is claimed.
- [x] Remove rate-allocation's partial-pass byte-prefix copies by retaining each Tier-1 buffer and recording a projected logical byte length. Standard packet encoding writes only that logical prefix, so packet bytes are unchanged. The sharing regression and packet-byte equality check passed; focused JPEG 2000 coverage passed `207/207`, the `jpeg2000-lossy` Native/OpenJPEG worker passed all five real fixtures, the complete unit suite passed `715/715`, and benchmark fixture verification passed `10/10`. In a bounded `1` warmup / `3` iteration / `1` invocation / `1` unroll measurement, lossy codec-encode allocation fell from the prior `172.66 MB/op` to `168.15 MB/op`; the three timing samples remain too variable for a throughput claim.

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
- [x] Profile PackBits scan, segment assembly, and DICOM frame-buffer copies. A bounded EventPipe sampled-thread-time trace attributed 56.0% of active `CodecEncode` samples to `RleSegmentCodec.Encode`, 30.1% to segment extraction, and 7.7% to `Buffer.BlockCopy`; PackBits output construction was the selected hotspot.
- [x] Lock exact samples, segment offsets, multi-frame behavior, and Native fixture decode. The pre-change and post-change `rle` Native interop worker passed all five bundled real DICOM fixtures in both directions; focused coverage constrains exact frames, packetization, offsets, malformed input, and multi-frame behavior.
- [x] Optimize one measured path only if it remains material after higher-cost codecs are addressed. PackBits encoding now writes directly to a pooled maximum-size buffer and returns only the exact encoded segment array, preserving its existing run decisions and byte packetization.
- [x] Verify exact round trips, malformed-input exceptions, full tests, and matched benchmarks. Focused RLE coverage passed `35/35`, the complete unit suite passed `713/713`, and benchmark fixture verification passed `10/10`. Matched .NET 10.0.8 ShortRun codec encode changed from `7.298 ms / 6.30 MB` to `4.520 ms / 4.82 MB` per operation; the three-iteration timing remains host-variable, while allocation fell by `1.48 MB/op`.

## Completed Optimization Summary

- [x] JPEG Baseline decode: inverse-DCT cosine cache, committed as `136a718`.
- [x] JPEG Baseline decode: Native 4:2:2 fancy upsampling and allocation-only block pipeline.
- [x] JPEG Baseline encode: integer-DCT and block-workspace allocation removal.
- [x] RLE Lossless encode: direct PackBits writer with a pooled maximum-size workspace.
- [ ] All remaining Phase 1 syntax-specific optimization passes.
