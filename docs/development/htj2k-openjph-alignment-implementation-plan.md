# HTJ2K OpenJPH Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the managed HTJ2K `.201`, `.202`, and `.203` alignment with `fo-dicom.Codecs`/OpenJPH without changing the established OpenJPEG-compatible behavior of classic JPEG 2000 `.90` and `.91`.

**Architecture:** Keep marker models and bounded codestream parsing shared, but route observable arithmetic, parameters, validation, and reference behavior through explicit classic or HT profiles. Implement OpenJPH-compatible normalized float input, ICT, 9/7, quantization, and cleanup behavior only in the HT path. Run native reference codecs only in bounded worker processes.

**Tech Stack:** C# with repository `LangVersion=latest`, .NET 10 tests/tools, `netstandard2.0` production libraries, xUnit v3, fo-dicom 5.2.6, fo-dicom.Codecs 5.16.7 reference workers.

**Spec:** `docs/design/jpeg2000-openjpeg-openjph-separation-design.md`

## Global Constraints

- Production projects continue to target `netstandard2.0` only.
- Production code remains pure C# and must not load OpenJPEG, OpenJPH, or any native codec library.
- Classic JPEG 2000 `.90/.91` retains OpenJPEG-compatible transform, quantization, packet, rate-control, and validation behavior.
- HTJ2K `.201/.202/.203` uses explicit OpenJPH-compatible entry points; no boolean default may silently select a family.
- Lossless interoperability requires exact decoded bytes; default reference codestream tests require exact logical codestream bytes.
- Native reference work runs in a bounded child process with captured output, timeout, and process-tree termination.
- Work in the current checkout and preserve unrelated user changes. Do not create a branch, worktree, or commit unless requested.

---

### Task 1: OpenJPH-Compatible RGB Input and ICT

**Files:**
- Modify: `src/fo-dicom.PureCodecs.Jpeg2000/Internal/Jpeg2000HtTileEncoder.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000HtCodecRoundTripTests.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceAlignmentTests.cs`

**Interfaces:**
- Consumes: interleaved DICOM RGB samples and HT lossy `codingBitDepth`.
- Produces: HT-only normalized `float` components using OpenJPH operation order before `Forward97HighThroughput`.

- [x] Add a focused test with literal OpenJPH-derived `Y`, `Cb`, and `Cr` float bit patterns for selected RGB samples.
- [x] Run the focused test and confirm it fails because the current path level-shifts integers, applies a direct ICT matrix, and scales afterward.
- [x] Replace the HT lossy preparation with one HT-only conversion that computes `(sample - half) * (1f / 2^precision)` first, then computes `Y`, `Cb`, and `Cr` using OpenJPH's `Y`-derived formulas and float operation order.
- [x] Run the focused arithmetic test and the RGB `.203` reference test; require a byte-identical 3369-byte logical codestream.
- [x] Run all `Jpeg2000Ht*` and `Htj2k*` tests.
- [x] Run all classic `Jpeg2000Classic*`, `Jpeg2000ExternalAcceptanceTests`, and `Jpeg2000StandardInternalTests` as a no-regression gate.

### Task 2: HT Parameters Fail Before Frame Access

**Files:**
- Modify: `src/fo-dicom.PureCodecs.Jpeg2000/Internal/DicomHtJpeg2000CodecBase.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000DicomIntegrationTests.cs`

**Interfaces:**
- Consumes: `DicomHtJpeg2000Params` from `Encode`.
- Produces: `ValidateParameters(DicomHtJpeg2000Params)` enforcing one HT layer and a target ratio of zero or a finite value greater than one.

- [x] Add rejection cases for `NumLayers != 1`, NaN, infinities, negative ratios, one, and values in `(0, 1]`; add acceptance cases for zero and a finite value greater than one. Use pixel data with no frame so validation ordering is observable.
- [x] Run the focused tests and confirm invalid parameters currently reach frame access or are silently accepted.
- [x] Call HT parameter validation before progression resolution, tolerance calculation, metadata mutation, and frame access.
- [x] Require `DicomCodecException` messages to identify the HT parameter and accepted range.
- [x] Run the focused tests and all JPEG 2000 integration tests.

### Task 3: Explicit Classic and HT Decode Profiles

**Files:**
- Modify: `src/fo-dicom.PureCodecs.Jpeg2000/Internal/Standard/Jpeg2000StandardFrameDecoder.cs`
- Modify: `src/fo-dicom.PureCodecs.Jpeg2000/Internal/Jpeg2000ClassicFrameCodec.cs`
- Modify: `src/fo-dicom.PureCodecs.Jpeg2000/Internal/Jpeg2000HtFrameCodec.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000DicomIntegrationTests.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000HtNativeCompatibilityTests.cs`

**Interfaces:**
- Produces: `Jpeg2000DecodeProfile.ClassicOpenJpeg` and `Jpeg2000DecodeProfile.HighThroughputOpenJph` passed explicitly into standard frame decoding.
- Classic profile: SIZ precision must match DICOM `BitsStored`.
- HT profile: may accept the reference 12-in-16 SIZ container form. Reversible samples must fit the signed or unsigned DICOM `BitsStored` range; irreversible reconstruction overshoot is clipped before packing.

- [x] Add a classic test proving the OpenJPH 12-in-16 exception is rejected for `.90/.91`.
- [x] Add HT signed and unsigned lossless fixtures whose decoded value exceeds the DICOM stored range and assert a managed exception.
- [x] Add an HT lossy fixture proving irreversible reconstruction overshoot is clipped to the DICOM stored range.
- [x] Run the new tests and confirm the previous shared validation accepted or clamped invalid lossless cases and rejected valid lossy overshoot.
- [x] Introduce the explicit enum/profile parameter and move the precision exception behind the HT profile.
- [x] Enforce `[-2^(BitsStored-1), 2^(BitsStored-1)-1]` or `[0, 2^BitsStored-1]` according to reversible or irreversible decode semantics.
- [x] Run the new tests, classic decode tests, and HT 12-bit interoperability tests.

### Task 4: Independent Reference Manifests and Real Provenance

**Files:**
- Modify: `tools/fo-dicom.PureCodecs.Htj2kReference/Program.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceAlignmentTests.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceDiffTests.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceWorkerTests.cs`

**Interfaces:**
- Produces: independently constructed reference and Pure `Htj2kReferenceManifest` values.
- Comparison fields: raw-frame hash, decoded-frame hash, codestream hash, logical length, marker summary, frame index/count, transfer syntax, effective parameters, and provenance.
- Provenance reads the loaded `fo-dicom.Codecs` assembly version and codestream-reported OpenJPH comment where present.

- [x] Add diff tests that mutate each required manifest field independently and assert a mismatch naming that field.
- [x] Add worker tests that compare emitted package provenance with the loaded assembly and reject an unexpected package version.
- [x] Run the tests and confirm the current comparer and hard-coded provenance miss these changes.
- [x] Extend the comparer and manifest builder with explicit field comparisons.
- [x] Build the Pure manifest from Pure input, output, and decoded data instead of `expected with` replacement.
- [x] Derive reference versions from runtime evidence and reject missing or unexpected provenance.
- [x] Run the manifest, diff, worker, and reference-alignment tests.

### Task 5: Process-Isolate Every Native HTJ2K Call

**Files:**
- Modify: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceAlignmentTests.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000HtNativeCompatibilityTests.cs`
- Modify: `tools/fo-dicom.PureCodecs.Htj2kReference/Program.cs`
- Modify: `tools/fo-dicom.NativeCodecs.Tools/Program.cs`
- Test: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceWorkerTests.cs`

**Interfaces:**
- Produces: worker commands for native encode/decode that accept input/output paths, syntax, parameters, and manifest path.
- Worker execution contract: redirected stdout/stderr, 120-second timeout, nonzero exit propagation, and entire-process-tree termination.

- [x] Add worker contract tests for success, invalid arguments, nonzero native failure, and timeout handling.
- [x] Run tests and confirm direct calls can execute in the xUnit process.
- [x] Restore reference alignment to `dotnet <worker.dll> --worker ...` and remove direct `Htj2kReferenceWorkerProgram.Run` calls from xUnit.
- [x] Route all methods in `Jpeg2000HtNativeCompatibilityTests` through the worker protocol.
- [x] Assert the ordinary test process has not loaded the `fo-dicom.Codecs` native codec assembly after worker-based tests.
- [x] Run the worker and all HT native compatibility tests.

### Task 6: True Multi-Frame Interoperability

**Files:**
- Modify: `tools/fo-dicom.PureCodecs.Htj2kReference/Program.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Jpeg2000HtNativeCompatibilityTests.cs`
- Modify: `tests/fo-dicom.PureCodecs.Tests/Htj2kReferenceWorkerTests.cs`

**Interfaces:**
- Consumes: the complete multi-frame `sample-05` dataset in one codec call.
- Produces: ordered per-frame codestream and decoded hashes plus frame count and DICOM metadata.

- [x] Add a worker test requiring one command to return every frame in order.
- [x] Exercise complete multi-frame native/Pure transcodes in each direction.
- [x] Assert frame count, frame ordering, exact lossless bytes, dimensions, precision, signedness, and transfer syntax.
- [x] Run the multi-frame test for `.201` and `.202` in both directions and record the reference wrapper difference.

The complete native-to-Pure calls are byte-exact. The reverse complete-dataset
call reproduces a `fo-dicom.Codecs 5.16.7` difference at frame 1 byte 32400,
while the same Pure codestreams decode byte-exact in individual native calls.
The release checklist keeps the complete reverse-direction gate open.

### Task 7: Documentation and Final Gates

**Files:**
- Modify: `docs/design/jpeg2000-openjpeg-openjph-separation-design.md`
- Modify: `docs/design/htj2k-openjph-alignment-design.md`
- Modify: `docs/development/development-checklist.md`
- Modify: `docs/development/phase-1-alpha-release-notes.md`

**Interfaces:**
- Produces: release statements matching verified behavior and no stale claim that HTJ2K native interoperability is optional.

- [x] Update design/checklist status only for gates actually completed in this worktree.
- [x] Run focused HTJ2K tests.
- [x] Run focused classic JPEG 2000 tests.
- [x] Run `dotnet build fo-dicom.PureCodecs.slnx -c Release --no-restore`.
- [x] Run `dotnet test fo-dicom.PureCodecs.slnx -c Release --no-build`.
- [x] Run the `.201`, `.202`, and `.203` reference worker matrix in both encode/decode directions.
- [x] Run modern and .NET Framework consumer smoke tests with direct project references.
- [x] Pack the NuGet package and inspect that production DLLs target only `netstandard2.0` and no native codec binary is present.
- [x] Report any unrun gate separately; do not convert an unverified item into a completion claim.
