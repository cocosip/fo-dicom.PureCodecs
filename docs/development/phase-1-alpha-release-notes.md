# fo-dicom.PureCodecs 0.1.0-alpha.1 Release Notes

## Release Type

First alpha package for phase 1 replacement validation.

This release is intended for compatibility testing against applications that currently use `fo-dicom.Codecs`. It is not a native-code wrapper; all codec execution paths are implemented in managed C#.

## Highlights

- Provides one NuGet package: `fo-dicom.PureCodecs`.
- Targets `netstandard2.0` for all production assemblies.
- Registers through fo-dicom's existing transcoder manager service integration.
- Does not require users to register RLE, JPEG, JPEG-LS, or JPEG 2000 assemblies separately.
- Contains no native codec DLLs, P/Invoke codec paths, or native fallback paths.
- Includes managed encode and decode coverage for the phase 1 transfer syntax matrix.
- Includes direct-project consumer smoke tests for modern .NET and .NET Framework 4.7.2 on Windows, plus separate NuGet package-content inspection.

## Supported Transfer Syntaxes

| Codec family | Transfer syntax | UID |
| --- | --- | --- |
| RLE | RLE Lossless | `1.2.840.10008.1.2.5` |
| JPEG | JPEG Baseline Process 1 | `1.2.840.10008.1.2.4.50` |
| JPEG | JPEG Extended Process 2/4 | `1.2.840.10008.1.2.4.51` |
| JPEG | JPEG Lossless Process 14 | `1.2.840.10008.1.2.4.57` |
| JPEG | JPEG Lossless Process 14 SV1 | `1.2.840.10008.1.2.4.70` |
| JPEG-LS | JPEG-LS Lossless | `1.2.840.10008.1.2.4.80` |
| JPEG-LS | JPEG-LS Near-Lossless | `1.2.840.10008.1.2.4.81` |
| JPEG 2000 | JPEG 2000 Lossless | `1.2.840.10008.1.2.4.90` |
| JPEG 2000 | JPEG 2000 Lossy | `1.2.840.10008.1.2.4.91` |
| JPEG 2000 / HTJ2K | HTJ2K Lossless | `1.2.840.10008.1.2.4.201` |
| JPEG 2000 / HTJ2K | HTJ2K Lossless RPCL | `1.2.840.10008.1.2.4.202` |
| JPEG 2000 / HTJ2K | HTJ2K Lossy | `1.2.840.10008.1.2.4.203` |

JPEG XL is intentionally excluded from phase 1 because `fo-dicom.Codecs` marks it as in development.

## Package Contents

The package includes these managed assemblies under `lib/netstandard2.0`:

- `fo-dicom.PureCodecs.dll`
- `fo-dicom.PureCodecs.Rle.dll`
- `fo-dicom.PureCodecs.Jpeg.dll`
- `fo-dicom.PureCodecs.JpegLs.dll`
- `fo-dicom.PureCodecs.Jpeg2000.dll`

The package should not include `runtimes/`, `native/`, `.so`, `.dylib`, or non-PureCodecs DLL payloads.

## Validation Baseline

The repository validation suite covers:

- Unit tests for codec primitives and marker parsing.
- Raw, RGB, and multi-frame round-trip tests.
- Lossless exact byte equality after decode.
- Lossy tolerance checks after decode.
- Efferent `fo-dicom.Codecs` unit and acceptance fixtures available in this repository.
- Managed exception behavior for invalid compressed input.
- Direct-project consumer and single-manager registration smoke tests.
- NuGet package-content checks for managed `netstandard2.0` assemblies only.

## Known Limitations

See [`phase-1-known-limitations.md`](phase-1-known-limitations.md) for the maintained phase 1 limitation list.

Limitations that remain outside this alpha are compatibility edges or fixture gaps, not native dependency work:

- JPEG XL remains outside phase 1.
- JPEG 2000 Part 2 multi-component and JPIP/JPT transfer syntaxes remain unregistered.
- Some rare JPEG/JPEG 2000 marker combinations fail with explicit managed exceptions.
- HTJ2K `.201`, `.202`, and `.203` use a managed Part 15 codestream path and
  are validated with managed round-trips, standard HT vectors, exact default
  `fo-dicom.Codecs 5.16.7` codestreams, and process-isolated interoperability.
  Complete `.201/.202` native-to-Pure multi-frame decode is byte-exact. The
  reverse complete-dataset native call has a documented `fo-dicom.Codecs 5.16.7`
  wrapper difference even though every identical Pure codestream decodes
  byte-exact through an individual native call. Broad independent third-party
  fixture coverage remains limited by available redistributable samples.

## Upgrade Notes

Applications that already configure fo-dicom services should replace `fo-dicom.Codecs` registration with:

```csharp
new DicomSetupBuilder()
    .RegisterServices(services => services.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

No per-codec-family registration is required.
