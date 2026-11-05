# Phase 1 Verification Baseline

## Purpose

This document records what the repository currently proves about the Phase 1
implementation and how to reproduce that evidence. Design intent lives in
`docs/design`; this file keeps only the current baseline, the standard
verification sequence, and the acceptance rules for future changes.

It replaces the former checkbox progress tracker and the resolved
`fo-dicom.Codecs` alignment remediation log.

## Current Status

Recorded on 2026-11-05:

- Package identity: `FoDicom.PureCodecs`, version `0.5.1`, published as one
  `FoDicom.PureCodecs.<version>.nupkg`.
- Package contents: five managed assemblies under `lib/netstandard2.0`, with no
  native or runtime-specific assets.
- Production target: `netstandard2.0` only.
- Codec execution: pure C#, with no P/Invoke, native codec DLLs, or native
  fallback paths.
- Transfer syntaxes: all twelve Phase 1 syntaxes are registered and support
  encode and decode.
- Reference side: `fo-dicom.Codecs` `6.0.0`, restored normally and used only
  through its public C# API.

## Current Results

| Check | Result |
| --- | --- |
| Release test suite | `934` passed, `0` failed, `0` skipped |
| Process-isolated interoperability matrix | `12/12` formats passed, `114/114` complete-dataset rows passed |
| Package layout inspection | exactly five `lib/netstandard2.0` assemblies, no native assets |
| Consumer smoke tests | modern .NET and .NET Framework 4.7.2 passed |

## Standard Verification Sequence

Run from the repository root:

```bash
dotnet restore fo-dicom.PureCodecs.slnx
dotnet build fo-dicom.PureCodecs.slnx -c Release --no-restore
dotnet test fo-dicom.PureCodecs.slnx -c Release --no-build --verbosity normal
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation -c Release --no-build -- --parallel 4 --worker-timeout-seconds 300
```

Run one interoperability format while diagnosing a codec:

```bash
dotnet run --project tools/fo-dicom.PureCodecs.InteropValidation -- --worker jpeg-ls-lossless
```

Verify the packed package and consumer applications:

```bash
bash ./eng/verify-package-consumer-smoke.sh
```

```powershell
.\eng\Verify-PackageConsumerSmoke.ps1 -RequireNet472
```

CI runs restore, build, the full test suite, and the interoperability matrix for
pull requests and pushes to `master` or `main`. Tag pushes matching `v*`
additionally pack, publish, and create a GitHub Release.

## Acceptance Rules

- Lossless round-trips must decode to exact source bytes.
- Lossy round-trips must stay within the recorded per-syntax tolerance.
- Frame counts and required compression tags must be preserved.
- Invalid input must fail with a managed exception.
- Interoperability rows are judged only by public codec API behavior. Package
  or runtime identity must not classify, skip, downgrade, or alter a result.
- A failing interoperability row is a failure. Do not add frame splitting,
  buffer reconstruction, package checks, or failure downgrades to make a row
  pass.

## Known Phase 1 Exclusions

- Progressive, arithmetic-coded, CMYK, and YCCK JPEG are unsupported. JPEG
  Process 1 accepts 8-bit samples; Process 2/4 also accepts 12-bit monochrome
  and 12-bit RGB SF444 in 16-bit containers, but not 12-bit SF422 encoding.
- JPEG-LS planar normalization is limited to three components with
  `BitsStored <= 8`; planar `YBR_FULL_422` is rejected.
- JPEG 2000 accepts raw codestreams only. Part 2 multi-component, JPIP, JPT, and
  component subsampling are out of scope. JP2-wrapped frames are rejected.
- JPEG XL is not part of Phase 1 because `fo-dicom.Codecs` marks it as in
  development.

## Maintenance

Update this document when a dependency upgrade, codec change, or test change
alters the recorded results. Keep it factual and current; do not turn it back
into a development progress log.
