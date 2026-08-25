# JPEG 2000 OpenJPEG and OpenJPH Compatibility Separation Design

## Status

This is a durable compatibility design document for the JPEG 2000 codec family.
It is authoritative for deciding which behavior may be shared between classic
JPEG 2000 and HTJ2K and which behavior must remain reference-family specific.

It complements:

- [`jpeg2000-codec-design.md`](jpeg2000-codec-design.md), which defines the
  complete JPEG 2000 family architecture.
- [`htj2k-openjph-alignment-design.md`](htj2k-openjph-alignment-design.md),
  which defines the HTJ2K reference and release matrix.

When these documents appear to conflict about algorithm reuse, this document is
authoritative for the OpenJPEG/OpenJPH separation boundary.

## Purpose

`fo-dicom.Codecs` does not use one native implementation for all five transfer
syntaxes:

| Transfer syntax | UID suffix | Compatibility family |
| --- | --- | --- |
| JPEG 2000 Lossless | `.90` | `fo-dicom.Codecs` through OpenJPEG |
| JPEG 2000 Lossy | `.91` | `fo-dicom.Codecs` through OpenJPEG |
| HTJ2K Lossless | `.201` | `fo-dicom.Codecs` through OpenJPH |
| HTJ2K Lossless RPCL | `.202` | `fo-dicom.Codecs` through OpenJPH |
| HTJ2K Lossy | `.203` | `fo-dicom.Codecs` through OpenJPH |

The codestream families share ISO/IEC 15444 concepts, but the observable
reference behavior is not interchangeable. Shared standard infrastructure must
not force OpenJPEG arithmetic, defaults, normalization, packet policy, or
validation exceptions onto HTJ2K. The reverse is also prohibited.

The production implementation remains pure C#, targets `netstandard2.0`, and
must not load or call either native library. Reference-family names describe
compatibility behavior, not runtime dependencies.

OpenJPEG/OpenJPH C/C++ source is a read-only research reference. Executable
interoperability and encoded-image comparison must be implemented in C# and
must reach the reference implementation only through normally restored
`fo-dicom`/`fo-dicom.Codecs` NuGet package APIs. Local DLL paths, binary
replacement, `HintPath`, and source/project references to upstream checkouts
are prohibited. A local upstream change must first be published as a complete
NuGet package to a package source and then consumed through `PackageReference`.

## Compatibility Principle

Share representation and bounded parsing where the standard contract is truly
identical. Separate policy and arithmetic wherever OpenJPEG and OpenJPH can
produce different observable results.

The following rule applies to every proposed shared helper:

1. Identify the standard data structure or operation represented by the helper.
2. Compare the current `.NET` behavior of the two `fo-dicom.Codecs` families.
3. Share the implementation only when structure, numeric convention, defaults,
   validation, and output are all equivalent for the supported profile.
4. Otherwise expose explicit classic and HTJ2K entry points, even if they call a
   private common primitive internally.
5. Lock both sides with independent reference fixtures before refactoring.

A boolean whose default silently selects one reference family's behavior is not
an acceptable public internal boundary. Prefer named classic and HTJ2K methods
or strategy types.

## Confirmed Alignment Failures

The 2026-08-24 review established the following baseline before repair:

- Release build succeeded with zero warnings and zero errors.
- Full test run: 784 passed and 3 failed out of 787.
- Focused HTJ2K run: 74 passed and 3 failed out of 77.
- `.201` and `.202` passed both interoperability directions for five fixtures.
- `.203` Pure-to-reference decode passed for the first signed 16-bit fixture.
- `.203` reference-to-Pure decode failed at sample zero: expected 590, actual 0.
- Default `.203` monochrome codestream differed first at header offset 142.
- Default `.203` RGB codestream differed first at codestream offset 903.

## Implementation Snapshot (2026-08-24)

The alignment work after that baseline now verifies:

- The HT normalized float sample import and ICT operation order repair remains
  covered by production codec tests. The former EOC-trimmed `.203` byte
  comparison is no longer a valid interoperability gate.
- Classic `.90/.91` encoding remains on the existing OpenJPEG-compatible path;
  the classic focused regression gate passes after the HT arithmetic changes.
- HT parameters reject unsupported `NumLayers` and invalid `TargetRatio` values
  before frame access.
- Decode profiles are explicit. Classic requires SIZ precision equal to
  `BitsStored`; HT alone may accept 12-in-16 SIZ precision. Reversible samples
  outside the declared stored range are rejected, while irreversible
  reconstruction overshoot is clipped to the declared range.
- Reference manifests contain transfer syntax, frame count, frame indexes, raw
  frame hashes, public encoded-frame hashes/lengths, and decoded-frame hashes.
- Reference and Native HTJ2K operations run in bounded child processes with
  redirected output, a 120-second timeout, nonzero propagation, and process-tree
  termination.
- Complete `.201/.202/.203` multi-frame calls are covered in both directions.
  Native-to-Pure passes; all three Pure-to-Native rows currently fail on frame
  1. They remain ordinary failed rows and are not reclassified by dependency
  version, commit, or single-frame diagnostics.

The investigation then confirmed these separate defects:

### OpenJPH Irreversible Decode Scale

HT cleanup decoding produced non-zero sign-magnitude coefficients, but inverse
quantization reconstructed normalized values near `0.009` for a source sample
near `590`. The HTJ2K decode path omitted the component precision scale
`2^precision` when returning from OpenJPH-style normalized coefficient space to
the DICOM sample domain.

This scale belongs only to the HTJ2K/OpenJPH irreversible path. It must not be
inserted into classic OpenJPEG inverse quantization.

### OpenJPEG and OpenJPH 9/7 Normalization

The shared inverse 9/7 implementation applied the classic OpenJPEG high-frequency
normalization to HTJ2K. After the precision-scale repair, this still produced a
maximum source error of 1343. Selecting the OpenJPH normalization reduced the
maximum error to 2 and made the Pure result differ from the reference decode by
at most 1.

Classic JPEG 2000 must retain its OpenJPEG normalization. HTJ2K must call an
explicit OpenJPH normalization entry point.

### HT Cleanup Inclusion Threshold

The HTJ2K irreversible encoder treated every non-zero fixed-point magnitude as
an included code-block. The cleanup pass only represents coefficients that are
significant at its resolved CUP bit-plane. Sub-threshold blocks were therefore
written as five-byte empty cleanup payloads instead of being omitted.

Matching inclusion to the cleanup bit-plane threshold made the deterministic
monochrome `.203` codestream byte-identical to the reference, including TLM and
SOT lengths.

### Resolved RGB `.203` Difference

The two remaining RGB codestream byte differences were traced to HT input
normalization and ICT operation order. The HT-only color transform now
normalizes each source sample before applying the OpenJPH-compatible formulas.
The prior trimmed comparison matched a 3369-byte logical codestream. That result
is historical diagnostic evidence only; the current public-frame gate does not
trim reference output. Classic JPEG 2000 color handling is unchanged.

## Current Verification (2026-08-25)

- Full Release suite: 806 passed, 0 failed, 0 skipped.
- Release build: 0 warnings and 0 errors.
- The complete 12-format interoperability matrix returned nonzero: 4 workers
  passed, 8 failed, with 104 of 112 complete-dataset direction rows passing.
- `.201`, `.202`, and `.203` Native-to-Pure rows pass. Their Pure-to-Native
  seven-frame rows fail on frame 1 and remain failed.
- Modern and .NET Framework 4.7.2 smoke applications passed using direct
  project references.
- `FoDicom.PureCodecs.0.4.0.nupkg` contains only the five managed production
  assemblies under `lib/netstandard2.0` and no runtime or native codec payload.

### Reference package boundary

Reference projects restore `fo-dicom.Codecs` through normal NuGet
`PackageReference` and call only its public C# API. Validation does not inspect
the resolved package version, release commit, assembly metadata, codestream
implementation version, or `.deps.json`. Complete-dataset behavior is the only
gate: each direction either passes its pixel, frame, and tag assertions or fails
with a nonzero worker exit.

## Required Separation Matrix

| Stage | Shared standard infrastructure | Classic `.90/.91` policy | HTJ2K `.201/.202/.203` policy |
| --- | --- | --- | --- |
| DICOM frame validation | Frame length, dimensions, layout primitives | OpenJPEG-compatible parameter and signed-value behavior | OpenJPH-compatible precision/container behavior |
| SIZ and basic markers | Bounded big-endian parsing and model types | Classic capabilities and padding rules | CAP, TLM, and HT profile requirements |
| Coding defaults | Model types only | OpenJPEG resolution, layer, rate, MCT, and packet defaults | OpenJPH decomposition, progression, TLM, and default parameter behavior |
| Component transform | Component storage and layout helpers | OpenJPEG RCT/ICT arithmetic and source photometric normalization | OpenJPH RCT/ICT arithmetic and ordering |
| Reversible DWT | Geometry primitives may be shared after proof | OpenJPEG 5/3 edge and origin behavior | OpenJPH 5/3 edge and origin behavior |
| Irreversible DWT | Buffer and geometry scaffolding only | OpenJPEG `OPJ_FLOAT32` lifting and normalization | OpenJPH float lifting and normalization |
| Quantization | QCD syntax and subband indexing | OpenJPEG step generation, band depth, dead-zone, and PCRD inputs | OpenJPH base delta, gains, `Kmax`, fixed-point scale, and CUP threshold |
| Code-block coding | Code-block geometry model | MQ/EBCOT Tier-1 passes | MEL, VLC, MagSgn, cleanup and refinement passes |
| Packet coding | Packet coordinates and bounded byte I/O | OpenJPEG tag-tree, layer contribution, and empty-packet behavior | OpenJPH HT inclusion, pass lengths, progression, and tile-part division |
| Rate control | Public validation style | OpenJPEG rate levels and PCRD distribution | Reject unsupported layers; keep Pure target-ratio behavior distinct |
| Decode precision | Common range utilities | Strict SIZ precision versus `BitsStored` | Bounded Native 12-in-16 compatibility exception plus range enforcement |
| Encapsulation | Logical codestream extraction | OpenJPEG DICOM item padding behavior | OpenJPH logical EOC and HT frame behavior |

## Code Boundaries

### Shared Structural Layer

These areas may remain shared when tests prove identical semantics:

- Marker-safe byte readers and writers.
- SOC/EOC scanning and logical codestream extraction.
- Bounded marker length parsing.
- SIZ, COD, QCD, SOT, and SOD model representation.
- Tile, component, resolution, precinct, subband, and code-block geometry.
- Progression-order coordinate models.
- DICOM frame shape and metadata utilities.

Shared code must not embed one reference family's default values.

### Classic OpenJPEG Compatibility Layer

Classic-only behavior is owned by the classic codec adapter, classic frame
encoder/decoder, classic Tier-1 implementation, classic packet implementation,
and OpenJPEG-compatible rate-control logic.

Changes in this layer require `.90` and `.91` reference gates. An HTJ2K test is
not evidence that a classic change is correct.

### HTJ2K OpenJPH Compatibility Layer

HT-only behavior is owned by the HT codec adapter, HT frame/tile encoder, HT
block codec, HT packet assembly, OpenJPH-compatible irreversible quantization,
and HT-specific transform entry points.

Changes in this layer require `.201`, `.202`, and `.203` gates. A classic JPEG
2000 test is not evidence that an HTJ2K change is correct.

### Transform API

The irreversible wavelet implementation must expose distinct entry points:

```text
Forward97                 classic/OpenJPEG contract
Inverse97                 classic/OpenJPEG contract
Forward97HighThroughput   HTJ2K/OpenJPH contract
Inverse97HighThroughput   HTJ2K/OpenJPH contract
```

The named methods may reuse private lifting primitives only when their numeric
behavior is intentionally parameterized and independently tested. HT callers
must never reach the classic method by default or fallback.

The same explicit separation is required if investigation finds family-specific
5/3 edge behavior, ICT rounding, QCD generation, or sample packing.

## Parameter and Validation Separation

Classic JPEG 2000 retains the established OpenJPEG-compatible contract for
`Rate`, `RateLevels`, `TargetRatio`, `NumLayers`, MCT, signed encoding, and
optional final lossless layers.

HTJ2K uses a separate contract:

- `NumLayers` must equal one until real HT packet-layer contributions exist.
- `TargetRatio` must be zero or a finite value greater than one.
- Invalid values fail with `DicomCodecException` before frame processing.
- `.202` keeps its required RPCL transfer-syntax behavior.
- Reference-ignored parameters must not silently claim an effect.

The reference HTJ2K 12-bit-in-16 SIZ precision exception must be selected by an
HTJ2K decode profile, not by a generic JPEG 2000 relaxation. Reversible decoded
values must fit the DICOM `BitsStored` range; irreversible reconstruction
overshoot is clipped to that range before packing.

## Test Strategy

### Independent Reference Gates

Classic and HTJ2K tests are separate release gates:

- Classic `.90/.91`: `fo-dicom.Codecs`/OpenJPEG reference codestreams,
  bidirectional pixel interoperability, rate-layer distribution, and classic
  malformed-input coverage.
- HTJ2K `.201/.202/.203`: `fo-dicom.Codecs`/OpenJPH reference codestreams,
  bidirectional pixel interoperability, HT pass/packet coverage, and HT-specific
  malformed-input coverage.

Running only the combined JPEG 2000 test set does not replace reporting the two
family results independently.

### Cross-Family Regression Gates

Every change to a file used by both families must run:

1. The focused failing reference test.
2. All HTJ2K tests.
3. All classic JPEG 2000 tests.
4. The five-syntax interoperability matrix.
5. The full solution tests and Release build before completion.

Classic tests must include a case proving that HT-specific 12-in-16 precision
acceptance remains rejected for `.90/.91`. HT tests must include Native
12-in-16 decode, reversible out-of-range rejection, and irreversible overshoot
clipping.

### Intermediate Evidence

Exact output failures are diagnosed in this order:

1. Imported component samples and level shift.
2. RCT or ICT results.
3. Per-resolution DWT coefficients.
4. QCD steps, `Kmax`, missing MSBs, and fixed-point quantized values.
5. Code-block pass counts, segment lengths, and payload hashes.
6. Packet inclusion, tag-tree state, contribution lengths, and tile-part split.
7. Marker values and complete logical codestream bytes.
8. Decoded sample metrics.

Reference tools record only public reference artifacts. Production codec tests
may inspect Pure internal stages for algorithm diagnosis, but interoperability
results come only from complete public-path behavior.

## Development Sequence

### Phase 1: Freeze the Separation Boundary

- Keep explicit classic and HTJ2K transform entry points.
- Add focused unit tests for both 9/7 normalization contracts.
- Scope 12-in-16 precision acceptance to HTJ2K.
- Add a source-level or assembly-level guard against HT codecs calling classic
  compatibility entry points.

### Phase 2: Complete `.203` Alignment

- Preserve the repaired reference-to-Pure precision scale.
- Preserve OpenJPH inverse 9/7 normalization.
- Preserve cleanup bit-plane inclusion filtering.
- Locate the two remaining RGB codestream bytes through intermediate snapshots.
- Require exact deterministic mono and RGB codestream equality.
- Re-run signed 16-bit reference-to-Pure and Pure-to-reference decode.

### Phase 3: Parameter and Precision Contracts

- Reject unsupported HT layer counts and invalid target ratios before reading a
  frame.
- Add classic strict-precision and HT compatibility-precision tests.
- Reject reversible HT decoded values outside the declared stored range and
  clip irreversible reconstruction overshoot.

### Phase 4: Reference Infrastructure

- Compare every behavioral manifest field, including transfer syntax, raw hash,
  decoded hash, logical length, effective parameters, marker summary, and metrics.
- Do not inspect dependency or implementation versions to classify behavior.
- Isolate every Native operation in a bounded worker process.
- Validate multi-frame input with one complete codec call per direction.

### Phase 5: Frozen Matrix and Documentation

- Commit only redistributable, de-identified reference fixtures and their
  manifest under test fixture directories.
- Cover precision, signedness, layout, frame count, odd dimensions, 64-boundary
  geometry, representative large frames, and malformed inputs.
- Reconcile README, checklist, limitations, and family design claims.
- Do not mark HTJ2K complete until all three syntaxes are required release gates.

## Resume Procedure

A future developer or AI must use this sequence. Do not skip directly to a
speculative transform change.

1. Read `AGENTS.md`, this entire document, `jpeg2000-codec-design.md`, and
   `htj2k-openjph-alignment-design.md`.
2. Run `git status --short`, inspect every existing diff, and preserve the
   current working-tree repairs. Confirm that `docs/superpowers/` has no tracked
   files.
3. Rebuild once before using `--no-build`. Reproduce the single RGB `.203`
   reference-alignment failure and record its first differing byte, tile-part,
   and decoded difference count.
4. Add test-only intermediate snapshots at the first divergent tile/code-block.
   Production code must not retain temporary console or file diagnostics.
5. Identify the first semantic divergence before changing arithmetic. Make the
   smallest HT-specific correction and keep classic behavior unchanged.
6. Run the focused RGB failure, all HTJ2K tests, and all classic JPEG 2000 tests
   after each shared-file change.
7. Complete parameter, precision, manifest, worker-isolation, and multi-frame
   tasks only after exact RGB `.203` parity is understood, unless a task is
   needed to obtain reliable diagnostic evidence.
8. Run the final gates and update this handoff snapshot with actual results.
   Remove resolved items rather than leaving ambiguous completion claims.

Use these commands from the repository root. In the Codex app, follow
`AGENTS.md` and run all `dotnet` commands outside the sandbox.

```powershell
git status --short
git diff --check
git ls-files -- 'docs/superpowers/*'

dotnet build fo-dicom.PureCodecs.slnx -c Release

dotnet test tests/fo-dicom.PureCodecs.Tests/fo-dicom.PureCodecs.Tests.csproj `
  -c Release --no-build `
  --filter "FullyQualifiedName~Htj2k|FullyQualifiedName~Jpeg2000Ht"

dotnet test tests/fo-dicom.PureCodecs.Tests/fo-dicom.PureCodecs.Tests.csproj `
  -c Release --no-build `
  --filter "FullyQualifiedName~Jpeg2000&FullyQualifiedName!~Ht"

dotnet test fo-dicom.PureCodecs.slnx -c Release --no-build --verbosity normal

dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation `
  -c Release --no-build -- --worker htj2k-lossless
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation `
  -c Release --no-build -- --worker htj2k-lossless-rpcl
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation `
  -c Release --no-build -- --worker htj2k-lossy
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation `
  -c Release --no-build -- --parallel 4 --worker-timeout-seconds 300

.\eng\Verify-PackageConsumerSmoke.ps1 -RequireNet472
```

If a filter selects an unexpected test count, list the selected tests and fix
the filter before treating the run as a gate. Report classic and HTJ2K counts
separately.

## AI Handoff Prompt

The document is intended to be sufficient context for continuation. For a new
AI session, provide the repository and this prompt:

```text
Work in the fo-dicom.PureCodecs repository. First read AGENTS.md and then read
docs/design/jpeg2000-openjpeg-openjph-separation-design.md completely. Treat
that design document as the authoritative compatibility boundary and current
handoff state. Also read the two focused design documents it links.

Continue the unfinished JPEG 2000/HTJ2K alignment work from the existing dirty
worktree. Do not revert, discard, or overwrite current changes, and do not
commit or push unless I explicitly ask. Never add docs/superpowers/* to Git.

Classic JPEG 2000 .90/.91 must align with fo-dicom.Codecs/OpenJPEG behavior.
HTJ2K .201/.202/.203 must align with fo-dicom.Codecs/OpenJPH behavior. Shared
data structures are allowed, but do not force arithmetic, normalization,
quantization, precision exceptions, defaults, parameters, packet policy, or
rate control from one family onto the other.

Start by inspecting git status, the current diffs, and the 2026-08-24
implementation snapshot above. Preserve exact default `.201/.202/.203`
codestream alignment, the explicit decode profiles, HT parameter validation,
independent manifests, bounded workers, and classic `.90/.91` behavior. Do not
widen tolerances, patch output bytes, or rely on Pure self-roundtrip as external
compatibility evidence.

Continue only the unchecked checklist and final gates. Run the process-isolated
interop matrix with normally restored packages; do not classify failures by
package version or commit. Run both complete-call directions and leave every
failed row failed.
Report exact commands, counts, failures, and any gate that could not run. Do
not claim completion while a required gate is failing or unexecuted.
```

## Prohibited Shortcuts

- Do not widen lossy tolerance to hide a known transform or packet mismatch.
- Do not make classic behavior conditional on an HT fixture without an explicit
  family strategy.
- Do not apply an HT precision exception to the shared decoder globally.
- Do not patch final codestream bytes or hashes.
- Do not use Pure self-roundtrip as evidence of reference compatibility.
- Do not run Native codecs inside the ordinary xUnit process.
- Do not inspect or copy OpenJPH native implementation source.
- Do not add P/Invoke, native fallback, or a production native dependency.
- Do not store implementation plans under `docs/superpowers/` in Git.

## Completion Criteria

The separation work is complete only when:

- Classic `.90/.91` tests prove their OpenJPEG-compatible behavior is unchanged.
- HTJ2K `.201/.202/.203` exact and interoperability gates pass.
- Family-specific transform, quantization, precision, parameter, and packet
  policies have explicit entry points or strategy ownership.
- No shared default silently selects OpenJPEG behavior for HTJ2K or OpenJPH
  behavior for classic JPEG 2000.
- All behavioral manifest comparisons are independent and complete.
- Native tests are process-isolated and real multi-frame calls are covered.
- Focused tests, full tests, Release build, worker matrix, consumer smoke tests,
  and package inspection pass.
