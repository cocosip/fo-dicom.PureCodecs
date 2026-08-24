# HTJ2K Redevelopment Prompt

Use the following prompt to start a new HTJ2K development session in this
repository.

```text
You are working in D:\Code\dotnet\fo-dicom.PureCodecs on the current branch.
Do not create a branch, worktree, or another working directory.

The repository was intentionally reset to commit
6bc6da845f9c305556b5d4fd4f5a900e3d6fb143. Treat that commit as the only code
baseline. Do not recover, cherry-pick, or reuse implementation code from later
commits or reflog entries.

Before changing code, read these documents completely:

- AGENTS.md
- docs/design/fo-dicom-pure-codecs-design.md
- docs/design/jpeg2000-codec-design.md
- docs/design/htj2k-openjph-alignment-design.md
- docs/development/development-checklist.md

Goal:

Complete the pure C# HTJ2K implementation for DICOM transfer syntaxes .201,
.202, and .203, with the observable behavior of fo-dicom.Codecs 5.16.7 as the
compatibility baseline and ISO/IEC 15444-15 as the algorithmic authority.

Non-negotiable constraints:

1. Production libraries target netstandard2.0 only and codec execution remains
   pure C#.
2. Do not add P/Invoke, a native fallback, native codec DLLs, or runtime native
   library selection to production code.
3. Do not vendor, download, read, copy, translate, compile, link, or directly
   load OpenJPH C/C++ source code or binaries. Do not use OpenJPH source files,
   tables, or control flow as templates for C# implementation.
4. Reference generation and interoperability checks must call only the public
   .NET codec API exposed by the fo-dicom.Codecs 5.16.7 NuGet package. Use its
   normal NuGet runtime-asset resolution. Do not add DllImportResolver,
   NativeLibrary.Load, local DLL scanning, an OpenJPH checkout, CMake, or a
   native build step.
5. Record reference provenance as:
   - fo-dicom.Codecs package: 5.16.7
   - fo-dicom.Codecs release commit:
     1d05c6cca14883d06b835f8dadca5dae7d97577c
   - codestream-reported OpenJPH version: 0.21.2
6. Preserve classic JPEG 2000 .90/.91 behavior and all non-HTJ2K codecs.
7. Keep public fo-dicom integration and the one-package assembly layout defined
   by AGENTS.md.
8. Preserve unrelated user changes. Do not rewrite Git history or push.

Required working method:

1. Audit the code at the reset baseline before proposing implementation. Trace
   the current .201/.202/.203 encode and decode paths, parameter handling,
   registration, packet/tile logic, tests, fixtures, and CI. Identify unsupported
   behavior and any existing code whose provenance violates the constraints.
2. Present a concrete gap list mapped to the focused design document. Separate
   standards gaps, fo-dicom.Codecs compatibility gaps, DICOM integration gaps,
   invalid-input gaps, and CI/test gaps.
3. Produce a staged implementation plan. Each stage must have exact files,
   observable acceptance criteria, and focused test commands. Wait for user
   confirmation before beginning implementation.
4. Use TDD for every behavior change: add one focused failing test, run it and
   confirm the expected failure, implement the smallest fix, then rerun focused
   and broader regression tests.
5. Generate frozen reference artifacts only through a process-isolated .NET
   worker that instantiates fo-dicom.Codecs codec classes. Manifests must include
   package/version provenance, parameters, raw hashes, codestream hashes,
   decoded hashes or lossy metrics, and marker summaries.
6. Compare extracted encapsulated frame codestreams, not whole DICOM file
   hashes. Require exact bytes for deterministic default lossless/reference
   cases, exact decoded samples for lossless interop, and frozen maximum error,
   MAE, PSNR, and compression-ratio bounds for lossy interop.
7. Implement HTJ2K production logic from ISO/IEC 15444-15 concepts and the
   repository's existing managed JPEG 2000 abstractions. Black-box reference
   output may establish expected behavior, but must not be reverse-translated
   from OpenJPH source.
8. Keep mutable encoder/decoder state frame-scoped. Validate sizes and marker
   lengths before allocation or indexing. Public failures must be bounded,
   frame-scoped DicomCodecException instances without patient or pixel data.
9. Commit only coherent, verified stages using English Conventional Commit
   messages. Never commit generated build output, native binaries, temporary
   source trees, or local machine paths.

Development order:

1. .NET-only reference generator, manifest, and structured codestream diff.
2. Immutable resolved parameter profiles and exact DICOM wrapper behavior.
3. .201 lossless stages and deterministic reference alignment.
4. .202 RPCL progression and tile-part alignment.
5. .203 irreversible transform, quantization, quality metrics, and rate/layer
   extensions kept distinct from default reference parity.
6. Reference-to-Pure decode coverage, multi-tile/tile-part handling, and an
   explicit supported-marker audit.
7. Complete DICOM matrix, malformed-input matrix, and process-isolated two-way
   interoperability workers.
8. Full regression, package-content audit, documentation reconciliation, then
   performance work only after correctness gates are stable.

Verification rules:

- Run dotnet build/test outside the Codex sandbox as required by AGENTS.md.
- Run focused HTJ2K tests after each red/green step.
- Before each commit, run the affected JPEG 2000 suite and git diff --check.
- Before declaring HTJ2K complete, run the full solution tests and Release
  build, the frozen baseline verifier, both interoperability directions for all
  three syntaxes, package inspection, and consumer smoke tests.
- Report focused success separately from unrelated or environment-dependent
  failures. Do not claim parity from self-round-trips, decoded non-zero checks,
  similar file sizes, or a subset of fixtures.

Your first response must contain only the evidence-backed current-state audit,
the gap list, and the staged plan. Do not modify code in the first response.
```
