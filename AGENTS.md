# AGENTS.md

## Project

This repository is `fo-dicom.PureCodecs`, a pure C# replacement for `fo-dicom.Codecs`.

The authoritative design and development plan is:

- `docs/design/fo-dicom-pure-codecs-design.md`
- `docs/development/development-checklist.md`

Focused design documents:

- `docs/design/codec-entry-design.md`
- `docs/design/rle-codec-design.md`
- `docs/design/jpeg-codec-design.md`
- `docs/design/jpegls-codec-design.md`
- `docs/design/jpeg2000-codec-design.md`

Future implementation work must follow that document unless the user explicitly changes the design.

## Hard Constraints

- Production libraries must target `netstandard2.0` only.
- Do not add `netcoreapp`, `net8.0`, or other production target frameworks unless the user explicitly approves a design change.
- Codec execution must be pure C#.
- Do not use native C/C++ codec DLLs.
- Do not use P/Invoke for codec execution.
- Do not add a native fallback path.
- Deliver one NuGet package.
- The NuGet package may contain multiple DLLs, split by codec family.

## Reference Dependency Boundary

- OpenJPEG, OpenJPH, and all other C/C++ codec implementations are research
  references only. Do not copy, translate, compile, link, directly load, or
  directly call their source or locally built binaries from this repository's
  production code, tests, or tools. Native runtime assets transitively supplied
  and internally used by the official `fo-dicom.Codecs` NuGet package are
  allowed only behind that package's public C# API.
- Interoperability, encoded-image comparison, and reference fixture generation
  must call the public C# APIs exposed by the `fo-dicom` and
  `fo-dicom.Codecs` NuGet packages.
- Reference projects must use normal NuGet `PackageReference`. Do not use
  assembly `Reference`/`HintPath`, copy or replace `fo-dicom.Codecs.dll`, or add
  a `ProjectReference`/source link to a local `fo-dicom` or
  `fo-dicom.Codecs` checkout.
- Local `fo-dicom` and `fo-dicom.Codecs` source checkouts may be read for
  compatibility research only. To validate a local upstream change, first
  produce a complete NuGet package and consume it from a package source through
  normal restore.
- The `fo-dicom.Codecs` version in central package management exists only for
  ordinary NuGet restore. Do not add supported-version ranges, package-version
  checks, commit/provenance checks, assembly metadata checks, `.deps.json`
  inspection, or environment-variable switches to compatibility tooling.
- Compatibility results must be determined only by behavior through the public
  codec APIs. Package or runtime identity must not be recorded in reference
  output and must not classify, skip, downgrade, or otherwise alter a result.

## Assembly Split

The intended package shape is:

- `fo-dicom.PureCodecs.dll`: public entry point, registration, shared contracts, shared utilities.
- `fo-dicom.PureCodecs.Jpeg.dll`: JPEG Process 1, Process 2/4, Process 14, Process 14 SV1.
- `fo-dicom.PureCodecs.JpegLs.dll`: JPEG-LS Lossless and JPEG-LS Near-Lossless.
- `fo-dicom.PureCodecs.Jpeg2000.dll`: JPEG 2000 Lossless, JPEG 2000 Lossy, HTJ2K Lossless, HTJ2K Lossless RPCL, HTJ2K Lossy.
- `fo-dicom.PureCodecs.Rle.dll`: RLE Lossless.

The split is by codec family, not by individual transfer syntax.

## Phase 1 Scope

Phase 1 must fully replace the completed codec support in `fo-dicom.Codecs`.

Required transfer syntaxes:

- RLE Lossless
- JPEG Baseline Process 1
- JPEG Extended Process 2/4
- JPEG Lossless Process 14
- JPEG Lossless Process 14 SV1
- JPEG-LS Lossless
- JPEG-LS Near-Lossless
- JPEG 2000 Lossless
- JPEG 2000 Lossy
- HTJ2K Lossless
- HTJ2K Lossless RPCL
- HTJ2K Lossy

JPEG XL is not in phase 1 because `fo-dicom.Codecs` marks it as in development.

## fo-dicom Integration

Integrate through fo-dicom's existing codec APIs:

- `FellowOakDicom.Imaging.Codec.IDicomCodec`
- `FellowOakDicom.Imaging.Codec.ITranscoderManager`
- `FellowOakDicom.Imaging.Codec.TranscoderManager`

The intended user registration is:

```csharp
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

Do not require users to register each codec-family DLL separately.

## Reference Source Locations

Use local source checkouts for compatibility research. The exact checkout roots are machine-specific:

- `<FO_DICOM_SOURCE_ROOT>`: local checkout of `fo-dicom`.
- `<FO_DICOM_CODECS_SOURCE_ROOT>`: local checkout of `fo-dicom.Codecs`.

Important files:

- `<FO_DICOM_SOURCE_ROOT>\FO-DICOM.Core\Imaging\Codec\IDicomCodec.cs`
- `<FO_DICOM_SOURCE_ROOT>\FO-DICOM.Core\Imaging\Codec\TranscoderManager.cs`
- `<FO_DICOM_SOURCE_ROOT>\FO-DICOM.Core\Imaging\Codec\DicomTranscoder.cs`
- `<FO_DICOM_SOURCE_ROOT>\FO-DICOM.Core\Imaging\DicomPixelData.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\NativeTranscoderManager.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\DicomRleCodec.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\DicomJpegCodec.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\DicomJpegLsCodec.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\DicomJpeg2000Codec.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Codec\DicomHtJpeg2000Codec.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Unit\TranscodeUnitTest.cs`
- `<FO_DICOM_CODECS_SOURCE_ROOT>\Tests\Acceptance\AcceptanceTests.cs`

## Verification

When implementation begins, use the `fo-dicom.Codecs` tests and DICOM samples as the compatibility baseline, but add stronger validation:

- Exact byte equality for lossless round-trips.
- Tolerance checks for lossy round-trips.
- Frame count preservation.
- Required DICOM tag checks after compression.
- Managed exception checks for invalid input.

## .NET Commands

When running `dotnet build` or `dotnet test` in the Codex app, run the command outside the sandbox by requesting escalated permissions.

Reason: the current Codex app sandbox can prevent some `dotnet` commands from streaming or returning output correctly, which may make the command appear to hang.

When requesting sandbox escalation for any `dotnet` command, include:

- `sandbox_permissions: "require_escalated"`
- `prefix_rule: ["dotnet"]`

If a persistent `dotnet` prefix rule is already approved, run `dotnet` commands outside the sandbox automatically.
