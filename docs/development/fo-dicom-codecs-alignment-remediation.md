# fo-dicom.Codecs Alignment Remediation

## 1. Purpose

This document is the execution baseline for closing the confirmed compatibility
gaps between `fo-dicom.PureCodecs` and the public behavior of
`fo-dicom.Codecs` for the Phase 1 transfer syntaxes.

It is intentionally separate from the historical design/checklist documents.
The findings below come from the current production implementation, the local
`fo-dicom.Codecs` source, and current interoperability runs. A checked item in
an older document or a passing self-round-trip test is not evidence that a gap
has been closed.

This document is a remediation specification, not a claim that the work has
already been implemented. Each alignment item remains open until its own
acceptance criteria and the repository-wide release gates pass.

## 2. Current Verified Baseline

The baseline used for this review is:

- PureCodecs baseline commit: `42d88aa2391472a677166eb52bfc2672088910f7`.
- Reference execution uses the normally restored `fo-dicom.Codecs` package
  through its public C# API.
- PureCodecs Release test result after validation cleanup: `806/806` passed,
  `0` skipped.
- Current 12-format process-isolated interoperability result: 4 workers passed,
  8 workers failed, with `104` complete-dataset direction rows passed and `8`
  failed.

The eight failed workers are:

- JPEG Extended Process 2/4.
- JPEG Lossless Process 14.
- JPEG Lossless Process 14 SV1.
- JPEG-LS Lossless.
- JPEG-LS Near-Lossless.
- HTJ2K Lossless.
- HTJ2K Lossless RPCL.
- HTJ2K Lossy.

The prior investigation attributed these failures to reference-side buffer
ownership. That attribution remains diagnostic context only. Validation code
must not inspect a package version or commit to change the result: a public-path
failure is a normal failed row and exits nonzero.

An attempted frame-scoped workaround exposed eight affected complete-dataset
rows, but that workaround is not an acceptable completion gate: splitting or
rebuilding Pixel Data in validation code changes the path being validated. The
result is retained only as diagnostic evidence. Complete public-API DICOM codec
execution remains authoritative and must fail when either direction does not
meet its pixel, frame, or tag assertions.

### 2.1 Progress Dashboard

Last updated: `2026-08-25`

- Overall item progress: `0/9` remediation items complete.
- Overall checkpoint progress: `4/51` implementation checkpoints complete.
- Current active item: `ALN-TEST-001`.
- Current resume point: `ALN-TEST-001 / T5` completion gate.
- Last completed activity: completed the public-path 12-format matrix; 4 workers
  passed and 8 failed with `104/112` direction rows passing.

| Item | Stage | Status | Completed checkpoint | Next checkpoint | Evidence commit/PR |
| --- | --- | --- | --- | --- | --- |
| `ALN-TEST-001` | 0 | Blocked | `T1-T4` | `T5` | Working tree |
| `ALN-JPEG-001` | 1 | Not started | None | `R1` | None |
| `ALN-JPEG-002` | 1 | Not started | None | `M1` | None |
| `ALN-J2K-001` | 1 | Not started | None | `K1` | None |
| `ALN-J2K-002` | 1 | Not started | None | `C1` | None |
| `ALN-JPEG-003` | 2 | Not started | None | `P1` | None |
| `ALN-JPEG-004` | 2 | Not started | None | `S1` | None |
| `ALN-J2K-003` | 2 | Not started | None | `O1` | None |
| `ALN-JLS-001` | 3 | Not started | None | `H1` | None |

Allowed status values are:

- `Not started`: no production implementation checkpoint is complete.
- `In progress`: at least one checkpoint is complete and the item gate is open.
- `Blocked`: the next checkpoint cannot proceed; the blocking evidence is
  recorded in the progress log.
- `Done`: every checkpoint and acceptance criterion for the item has passed.
- `Deferred`: scope was explicitly changed and the decision/evidence is logged.

### 2.2 Resume Protocol

At the start of every later alignment session:

1. Read sections 2.1 and 2.3 first.
2. Verify the current repository HEAD and restore dependencies normally.
3. Locate the dashboard row for the current resume point.
4. Continue from the first unchecked checkpoint in that item's `Progress
   checkpoints` subsection.
5. Do not repeat completed investigation or fixture generation unless the
   production path, fixture, reference package, or recorded evidence changed.
6. After completing a checkpoint, update its checkbox, the dashboard's
   `Completed checkpoint` and `Next checkpoint`, and the progress log in the
   same change set.
7. Mark an item `Done` only after its acceptance criteria and section 15 gate
   pass. A focused unit test alone is not sufficient.

If work stops midway through a checkpoint, leave it unchecked and add a log
entry that records the exact file/function, observed output, and next concrete
action. The next session resumes that checkpoint rather than starting the item
again.

### 2.3 Progress Log

This table is append-only. Keep enough evidence to distinguish completed work
from a hypothesis or an interrupted attempt.

| Date | Item/checkpoint | Result | Verification/evidence | Next action |
| --- | --- | --- | --- | --- |
| 2026-08-25 | Baseline review | Complete | Pure HEAD `42d88aa`; Release `820/820`; interoperability 9 workers passed and 3 reference-beta multi-frame ownership failures | Start `ALN-TEST-001 / T1` |
| 2026-08-25 | `ALN-TEST-001 / T1` | Rejected | Package-version/commit classification was test-side result adaptation, not interoperability evidence | Restart `ALN-TEST-001 / T1` |
| 2026-08-25 | `ALN-TEST-001 / T2` | Complete | RLE worker frame-scoped Native decode gate reported all 7 frames of `sample-05.dcm`; focused Release test `1/1` passed | Start `ALN-TEST-001 / T3` |
| 2026-08-25 | `ALN-TEST-001 / T3` | Complete | JPEG-LS no-skip beta classification and separate codec/dataset counters; combined focused Release tests `6/6` passed | Run all 12 workers for `ALN-TEST-001 / T4` |
| 2026-08-25 | `ALN-TEST-001 / T4` | Complete | Release matrix: 12/12 workers passed; codec rows `244/244`; known beta wrapper defects 8; unexpected dataset failures 0 | Run `ALN-TEST-001 / T5` completion gate |
| 2026-08-25 | `ALN-TEST-001 / T1-T4` | Rejected | Frame splitting, output reconstruction, internal codec calls, and known-defect success classification changed or masked the public path under test; prior counts are diagnostic only | Restart `T1` by removing validation-side compatibility behavior |
| 2026-08-25 | `ALN-TEST-001 / T1` | In progress | Removed `.deps.json`, package/commit/runtime-version checks, environment-result switch, dedicated version-gate script, and Native output trimming; focused manifest test `1/1` passed | Remove remaining internal codec calls and validation-side codestream adaptation |
| 2026-08-25 | `ALN-TEST-001 / T1` | Complete | Source scan found no version/result switches, internal Pure codec calls, frame reconstruction, EOC trimming, or Native output post-processing in the three validation/reference tools | Start `T2` |
| 2026-08-25 | `ALN-TEST-001 / T2` | Complete | HTJ2K reference now uses complete-dataset `DicomTranscoder`, raw public frame buffers, and `reference.dcm`; HTJ2K validation uses Native `DicomTranscoder`, pixel/tag assertions, and `decoded.dcm`; focused tests `11/11` passed | Start `T3` |
| 2026-08-25 | `ALN-TEST-001 / T3` | Complete | JPEG-LS worker returned nonzero with `9` passed and `1` failed complete-dataset rows; no skip, blocked, or version classification | Run `T4` matrix |
| 2026-08-25 | `ALN-TEST-001 / T4` | Complete | Release tests `806/806`; 12-format matrix exited nonzero: 4 workers passed, 8 failed, direction rows `104/112` passed | Keep `T5` open until all public-path rows pass |
| 2026-08-25 | `ALN-TEST-001 / version-gate cleanup` | Complete | Removed the central minimum-version range, deleted the remaining version-field assertions, and made project instructions prohibit package/runtime identity checks; compatibility remains behavior-only | Continue `T5` from the public-path failures |

When a checkpoint changes status, append one row using this pattern:

```text
YYYY-MM-DD | ALN-... / Xn | Complete, failed, or blocked | exact test/interop result and commit | next checkpoint or recovery action
```

## 3. Scope

### 3.1 In scope

| ID | Family | Confirmed gap | Direction | Priority |
| --- | --- | --- | --- | --- |
| `ALN-JPEG-001` | JPEG/JPEG Lossless/JPEG-LS | DRI/RST restart intervals | Decode | P0 |
| `ALN-JPEG-002` | JPEG/JPEG Lossless | Multiple scans and per-component DC tables | Decode | P0 |
| `ALN-J2K-001` | Classic JPEG 2000 | SOP/EPH packet markers | Decode | P0 |
| `ALN-J2K-002` | Classic JPEG 2000 | RESET/VSC code-block styles | Decode | P0 |
| `ALN-JPEG-003` | JPEG Process 1/2/4 | 16/8 containers, 12-bit color, 16-bit DQT | Encode/decode | P1 |
| `ALN-J2K-003` | Classic JPEG 2000 | Non-LRCP progression orders | Encode | P1 |
| `ALN-JPEG-004` | JPEG Process 1/2/4 | `SmoothingFactor` behavior | Encode | P1 |
| `ALN-JLS-001` | JPEG-LS | APP8 `mrfx` HP1/HP2/HP3 transform | Decode | P2 |
| `ALN-TEST-001` | Interoperability tooling | Reference beta pooled-buffer isolation | Test infrastructure | P0 |

P0 items accept legal compressed input that Native accepts and therefore have
the highest interoperability risk. P1 items close encoding/API behavior gaps.
P2 is a Native extension rather than a JPEG-LS Part 1 baseline requirement.

### 3.2 Explicitly out of scope

The following observations are not remediation items:

- RLE: no current production-code difference was found and bidirectional
  multi-frame interoperability passes.
- HTJ2K `.201/.202/.203`: default-parameter monochrome/RGB and single/multi-frame
  interoperability currently passes. The reference beta ownership bug is
  handled only under `ALN-TEST-001`.
- HTJ2K RGN and PPM/PPT: current OpenJPH has the same limitation.
- JPEG 2000 component subsampling: the Native managed extraction path also
  requires full image-sized components.
- JP2 wrapper tolerance: DICOM JPEG 2000 frames require a raw codestream; Native
  accepting a JP2 wrapper is extra tolerance, not a Phase 1 requirement.
- JPEG 9-, 10-, and 11-bit sequential DCT: the Native implementation is
  permissive, but this is not part of the Process 1/2/4 conformance target.
- JPEG 2000 PTERM: no current evidence shows a decoded-sample difference. Add a
  remediation item only after a legal fixture demonstrates a failure.
- JPEG-LS encode-time `InterleaveMode` and `ColorTransform` parameters: the
  current Native DICOM entry point also derives interleave from Pixel Data and
  forces no color transform. Do not change the Pure public behavior in the
  name of Native alignment.

## 4. Cross-Cutting Rules

### 4.1 Production constraints

- Production projects remain `netstandard2.0` only.
- All production codec execution remains pure C#.
- Do not add P/Invoke, native binaries, or a native fallback.
- Do not copy or translate OpenJPEG, OpenJPH, libjpeg, or CharLS source.
- Keep one NuGet package and the existing family assembly split.
- Keep registration through `PureTranscoderManager`; no per-family registration
  is introduced.

### 4.2 Reference boundary

- Tests and tools may use only the public C# API of normally restored
  `fo-dicom` and `fo-dicom.Codecs` packages.
- Do not reference a local Native DLL, replace package DLLs, use `HintPath`, or
  add a project reference to a local upstream checkout.
- Local upstream source may explain behavior but is not executable test code.
- A special-purpose codestream not producible through public Native parameters
  must come from a redistributable fixture with recorded provenance, or from a
  small standards-derived test builder. In both cases, validate the resulting
  DICOM frame through the public `fo-dicom.Codecs` decoder before accepting it
  as a compatibility fixture.

### 4.3 Evidence hierarchy

For every item, evidence is ranked as follows:

1. Public Native decode/encode of the same DICOM frame or Pixel Data contract.
2. Independent standards-derived fixture decoded by both implementations.
3. Exact lossless samples or bounded lossy samples after cross-stack decode.
4. Structural marker/packet assertions.
5. Pure self-round-trip.

Items 4 and 5 are supporting evidence only. They cannot close an alignment
item without item 1 or 2.

### 4.4 Required test layers

Every remediation item must add all applicable layers:

- A focused primitive/state test for the exact algorithm change.
- A frame-codec test using a complete legal codestream.
- A DICOM `IDicomCodec` test checking frames and required tags.
- A Native-to-Pure public-API interoperability test.
- A Pure-to-Native test when the Pure encoder is changed.
- Managed exception tests for corrupt marker order, invalid lengths, and
  truncated entropy data.

Lossless output requires exact decoded sample equality. Lossy output requires a
documented tolerance derived from the same Native parameters and fixtures.

## 5. `ALN-TEST-001`: Remove Validation-Side Compatibility Adaptation

### Problem

The validation and reference projects accumulated special handling for specific
failures: package-version/commit classification, `.deps.json` inspection,
environment-result switches, per-frame dataset reconstruction, internal Pure
codec calls, and output trimming. Those paths change or reinterpret the behavior
being validated and therefore cannot establish compatibility.

### Required adjustment

Modify:

- `tools/fo-dicom.PureCodecs.InteropValidation/Program.cs`
- `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceAlignmentTests.cs`

Keep the validation boundary thin and public:

1. The authoritative row must pass the unmodified complete DICOM dataset
   through the public `IDicomCodec` or `DicomTranscoder` API once per direction.
2. Do not trim, patch, split, rebuild, or decode a frame through an internal
   Pure codec in order to make a reference row pass.
3. A separate single-frame fixture may be used as diagnostic evidence, but it
   cannot replace, downgrade, or make the complete-dataset row successful.
4. Do not read package versions, commits, assembly metadata, `.deps.json`, or
   environment variables to classify or alter a result.
5. Remove test environment switches and format-specific compatibility branches.
   Legal-input compatibility must be implemented in the corresponding
   production codec under `src/`.

All complete-dataset rows must pass without test-side adaptation. Any remaining
failure stays failed and becomes input to a production-codec repair.

### Progress checkpoints

- [x] `T1` Remove validation-side frame/output adaptation, internal codec calls,
  environment-result switches, and Native output post-processing.
- [x] `T2` Reduce reference/validation tools to public API execution, artifact
  capture, and assertions that do not alter codec input or output.
- [x] `T3` Make complete-dataset rows authoritative with only ordinary `passed`
  or `failed` results and no skips.
- [x] `T4` Run all 12 workers and record complete public-path evidence plus any
  diagnostic single-frame evidence separately.
- [ ] `T5` Pass the item acceptance criteria and section 15 completion gate with
  normally restored dependencies.

### Acceptance criteria

- All 12 formats have complete-dataset bidirectional evidence with no skips or
  validation-side output changes.
- Every failed public-path row remains failed and causes a nonzero worker exit.
- No package version, commit, `.deps.json`, or environment variable changes a
  compatibility result.

## 6. `ALN-JPEG-001`: JPEG Family Restart Intervals

### Problem and root cause

The following parsers explicitly reject DRI and RST markers:

- `src/fo-dicom.PureCodecs.Jpeg/Internal/JpegSequentialDctCodec.cs`
- `src/fo-dicom.PureCodecs.Jpeg/Internal/JpegLosslessFrameCodec.cs`
- `src/fo-dicom.PureCodecs.JpegLs/Internal/JpegLsFrameCodec.cs`
- `src/fo-dicom.PureCodecs.Jpeg/Internal/JpegMarkerReader.cs`
- `src/fo-dicom.PureCodecs.JpegLs/Internal/JpegLsMarkerReader.cs`

The entropy readers currently treat entropy data as one uninterrupted byte
sequence. They do not return restart boundaries to the scan decoder, and the
scan decoders have no operation for resetting predictor/context state.

### JPEG sequential DCT remediation

Introduce a parsed restart interval value and an entropy scan representation
that retains RST boundaries instead of flattening everything into one byte
array. The recommended internal responsibilities are:

- `JpegRestartInterval`: parsed DRI value in MCUs, with strict two-byte payload
  validation for classic JPEG.
- `JpegEntropySegment`: entropy bytes followed by the observed RST marker.
- `JpegEntropyScanReader`: removes byte stuffing, stops at RST/next marker, and
  preserves the marker sequence without treating it as compressed data.

In `JpegSequentialDctCodec`:

1. Count decoded MCUs, not pixels or blocks.
2. At each non-final restart boundary, byte-align the entropy reader.
3. Require `RST0` through `RST7` in modulo-eight order.
4. Reset all DC predictors to zero.
5. Start a fresh entropy bit reader after the marker.
6. Reject a missing, early, duplicate, or out-of-sequence restart marker with a
   managed `DicomCodecException` containing the MCU index.

Sampling factors matter: one restart interval counts MCUs even when an MCU
contains multiple luma/chroma blocks.

### JPEG Lossless remediation

Use the same classic JPEG DRI/RST parser, but reset lossless scan state at an
MCU boundary:

- reset the restart marker sequence;
- reset differential Huffman state;
- make the first sample after restart use the JPEG lossless initial prediction
  value derived from sample precision and point transform;
- do not reuse left/upper neighbors across a restart boundary.

The implementation belongs in `JpegLosslessScanCodec` rather than in the DICOM
adapter so that frame-level tests can exercise it directly.

### JPEG-LS remediation

JPEG-LS DRI supports 2-, 3-, or 4-byte interval values. Add this parsing to the
JPEG-LS marker model. At every restart boundary:

- validate the modulo-eight RST marker;
- reset JPEG-LS regular/run context models and run indices;
- reset line/predictor state as required by the JPEG-LS restart process;
- continue with the effective LSE preset and NEAR value for that scan.

Keep the JPEG and JPEG-LS restart implementations separate after marker
parsing; their reset semantics are not interchangeable.

### Progress checkpoints

- [ ] `R1` Add classic JPEG DRI parsing and entropy-segment/RST preservation.
- [ ] `R2` Implement sequential DCT MCU restart handling and predictor reset.
- [ ] `R3` Implement JPEG Lossless restart prediction and scan-state reset.
- [ ] `R4` Add JPEG-LS 2/3/4-byte DRI parsing and restart-state reset.
- [ ] `R5` Replace rejection tests with legal and corrupt restart fixtures.
- [ ] `R6` Pass Native/Pure cross-stack restart interoperability in applicable
  directions.
- [ ] `R7` Pass the item acceptance criteria and section 15 completion gate.

### Tests and fixtures

Add focused tests to:

- `JpegSequentialDctCodecTests.cs`
- `JpegLosslessCodecRoundTripTests.cs`
- `JpegLosslessScanCodecTests.cs`
- `JpegLsInvalidStreamTests.cs`
- `JpegLsCodecRoundTripTests.cs`

Replace the current "restart is rejected" expectations with:

- interval of one MCU/line;
- interval spanning several MCUs/lines;
- more than eight boundaries to prove RST wraparound;
- truncated data before RST;
- wrong RST number;
- RST without DRI;
- exact lossless cross-stack decoded pixels.

### Acceptance criteria

- Native-generated or Native-validated restart fixtures decode in Pure.
- Pure and Native return identical pixels for all lossless fixtures.
- Corrupt restart sequencing produces a managed exception, never an index or
  buffer exception.
- Existing non-restart codestream bytes and performance remain unchanged.

## 7. `ALN-JPEG-002`: Multiple Scans and Huffman Table Selection

### Problem and root cause

`JpegSequentialDctCodec.ParseFrame` and `JpegLosslessFrameCodec.ParseFrame`
stop after the first SOS. Sequential DCT can therefore return zero-filled
planes for components stored in later scans. JPEG Lossless rejects a scan that
does not contain every DICOM component. It also resolves one DC Huffman table
for the whole scan even though each scan component has its own selector.

### Required data model

Replace the single `Scan`/`ScanData` fields with an ordered scan collection.
Each parsed scan must snapshot:

- its SOS component selectors and table selectors;
- spectral/successive fields;
- entropy segments and restart interval;
- the DHT/DQT tables effective when that SOS begins.

Table snapshots are required because legal JPEG streams may redefine a table
between scans. Keeping only the final global table array can decode an earlier
scan with the wrong table.

### Sequential DCT remediation

1. Parse until EOI and collect every SOS.
2. Decode each scan into shared component planes.
3. Support an interleaved scan containing all components and non-interleaved
   scans containing a subset, including one component per scan.
4. Maintain DC predictors per component and reset them at scan/restart
   boundaries.
5. Reject duplicate component coverage when it would overwrite an already
   completed sequential component plane.
6. Require every SOF component to be fully populated before color conversion.

This item does not add progressive JPEG support. SOF2 and progressive spectral
or successive-approximation scans remain outside Phase 1 and must fail
explicitly.

### JPEG Lossless remediation

1. Decode every SOS in order into shared component sample planes.
2. Resolve the DC Huffman table separately for each scan component.
3. Preserve the selection value and point transform from each scan.
4. Support one-component non-interleaved scans and multi-component interleaved
   scans.
5. Require every frame component exactly once unless the JPEG process permits a
   continuation represented by the scan parameters.

Refactor `JpegLosslessScanCodec` to accept a per-component table map instead of
one `JpegHuffmanTable` when decoding a multi-component scan.

### Progress checkpoints

- [ ] `M1` Replace the single-scan parsed model with ordered scan/table snapshots.
- [ ] `M2` Decode sequential DCT scans into shared component planes.
- [ ] `M3` Decode JPEG Lossless scans with per-component DC table selection.
- [ ] `M4` Add legal multi-scan/table-redefinition fixtures and corrupt variants.
- [ ] `M5` Pass Native-to-Pure multi-scan interoperability and existing
  single-scan regressions.
- [ ] `M6` Pass the item acceptance criteria and section 15 completion gate.

### Tests and fixtures

Add legal full-frame fixtures covering:

- RGB as three sequential one-component scans;
- RGB in one interleaved scan;
- DHT redefinition between scans;
- different DC tables for components in one JPEG Lossless scan;
- a missing final component scan;
- duplicate component scan;
- EOI before the final scan completes.

Validate the legal fixtures with the public Native decoder and require exact
pixels for JPEG Lossless. Use the existing lossy tolerance policy for DCT JPEG.

### Acceptance criteria

- No parser exits merely because the first SOS was read.
- All SOF components are reconstructed before output is returned.
- Per-component table selectors affect the correct component only.
- Existing single-scan Native/Pure interoperability remains unchanged.

## 8. `ALN-JPEG-003`: JPEG Precision, Containers, and DQT

### Problem and root cause

`DicomJpegSequentialCodecBase` accepts only 8/8 data except for a dedicated
16-allocated/12-stored monochrome Process 2/4 branch. This excludes two Native
paths:

- an 8-bit JPEG sample stored in a 16-bit DICOM container;
- a 12-bit three-component Process 2/4 image.

`JpegSequentialDctCodec.ParseQuantizationTables` also rejects 16-bit DQT
entries even though the internal quantization model stores integer divisors.

### Container normalization

Add a family-internal sample input abstraction that exposes:

- sample precision;
- component count;
- interleaved sample access as `int` or `ushort`;
- explicit little-endian DICOM container unpacking.

For `BitsAllocated=16, BitsStored=8`, unpack the low eight bits before encoding,
matching the Native observable behavior. Update compressed Pixel Data metadata
through the DICOM adapter in the same way as Native; do not silently truncate
embedded overlays without an explicit validation decision.

On decode, use codestream precision to select the output container and verify
it against the target DICOM metadata before allocating output.

### 12-bit color

Generalize the existing 12-bit DCT path from a monochrome `ushort[]` shortcut
to component-aware samples. The DCT, quantization, Huffman, sampling, and color
conversion stages must accept 12-bit values without downcasting to `byte`.

Initial scope:

- Process 2/4 only;
- RGB or full-resolution YBR input;
- SF444 first, followed by SF422 only after a cross-stack fixture proves the
  sample layout and tolerance;
- both interleaved and explicitly normalized planar DICOM input.

### 16-bit DQT

When DQT precision is one:

1. Read each value as unsigned big-endian 16-bit.
2. Reject zero divisors and truncated payloads.
3. Preserve the existing zigzag-to-natural mapping.
4. Keep encoder output at 8-bit DQT unless an encoder requirement is separately
   demonstrated; this item primarily closes Native-compatible decode.

### Progress checkpoints

- [ ] `P1` Add 16/8 DICOM container normalization and metadata tests.
- [ ] `P2` Generalize the 12-bit DCT path to component-aware samples.
- [ ] `P3` Add 12-bit RGB SF444 encode/decode support and fixtures.
- [ ] `P4` Parse 16-bit DQT values and add invalid-payload coverage.
- [ ] `P5` Pass public Native interoperability for 16/8, 12-bit RGB, and 16-bit
  DQT cases.
- [ ] `P6` Pass the item acceptance criteria and section 15 completion gate.

### Acceptance criteria

- 16/8 input follows the same metadata and decoded-pixel behavior as Native.
- 12-bit RGB Pure output decodes through Native within a fixture-derived lossy
  tolerance.
- Native 12-bit RGB output decodes through Pure within the same tolerance.
- Legal 16-bit DQT input decodes through Pure and Native with equivalent output.
- Existing 8-bit and 12-bit monochrome codestream baselines do not regress.

## 9. `ALN-JPEG-004`: `SmoothingFactor`

### Problem

Pure throws for every non-zero `SmoothingFactor`. Native has two observable
behaviors:

- eligible Baseline turbo paths ignore the value;
- non-turbo sequential paths pass it to libjpeg and smoothing affects encoding.

The setting is a codec API compatibility concern, not a JPEG codestream marker
feature.

### Required behavior mapping

Mirror the Native path decision using DICOM-visible inputs:

- Baseline, precision <= 8, non-palette input: accept and ignore smoothing,
  because the Native turbo path ignores it.
- Process 2/4 and other non-turbo-compatible inputs: apply an independently
  implemented pre-DCT smoothing stage.

Validate the same accepted numeric range as Native/libjpeg. Invalid values must
produce a managed parameter exception before any frame is emitted.

Do not copy the libjpeg smoothing implementation. Derive the filter from public
behavior and applicable JPEG documentation, then lock it with public Native API
fixtures.

### Progress checkpoints

- [ ] `S1` Capture Native public-API behavior for ignored and applied paths.
- [ ] `S2` Accept/validate the Native-supported parameter range and preserve the
  Baseline-ignore behavior.
- [ ] `S3` Implement the independent non-turbo pre-DCT smoothing stage.
- [ ] `S4` Pass lossy cross-stack behavior/tolerance and factor-zero regression
  tests.
- [ ] `S5` Pass the item acceptance criteria and section 15 completion gate.

### Acceptance criteria

- Non-zero smoothing no longer fails on a Native-supported input.
- On the Native-ignore path, factor zero and non-zero produce the same Pure
  result.
- On the Native-apply path, non-zero smoothing measurably changes the encoded
  result and both directions decode within the documented lossy tolerance.
- `SmoothingFactor=0` preserves all existing byte baselines.

## 10. `ALN-J2K-001`: SOP and EPH Packet Markers

### Problem and root cause

`Jpeg2000CodingStyle.ParseStyle` retains only the precinct bit from `Scod`.
The SOP (`0x02`) and EPH (`0x04`) flags are discarded. The standalone marker
types can parse SOP/EPH structurally, but `Jpeg2000StandardPackets.DecodePacket`
never consumes them from the packet stream. Marker unit tests therefore do not
prove packet decode support.

### Coding-style model changes

Extend the resolved coding-style model with explicit properties:

- `HasStartOfPacketMarkers` from `Scod & 0x02`.
- `HasEndOfPacketHeaderMarkers` from `Scod & 0x04`.

Preserve these properties through COD/COC inheritance and tile-header
overrides. Precinct handling remains independent.

### Packet reader changes

Before an inline packet header:

1. If SOP is enabled and present, consume the complete SOP segment.
2. Validate `Lsop=4` and the modulo-65536 packet sequence number.
3. Follow Native tolerance for a missing optional SOP, but never interpret an
   SOP byte as packet-header bits.

After the packet header is byte-aligned:

1. If EPH is enabled, require and consume `FF92`.
2. Advance the correct cursor before reading code-block body bytes.

The implementation must keep separate cursors for packed packet headers and
tile body data. Add explicit cases for inline headers, PPM, and PPT so that an
EPH marker is consumed from the location defined by the codestream organization
rather than assumed to be in `_data`.

### Progress checkpoints

- [ ] `K1` Preserve SOP/EPH flags through COD/COC and tile inheritance.
- [ ] `K2` Consume and validate inline SOP markers and sequence numbers.
- [ ] `K3` Consume required EPH markers without shifting packet body offsets.
- [ ] `K4` Handle SOP/EPH correctly with PPM and PPT header cursors.
- [ ] `K5` Pass Native-validated full-codestream and malformed-marker tests.
- [ ] `K6` Pass the item acceptance criteria and section 15 completion gate.

### Tests and fixtures

- One packet with SOP only.
- One packet with EPH only.
- Multiple packets with SOP sequence rollover coverage at the primitive level.
- SOP+EPH with multiple layers/resolutions.
- SOP/EPH combined with PPM and PPT.
- Invalid `Lsop`, wrong sequence, missing EPH, and truncated marker.
- Full Native-validated lossless codestream with exact decoded pixels.

### Acceptance criteria

- SOP/EPH flags survive COD/COC and tile override resolution.
- Packet body offsets remain exact with and without packed headers.
- Native-valid SOP/EPH codestreams decode exactly in Pure.
- Malformed packet markers fail with packet/tile context in a managed exception.

## 11. `ALN-J2K-002`: RESET and VSC Code-Block Styles

### Problem and root cause

The standard Tier-1 decoder currently implements:

- BYPASS (`0x01`) raw lazy passes;
- TERMALL (`0x04`) contribution segmentation;
- SEGMARK (`0x20`) segmentation-symbol consumption.

It does not implement:

- RESET (`0x02`): reset MQ context probabilities after each coding pass;
- VSC (`0x08`): vertically causal stripe context formation.

OpenJPEG's public decoder path handles both flags.

### RESET remediation

Refactor Tier-1 context initialization into one operation used at decoder start
and pass-boundary reset. It must restore all 19 contexts, including the special
initial states for zero coding, run length, and uniform contexts.

After every completed coding pass, if RESET is set and the next pass remains in
the same code-block segment, reset context probability state without resetting:

- arithmetic decoder byte position;
- decoded coefficient significance/magnitude;
- current bit-plane/pass type;
- code-block neighbor flags.

Add a focused `Jpeg2000StandardMqDecoder.ResetContexts`-style API rather than
reconstructing the decoder, because reconstructing it would incorrectly restart
the arithmetic byte stream.

### VSC remediation

Centralize zero/sign context formation behind a stripe-aware neighbor accessor.
When VSC is set, apply the ISO/IEC 15444-1 vertically causal boundary rule at
four-row stripe boundaries. The mask must affect significance, sign, cleanup,
and run-mode context decisions consistently.

Do not scatter VSC conditionals through individual pass loops. A single context
formation boundary makes it possible to test the same rule for all pass types.

### PTERM decision

PTERM changes predictable termination and validation. The current review does
not prove a decoded-sample failure. First add a Native-validated fixture and a
focused termination test. Implement PTERM validation only if that test fails;
do not combine speculative PTERM work with RESET/VSC.

### Progress checkpoints

- [ ] `C1` Add RESET/VSC Native-validated fixtures that fail before production
  changes.
- [ ] `C2` Centralize Tier-1 MQ context initialization/reset behavior.
- [ ] `C3` Apply RESET after each coding pass without resetting arithmetic input.
- [ ] `C4` Centralize and implement VSC stripe-boundary context formation.
- [ ] `C5` Test RESET/VSC combinations and separately record the PTERM decision.
- [ ] `C6` Pass the item acceptance criteria and section 15 completion gate.

### Acceptance criteria

- RESET fixtures exercise at least two passes without TERMALL so an actual
  in-segment context reset is required.
- VSC fixtures contain significant coefficients on both sides of a stripe
  boundary.
- RESET+VSC and RESET+BYPASS combinations decode through Pure and Native.
- Lossless fixtures produce exact samples; lossy fixtures use a fixed tolerance.
- Existing HT code-block style `0x40` behavior is unchanged.

## 12. `ALN-J2K-003`: Non-LRCP Classic JPEG 2000 Encoding

### Problem and root cause

`Jpeg2000StandardFrameEncoder` rejects every progression order except LRCP,
while `DicomJpeg2000Params.ProgressionOrder` and Native OpenJPEG support LRCP,
RLCP, RPCL, PCRL, and CPRL. The Pure decoder already supports all five orders
and POC; this item changes only classic JPEG 2000 encoding.

### Required adjustment

Remove the LRCP guard only after packet emission is generalized. Reuse the
existing `Jpeg2000ProgressionOrderIterator` as the single ordering authority.

The encoder must:

1. Build the complete packet model independently of output order.
2. Enumerate packets in the requested progression order.
3. Preserve inclusion/tag-tree and code-block contribution state across the
   new order.
4. Write the selected order into COD.
5. Keep layer truncation and rate allocation attached to `(layer, resolution,
   component, precinct)`, not to the old LRCP loop position.

PCRL and CPRL require correct precinct geometry across components. Do not treat
the precinct index as globally interchangeable when component resolutions
differ.

### Progress checkpoints

- [ ] `O1` Build packet models independently of emission order.
- [ ] `O2` Emit RLCP and RPCL while preserving layer/contribution state.
- [ ] `O3` Emit PCRL and CPRL with component-aware precinct geometry.
- [ ] `O4` Pass all five order combinations through Pure and Native decoders.
- [ ] `O5` Pass the item acceptance criteria and section 15 completion gate.

### Tests and acceptance

For all five orders, cover monochrome and RGB, lossless and lossy, multiple
resolutions, multiple precincts, and multiple layers.

Acceptance requires:

- COD reports the requested order.
- Pure decodes its output.
- Public Native OpenJPEG decodes Pure output.
- Native output for the same public progression parameter decodes in Pure.
- Lossless pixels are exact and lossy pixels meet the existing rate-specific
  tolerance.
- LRCP default codestream baselines remain unchanged.

## 13. `ALN-JLS-001`: CharLS HP Color Transform Decode

### Problem and classification

CharLS recognizes APP8 payload `mrfx` followed by HP1, HP2, or HP3 and applies
the inverse reversible transform. Pure currently skips APP8 as metadata, so it
decodes transformed component values as if they were RGB.

This is a Native extension compatibility item. JPEG-LS Part 1 does not define a
standard way to transport these HP transforms.

### Required adjustment

Add a JPEG-LS APP8 metadata parser that distinguishes:

- `mrfx` color-transform metadata;
- SPIFF APP8 structures;
- unrelated APP8 payloads, which remain safely skipped.

Store the transform on the parsed frame and validate:

- transform value is None, HP1, HP2, or HP3;
- exactly three components are present for HP transforms;
- sample precision/layout is supported;
- conflicting `mrfx` declarations fail explicitly.

After scan reconstruction and before planar output conversion, apply the exact
inverse reversible transform with modular arithmetic at the frame sample
precision. Keep this in a focused `JpegLsColorTransform` helper with separate
tests for HP1, HP2, HP3, wraparound, 8-bit, and 16-bit values.

Do not enable HP transform encoding through `DicomJpegLsParams` as part of this
item: the current Native DICOM encoder forces `None`, so doing so would expand
the public contract rather than align it.

### Progress checkpoints

- [ ] `H1` Parse APP8 `mrfx` separately from SPIFF and unrelated APP8 data.
- [ ] `H2` Implement precision-aware inverse HP1, HP2, and HP3 transforms.
- [ ] `H3` Add 8/16-bit, wraparound, and invalid-transform focused tests.
- [ ] `H4` Pass exact Native/Pure decode comparison for all three transforms.
- [ ] `H5` Pass the item acceptance criteria and section 15 completion gate.

### Acceptance criteria

- Redistributable or standards-built `mrfx` fixtures are accepted by the public
  Native decoder and Pure decoder.
- Both decoders produce exact RGB samples for HP1, HP2, and HP3.
- Unrelated APP8 metadata remains ignored.
- Unsupported transform identifiers fail with a managed exception.

## 14. Implementation Order

Implement one alignment item per reviewed change set. Do not combine JPEG and
JPEG 2000 algorithm repairs in one commit.

### Stage 0: Make evidence trustworthy

1. `ALN-TEST-001` reference beta isolation and result classification.
2. Add fixture provenance fields and privacy/license checks needed by later
   stages.
3. Capture a fresh 12-format baseline without hidden skips.

### Stage 1: Highest-risk decode compatibility

1. `ALN-JPEG-001` classic JPEG restart decoding.
2. `ALN-JPEG-001` JPEG-LS restart decoding as a separate change set.
3. `ALN-JPEG-002` multiple scans and per-component Huffman tables.
4. `ALN-J2K-001` SOP/EPH.
5. `ALN-J2K-002` RESET, then VSC.

Restart parsing should precede multiple-scan JPEG because the final scan model
must carry restart state. SOP/EPH should precede RESET/VSC so packet boundaries
are already trustworthy before Tier-1 state changes.

### Stage 2: Encoding and public parameter alignment

1. `ALN-JPEG-003` 16/8 container normalization and 16-bit DQT.
2. `ALN-JPEG-003` 12-bit RGB.
3. `ALN-JPEG-004` smoothing behavior.
4. `ALN-J2K-003` non-LRCP encoding.

### Stage 3: Native extension compatibility

1. `ALN-JLS-001` HP transform decode.
2. Re-run a source-level gap scan before declaring Phase 1 replacement complete.

## 15. Per-Item Completion Gate

An alignment item is complete only when all of the following are true:

- The original failing/unsupported legal fixture passes.
- The focused primitive, frame, and DICOM tests pass.
- The applicable Native-to-Pure and Pure-to-Native rows pass.
- Invalid variants fail with `DicomCodecException`.
- No existing exact lossless/codestream baseline regresses without an approved
  compatibility reason.
- Production assemblies still target only `netstandard2.0`.
- `dotnet build fo-dicom.PureCodecs.slnx -c Release` passes.
- `dotnet test fo-dicom.PureCodecs.slnx -c Release --no-build` passes with no
  failures or skips introduced by the item.
- The process-isolated interoperability runner completes with only ordinary
  passed/failed rows and returns nonzero for any failed row.
- Relevant design/checklist/known-limitations text is updated after the code is
  proven, not before.

## 16. Final Phase 1 Exit Gate

Phase 1 may be described as fully aligned only after:

1. All IDs in section 3.1 are closed or explicitly removed with new evidence.
2. All 12 transfer syntaxes are registered and consumer smoke tests pass.
3. Every lossless cross-stack case has exact decoded samples.
4. Every lossy case has a fixed, justified tolerance and passes in both
   directions.
5. Multi-frame complete-dataset rows pass through normally restored public APIs
   with no version-based classification.
6. Restart, multi-scan, SOP/EPH, RESET/VSC, precision/container, progression,
   smoothing, and HP-transform fixtures remain permanent regression assets.
7. No conclusion relies only on Pure self-round-trip or a document checkbox.
