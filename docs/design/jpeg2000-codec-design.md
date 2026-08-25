# JPEG 2000 and HTJ2K Codec Design

## Purpose and Status

This is the single design authority for the JPEG 2000 family in
`fo-dicom.PureCodecs`. It supersedes the former OpenJPEG/OpenJPH separation and
HTJ2K alignment documents.

The family is implemented in pure C#. Classic JPEG 2000 functionality and the
HTJ2K codec paths are present; Phase 1 is **not** released as fully aligned.
The outstanding release gate is the normally restored public
`fo-dicom.Codecs` complete-dataset interoperability matrix described in
[Development Checklist](../development/development-checklist.md).

## Scope

Production assembly: `fo-dicom.PureCodecs.Jpeg2000.dll` targeting
`netstandard2.0`.

| Transfer syntax | UID | Codec path | Phase 1 status |
| --- | --- | --- | --- |
| JPEG 2000 Lossless | `1.2.840.10008.1.2.4.90` | Classic | Implemented |
| JPEG 2000 Lossy | `1.2.840.10008.1.2.4.91` | Classic | Implemented |
| HTJ2K Lossless | `1.2.840.10008.1.2.4.201` | High-throughput | Implemented; release gate open |
| HTJ2K Lossless RPCL | `1.2.840.10008.1.2.4.202` | High-throughput | Implemented; release gate open |
| HTJ2K Lossy | `1.2.840.10008.1.2.4.203` | High-throughput | Implemented; release gate open |

JPEG 2000 Part 2 multi-component syntaxes (`.92` and `.93`), JPIP, JPT, and
component subsampling are outside Phase 1 and fail with managed exceptions.
JP2-wrapped frames are detected and rejected; this codec consumes raw J2K
codestreams.

## Public Surface and DICOM Contract

`PureTranscoderManager` registers these `IDicomCodec` implementations; consumers
do not register the JPEG 2000 assembly independently:

```csharp
DicomJpeg2000LosslessCodec
DicomJpeg2000LossyCodec
DicomHtJpeg2000LosslessCodec
DicomHtJpeg2000LosslessRpclCodec
DicomHtJpeg2000LossyCodec
```

`DicomJpeg2000Params` preserves the public fo-dicom parameter contract for
`Irreversible`, `Rate`, `RateLevels`, `AllowMCT`,
`UpdatePhotometricInterpretation`, and
`EncodeSignedPixelValuesAsUnsigned`. Its managed progression-order value covers
LRCP, RLCP, RPCL, PCRL, and CPRL. `DicomHtJpeg2000Params` maps fo-dicom's HTJ2K
parameters; `.202` always enforces RPCL.

For each frame, the adapter validates the DICOM geometry, bit depth, signedness,
component layout, and frame length; maps them to `Ssiz`; normalizes supported
RGB/YBR layouts; and repacks decoded samples into fo-dicom's raw frame layout.
`YBR_FULL` and `YBR_FULL_422` normalize to RGB before classic MCT and update the
compressed photometric interpretation to `YBR_RCT` or `YBR_ICT`.

## Architecture

The implementation shares JPEG 2000 structural infrastructure, not compatibility
policy:

| Layer | Shared responsibility | Classic `.90/.91` policy | HTJ2K `.201/.202/.203` policy |
| --- | --- | --- | --- |
| Codestream | Marker I/O; SIZ, COD, COC, QCD, QCC, SOT, SOD, EOC, COM; tile, component, precinct and packet models | Classic packet contribution model | HT packet and segment assembly |
| Transform/quantization | Geometry, subband indexing, QCD syntax and guards | OpenJPEG-observable 5/3, 9/7, QCD, PCRD and rate allocation | OpenJPH-observable normalization, scaling and irreversible quantization |
| Entropy coding | Bounds checks and model hand-off | EBCOT/MQ Tier-1 | Part 15 MEL, VLC, MagSgn, cleanup and refinement coding |
| DICOM boundary | Metadata validation, sample conversion, managed exceptions | OpenJPEG-observable public behavior | OpenJPH-observable public behavior |

Shared code must not erase the split. Classic and HTJ2K maintain separate
transform entry points, precision rules, rate/allocation behavior, packet policy,
and block coding. A change to shared code requires the classic reference gates
and all three HTJ2K reference gates.

Encoding follows: validate and normalize input; level shift; optional RCT/ICT;
reversible or irreversible DWT; quantization when lossy; tile/precinct/code-block
partitioning; family-specific block encoding; packet/marker writing; DICOM frame
encapsulation. Decoding performs the inverse stages after bounded marker, tile,
packet and block parsing.

## Compatibility and Reference Boundary

The behavioral baseline is `fo-dicom.Codecs` through its public C# API:

- Classic `.90/.91` behavior is referred to as OpenJPEG-compatible; HTJ2K
  `.201/.202/.203` behavior is referred to as OpenJPH-compatible. These names
  describe observed behavior, never a production dependency.
- Production remains managed C# only: no P/Invoke, native codec DLL, native
  fallback, native resolver, or runtime library selection.
- OpenJPEG/OpenJPH source may be read for algorithm, parameter, control-flow,
  and behavioral research. It must not be copied, translated, vendored,
  compiled, linked, loaded, or executed by production code, tests, or tools.
- Reference tests and tools use normal `PackageReference` to `fo-dicom` and
  `fo-dicom.Codecs`. They do not use local upstream project/assembly references,
  `HintPath`, DLL replacement, or identity/provenance checks.
- Native/reference operations run only in bounded child processes. A worker's
  result is its public API behavior; package version, commit, assembly metadata,
  `.deps.json`, and environment switches must not classify, skip, or alter a row.

Classic lossy alignment proceeds from DICOM sample mapping through DWT, QCD,
Tier-1 pass rate/distortion accounting, PCRD allocation, and Tier-2 packet
writing. Final codestream bytes are a terminal signal, not a substitute for
those stages. Multi-layer output requires actual early-layer packet
contributions. A DICOM encapsulation padding byte after EOC is outside the
logical codestream and is not included in `Psot`.

## Validation and Error Handling

The decoder supports the Phase 1 marker and coding features covered by fixtures,
including POC progression changes, RGN Maxshift, PPM/PPT packed packet headers,
SOP/EPH packet markers, and RESET/VSC classic code-block styles. HTJ2K rejects
unsupported RGN and PPM/PPT semantics.

Malformed marker lengths, missing required markers, invalid tile/packet bounds,
unsupported precision or layout, invalid progression/rate parameters, and
unsupported codestream features produce `DicomCodecException` with the transfer
syntax, operation, frame, and tile/component/marker context where known.

Validation has three independent layers:

1. Managed unit and DICOM integration tests cover markers, transforms,
   quantization, block coding, progression order, invalid input, tags, frame
   counts, lossless exact samples, and fixed lossy tolerances.
2. Committed fixtures exercise standard external codestream decoding and
   reference-produced HT block vectors.
3. Process-isolated workers compare complete DICOM datasets through public
   `fo-dicom.Codecs` APIs in both directions. Lossless assertions are exact;
   lossy assertions use the pre-measured fixed tolerance.

## Recorded Verification and Open Release Gate

The 2026-08-25 source/test audit recorded a full Release suite of `902/902`
passing and no skipped tests. The same audit recorded `104/112` passing rows in
the 12-format complete-dataset interoperability matrix: 4 workers passed and 8
failed. All eight failures are the seven-frame `sample-05.dcm` Pure-to-reference
decode path at frame 1; the corresponding reference-to-Pure rows pass.

This result proves neither a Pure codec defect nor release readiness. The public
reference decoder returns oversized pooled buffers for those complete-dataset
rows, whereas the declared frame size is exact. Per-frame extraction is allowed
only to diagnose the failure; it is not an acceptance substitute. Do not add
output reconstruction, frame splitting, package checks, or failure downgrades.

The gate closes only when an ordinarily restored public `fo-dicom.Codecs`
package passes every complete-dataset row, including `.201`, `.202`, and `.203`,
in both directions. At that point rerun the unchanged worker matrix, focused
JPEG 2000/HTJ2K tests, full Release suite, package inspection, and consumer smoke
tests before claiming Phase 1 completion.

## Maintenance Rules

Do not create a second JPEG 2000 design, alignment handoff, or optimization
checklist. Keep enduring design and current release-gate facts here; record
cross-codec active remediation only in
[fo-dicom.Codecs Alignment Remediation](../development/fo-dicom-codecs-alignment-remediation.md).
Historical benchmark measurements and resolved tool-compression investigations
are intentionally not retained as living requirements. Start future performance
work only from a new measured hotspot after the affected reference gate is
stable.
