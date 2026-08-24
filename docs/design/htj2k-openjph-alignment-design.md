# HTJ2K fo-dicom.Codecs Alignment Design

This focused design extends
[`jpeg2000-codec-design.md`](jpeg2000-codec-design.md). The family design remains
authoritative for shared JPEG 2000 architecture; this document is authoritative
for ongoing HTJ2K alignment with the observable .NET behavior of
`fo-dicom.Codecs`. OpenJPH is reached only through that package's .NET codec
API; it is not a source or build dependency of this repository.

The classic OpenJPEG versus HTJ2K OpenJPH implementation boundary is defined in
[`jpeg2000-openjpeg-openjph-separation-design.md`](jpeg2000-openjpeg-openjph-separation-design.md).
That document is authoritative whenever shared family code could impose one
reference library's arithmetic or policy on the other family.

## Purpose

Complete the managed HTJ2K implementation for transfer syntaxes `.201`, `.202`,
and `.203` by aligning its observable codec behavior with `fo-dicom.Codecs`.

The current deterministic reference baseline is:

- Reference NuGet package: `fo-dicom.Codecs 6.0.0-beta1`
- Reference release commit: `fc2df0efaa9acdee7b3640f821665107630933e8`
- Codestream-reported OpenJPH version: `0.30.1`

The package version, release commit, and codestream-reported OpenJPH version
must be recorded in generated fixture manifests. Runtime reference generation
accepts a supported version range (`fo-dicom.Codecs >= 6.0.0-beta1`) and records the
actual provenance; it does not require one exact assembly version or commit.
If a newer package changes deterministic output, frozen baselines must be
regenerated and reviewed explicitly rather than silently accepting new output.

## Verified Alignment Status (2026-08-24)

- Default `.201`, `.202`, and RGB/monochrome `.203` reference cases have exact
  logical-codestream alignment with `fo-dicom.Codecs 6.0.0-beta1`.
- Runtime provenance is read from the loaded package informational version and
  the codestream COM marker, then checked against the frozen baseline.
- Invalid HT extension parameters fail before frame access, and the 12-in-16
  SIZ precision exception is restricted to the HT decode profile. Reversible
  out-of-range samples are rejected; irreversible reconstruction overshoot is
  clipped to the DICOM stored range.
- All HT native reference calls in the alignment, compatibility, and tool
  regression tests execute in bounded child processes.
- Complete multi-frame `.201/.202/.203` calls are covered in both directions.
  Native-to-Pure is exact for lossless and within tolerance 8 for lossy. The
  current 6.0.0-beta1 wrapper differs only in the Pure-to-native complete call:
  frame 1 byte 32400 for `.201/.202`, and byte 31312 for `.203`; individual
  native calls are correct. This is caused by returning pooled arrays after
  exposing them to output buffers. Upstream commit `56a2da0` copies encoded
  and decoded frames before returning the arrays. PureCodecs does not reproduce
  this ownership bug.

## Hard Constraints

- Production libraries continue to target `netstandard2.0` only.
- Codec execution remains pure C#.
- No P/Invoke, native codec DLL, native fallback, or runtime OpenJPH dependency.
- OpenJPH/OpenJPEG C/C++ source may be read only as a compatibility-research
  reference. Do not vendor, copy, translate, compile, link, directly load, or
  execute that source or its locally built binaries in production code, tests,
  or tools, and do not use it as a source-level dependency or implementation
  template.
- Tests and reference tools may call only the public .NET codec API provided by
  the `fo-dicom.Codecs` NuGet package and rely on its normal NuGet runtime-asset
  resolution. They must not add a native resolver or inspect a local OpenJPH
  source/build tree.
- Tests and tools must use `PackageReference` for `fo-dicom` and
  `fo-dicom.Codecs`. Assembly paths, `HintPath`, DLL copying/replacement, and
  source or project references to local upstream checkouts are prohibited.
  A local upstream build is eligible only after it is packed as a complete
  NuGet package and restored from a package source.
- Supported upstream dependencies use a minimum version range and record the
  actually resolved version. No exact package version or commit is a permanent
  execution requirement; behavior gates remain mandatory for every resolved
  version.
- The existing JPEG 2000 family assembly remains the production boundary.
- Classic JPEG 2000 `.90` and `.91` behavior must not regress.
- Reference implementation names may appear in tests, tools, fixtures, and
  provenance documentation, but not in production type names.

## Alignment Authority

ISO/IEC 15444-15 is authoritative for the HTJ2K algorithm and codestream
requirements. The public .NET behavior and generated output of
`fo-dicom.Codecs 6.0.0-beta1` are the compatibility baseline for DICOM integration,
transfer-syntax selection, defaults, parameters, and deterministic reference
codestreams. OpenJPH implementation details are not an implementation source.

For deterministic default encoding, alignment ends with exact extracted-frame
codestream equality. Whole DICOM file equality is not required because dataset
serialization, item padding, and unrelated metadata may differ.

Incidental native-wrapper defects are not compatibility requirements. In
particular, the managed implementation must not reject a valid codestream only
because its compressed size is not smaller than the source frame. Such cases
must remain standards-compliant and interoperable even when the native wrapper
rejects them.

Pure-specific extension parameters may produce output different from the
reference default. They must still produce valid HTJ2K codestreams that decode
correctly through `fo-dicom.Codecs` and other selected external decoders.

## Scope

### Required Transfer Syntaxes

- HTJ2K Lossless (`1.2.840.10008.1.2.4.201`)
- HTJ2K Lossless RPCL (`1.2.840.10008.1.2.4.202`)
- HTJ2K Lossy (`1.2.840.10008.1.2.4.203`)

### Required Input Coverage

- 8-bit, 12-bit stored in a 16-bit container, and 16-bit samples.
- Signed and unsigned samples.
- MONOCHROME1, MONOCHROME2, RGB, YBR_FULL, and YBR_FULL_422 where the
  corresponding DICOM layout can be normalized without ambiguity.
- Planar and interleaved three-component input.
- Single-frame and multi-frame data.
- Small, odd-sized, code-block-boundary, and large frames.

### Explicitly Outside This Design

- JPEG 2000 Part 2 multi-component transfer syntaxes.
- JPIP/JPT transfer syntaxes.
- JPEG XL.
- Native fallback or runtime selection between managed and native codecs.
- Unrelated refactoring of the classic JPEG 2000 encoder or decoder.

## Architecture

The existing `fo-dicom.PureCodecs.Jpeg2000` assembly remains in place. HTJ2K
work is divided into six internal boundaries.

### 1. DICOM Adapter

Responsibilities:

- Convert public codec parameters into a resolved, validated encoding profile.
- Normalize DICOM sample layout and photometric interpretation.
- Map `BitsAllocated`, `BitsStored`, `HighBit`, `PixelRepresentation`, frame
  dimensions, component count, and planar configuration.
- Preserve frame count and required compressed-pixel metadata.
- Wrap failures with transfer syntax, operation, and frame context.

This boundary must not contain DWT, quantization, HT block, or packet logic.

### 2. fo-dicom.Codecs Reference Baseline

This is test and tooling infrastructure only. It records:

- Reference package version, release commit, and codestream version marker.
- Input DICOM and extracted raw frame hashes.
- Effective encoder parameters.
- `fo-dicom.Codecs` codestream hashes and marker summaries.
- Expected decoded pixel hashes or lossy quality metrics.

Reference generation calls the `fo-dicom.Codecs` .NET codec classes in an
isolated .NET process. Isolation bounds package failures without requiring a
local OpenJPH build, direct native invocation, custom DLL resolution, or any
OpenJPH source checkout.

Reference provenance is verified rather than copied from repository constants.
The worker reads the loaded `fo-dicom.Codecs` assembly version and the OpenJPH
version reported by the generated codestream. A manifest comparison includes
the raw-frame hash, codestream hash and logical length, decoded-frame hash or
lossy metrics, effective parameters, marker summary, and tile-part count.
Pure manifests are constructed independently from Pure output; they are not
created by copying reference fields. Deterministic reference artifacts are
versioned, and regeneration never silently accepts new output.

### 3. Transform and Quantization

Responsibilities:

- Signed and unsigned sample import and level shifting.
- Reversible and irreversible multi-component transforms.
- Reversible 5/3 and irreversible 9/7 wavelet transforms.
- Standard-conformant fixed-point and rounding behavior validated against
  frozen `fo-dicom.Codecs` output.
- Reversible and scalar-expounded quantization metadata.
- CAP magnitude-bound derivation.

Tests compare component samples, subband coefficients, quantized values, QCD,
and CAP data before considering downstream block or packet differences.

### 4. HT Block Coding

Responsibilities:

- Missing-MSB and zero-bit-plane calculation.
- Cleanup, significance-propagation, and magnitude-refinement passes.
- MEL, VLC, and MagSgn coding.
- Cleanup `Scup`, termination, reverse bitstream handling, and `0xFF` stuffing.
- Segment pass counts and byte lengths.

The boundary accepts a coefficient block plus coding context and returns
explicit pass/segment metadata with the encoded bytes. It must be testable
without constructing a complete DICOM frame or codestream.

### 5. Packet and Tile-Part Assembly

Responsibilities:

- Inclusion and zero-bit-plane tag trees.
- Code-block pass contributions and length coding.
- LRCP, RLCP, RPCL, PCRL, and CPRL packet iteration.
- Resolution-based tile-part division matching the frozen reference defaults.
- TLM, SOT, Psot, tile-part index, and tile-part count generation.
- Packet header and body state across layers and tile parts.

This boundary consumes already encoded block passes. It must not recalculate
wavelet coefficients or block payloads.

### 6. Codestream Decode

Responsibilities:

- Validate and parse main and tile headers.
- Reassemble packet headers, packet bodies, block segments, and tile parts.
- Decode HT passes and reconstruct component coefficients.
- Apply inverse quantization, inverse DWT, inverse component transform, and
  sample repacking.
- Validate decoded codestream metadata against DICOM pixel metadata.

Compatibility work includes multi-tile codestreams and an audit of COC, QCC,
POC, SOP, EPH, PLT, RGN, PPM, PPT, component subsampling, and tile-header
overrides. Unsupported standard features must be either implemented or rejected
with an explicit managed exception and a documented limitation.

## Encoding Data Flow

For every frame:

1. Validate DICOM metadata, parameters, dimensions, and exact source length.
2. Normalize planar/interleaved and YBR/RGB layout into resolved components.
3. Import signed or unsigned samples with the reference-compatible precision
   rules established by black-box fixtures.
4. Apply level shift and the selected component transform.
5. Apply the reversible or irreversible DWT.
6. Quantize for lossy encoding and construct QCD/CAP values.
7. Partition subbands into precincts and 64 by 64 code-blocks.
8. Encode all required HT passes and retain their lengths and contexts.
9. Construct packet headers and bodies in the resolved progression order.
10. Divide tile parts and write SOC, SIZ, CAP, COD, QCD, COM, TLM, SOT, SOD,
    and EOC with reference-compatible values and ordering.
11. Add exactly one encapsulated compressed frame to the destination pixel data.

Each stage exposes test-only snapshots or stable summaries so the first
semantic divergence can be located without inferring it from final file size.

## Decoding Data Flow

For every frame:

1. Validate the raw codestream envelope and bounded marker lengths.
2. Parse main-header and tile-header defaults and component overrides.
3. Validate dimensions, precision, signedness, and component count against the
   destination DICOM metadata before allocating large buffers.
4. Reassemble tile parts and iterate packets in codestream order.
5. Decode cleanup and refinement segments for each code-block.
6. Inverse quantize and reconstruct every tile component.
7. Apply inverse DWT, inverse component transform, and level-shift reversal.
8. Copy tiles into the complete component image and repack DICOM samples.
9. Restore the requested planar layout and add one raw frame.

Mutable packet, tag-tree, block, and transform state is scoped to one frame.
Multi-frame operations must not share mutable coding state.

## Parameter Contract

Every public parameter is classified explicitly:

- `ProgressionOrder`: follows the exact `fo-dicom.Codecs` transfer-syntax
  behavior, including parameter use for `.202` and the reference default for the
  other HTJ2K syntaxes.
- `Irreversible`, `NumberOfDecompositions`, `EmployColorTransform`, and
  `InsertTlmMarkers`: their effective behavior is derived from the current
  `fo-dicom.Codecs` .NET wrapper. Properties ignored by the reference wrapper remain
  compatibility-ignored unless a separately documented Pure extension is
  introduced.
- `TargetRatio` and `NumLayers`: Pure extensions. They must not silently claim
  native parity. Unsupported values are rejected until real multi-layer and
  rate behavior is implemented.

`TargetRatio` accepts zero as unset or a finite value greater than one.
`NumLayers` must remain one until real HT packet-layer contributions are
implemented. Non-finite ratios, nonzero ratios less than or equal to one, and
unsupported layer counts fail before frame processing.

Resolved parameters are immutable for a frame. Silent fallback from invalid
or unsupported extension values is not allowed.

## Alignment Levels

Alignment is evaluated in order. A later level cannot hide a failure in an
earlier level.

### Level 1: Structure

SIZ, CAP, COD, QCD, COM, TLM, SOT, Psot, tile-part count, code-block style,
progression order, and marker order match the reference.

### Level 2: Intermediate Semantics

Component samples, DWT coefficients, quantized values, missing MSBs, HT pass
counts, segment lengths, packet contributions, and tile-part boundaries match.

### Level 3: Compressed Frame

For frozen deterministic inputs and default parameters, the extracted complete
codestream is byte-for-byte equal to the `fo-dicom.Codecs` reference output.

### Level 4: Interoperability

- Pure encode to `fo-dicom.Codecs` decode.
- `fo-dicom.Codecs` encode to Pure decode.
- Lossless decoded pixels are exactly equal.
- Lossy decoded pixels meet frozen maximum-error, MAE, PSNR, and compression
  ratio bounds for the corresponding reference parameters.

Pixel tolerance must not be widened to mask a known structure, transform,
block, or packet mismatch.

## Error Handling

### DICOM Input Errors

Reject unsupported bit depth, component count, photometric interpretation,
planar layout, frame length, or parameter combinations before expensive buffer
allocation or transform work.

### Codestream Structure Errors

Reject missing required markers, invalid segment lengths, conflicting SIZ/COD/
CAP/QCD data, out-of-range Psot/TLM values, invalid tile-part indexes, invalid
packet lengths, and DICOM/codestream metadata conflicts.

The `fo-dicom.Codecs` 12-bit-in-16 HTJ2K compatibility case may use SIZ
precision equal to `BitsAllocated`, but decoded samples must still fit the
declared `BitsStored` range. This exception is HTJ2K-specific and must not relax
classic JPEG 2000 precision validation.

### HT Block Errors

Reject invalid `Scup`, MEL/VLC data, stuffing, pass counts, segment lengths,
and code-block contributions before indexing or copying outside declared data.

All public codec failures are surfaced as `DicomCodecException` with transfer
syntax, operation, and frame number. Internal exception types such as
`IndexOutOfRangeException` and `OverflowException` must not escape. Error text
must not include raw pixel or patient data.

## Test Matrix

The required matrix covers:

- `.201`, `.202`, and `.203`.
- Pure-to-Pure, Pure-to-`fo-dicom.Codecs`, and `fo-dicom.Codecs`-to-Pure.
- 8-bit, 12-bit-in-16, and 16-bit data.
- Signed and unsigned samples.
- MONOCHROME1, MONOCHROME2, RGB, YBR_FULL, and YBR_FULL_422.
- Planar and interleaved color.
- Single-frame and multi-frame data.
- Very small, odd-sized, 64-boundary, cross-code-block, and 888 by 459 frames.
- All five progression orders, with separate `.202` RPCL and tile-part checks.
- Default exact codestream baselines and standard-interoperable Pure extensions.
- Truncated, marker-corrupted, packet-corrupted, and bounded mutated inputs.

Fixtures are de-identified, redistributable, versioned, and accompanied by a
manifest containing provenance, parameters, raw hash, codestream hash, decoded
hash or lossy metrics, and expected marker summaries.

The existing external fixture assertion that only checks for non-zero output is
replaced by an expected raw hash or bounded lossy metric comparison.

Multi-frame interoperability passes the complete source through each encoder
and decoder once. Per-frame extraction may diagnose a failure but does not
satisfy the multi-frame gate. Lossy rows record maximum absolute error, MAE,
PSNR, and compression ratio using the declared precision and signedness.

The executable strict gate is `eng/Verify-Htj2kUpstreamMultiframe.ps1`. It
restores `fo-dicom.Codecs` through `PackageReference` from the configured NuGet
sources, with a configurable minimum version defaulting to `6.0.0-beta1`, and then
requires the three Pure-to-native complete decode cases. Version eligibility
does not replace the behavioral tests. A prior DLL-replacement probe is
diagnostic evidence only and is not a valid release gate under this dependency
boundary. The package-based strict gate remains pending because the current
`6.0.0-beta1` package does not pass the Pure-to-native complete-call cases. The
three native-encode-to-Pure-decode complete-call cases pass separately with
`6.0.0-beta1`.

## CI and Isolation

Ordinary xUnit execution covers pure managed units, self round-trips, committed
fixtures, invalid inputs, and DICOM integration.

`fo-dicom.Codecs` interoperability runs through process-isolated .NET workers. Each
worker handles one codec, direction, fixture, and parameter set, has an explicit
timeout, terminates its process tree on timeout, and reports structured output.
A package failure or hang must fail only that matrix row and must not terminate
or lock the managed test host. Workers use normal NuGet resolution and never
compile, locate, or load OpenJPH directly.

Ordinary xUnit tests do not instantiate Native codec classes or register the
Native transcoder manager in the test host. They invoke bounded workers or use
committed managed reference artifacts.

The HTJ2K matrix becomes a required CI and release gate rather than a documented
exclusion.

## Performance

Correctness and reference alignment precede optimization. After codestream
behavior is frozen, add separate benchmarks for `.201`, `.202`, and `.203` and
measure:

- Forward and inverse DWT.
- HT block encode and decode.
- Packet and tile-part assembly.
- Complete frame encode and decode.
- Allocated bytes and peak working memory.

Optimizations must retain exact reference fixtures and all interoperability
results. Initial release gates prevent clear regressions but do not require the
managed implementation to equal native throughput.

## Implementation Sequence

1. Add reproducible `fo-dicom.Codecs` .NET baseline generation and structured
   diff tooling without direct OpenJPH access.
2. Align `.201` default lossless encoding stage by stage and then byte for byte.
3. Align `.202` progression and reference tile-part behavior.
4. Align `.203` default lossy quantization and output, then define Pure extension
   rate behavior separately.
5. Complete `fo-dicom.Codecs`-to-Pure decode coverage and unsupported-feature
   audit.
6. Complete the DICOM, invalid-input, external-fixture, and multi-frame matrix.
7. Add process-isolated HTJ2K CI gates and reconcile release documentation.
8. Profile and optimize only after all correctness gates are stable.

Each implementation step begins with a failing focused test derived from a
frozen reference artifact. Broader JPEG 2000 and full solution tests run before
the step is considered complete.

## Completion Criteria

HTJ2K alignment is complete only when:

- Default `.201`, `.202`, and `.203` fixture codestreams meet the exact alignment
  level defined for their frozen inputs.
- Bidirectional `fo-dicom.Codecs` interoperability passes for the required
  matrix.
- All lossless decoded frames are byte-exact and all lossy fixtures meet frozen
  quality and compression bounds.
- Invalid input produces bounded managed failures.
- Required DICOM tags, frame counts, layouts, and encapsulation are preserved.
- Focused tests, the full test suite, Release build, interoperability runner,
  and consumer smoke tests pass.
- The compatibility table, known limitations, development checklist, and
  release notes describe the verified behavior without contradictory claims.
