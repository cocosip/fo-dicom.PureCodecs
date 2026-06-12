# Codec Entry Design

## Purpose

This document defines the entry layer for `fo-dicom.PureCodecs`.

The entry layer is implemented by `fo-dicom.PureCodecs.dll`. It wires all codec-family assemblies into fo-dicom's existing transcoder model. It must not implement codec algorithms directly except for small shared helpers.

## Responsibilities

`fo-dicom.PureCodecs.dll` owns:

- `PureTranscoderManager`.
- Explicit registration of every phase 1 `IDicomCodec`.
- Shared codec base helpers, only when they reduce duplication across codec families.
- Shared frame and buffer utilities.
- Shared managed exception wrapping helpers.
- User-facing package usage documentation.

It does not own:

- RLE algorithm implementation.
- JPEG algorithm implementation.
- JPEG-LS algorithm implementation.
- JPEG 2000 or HTJ2K algorithm implementation.
- Native fallback behavior.

## fo-dicom Integration

The only public fo-dicom integration path is:

```csharp
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

`PureTranscoderManager` inherits `FellowOakDicom.Imaging.Codec.TranscoderManager`.

It registers concrete `IDicomCodec` implementations in `LoadCodecs`.

## Registration Shape

Registration is explicit and deterministic:

```csharp
public sealed class PureTranscoderManager : TranscoderManager
{
    public PureTranscoderManager()
    {
        LoadCodecs(null, null);
    }

    public override void LoadCodecs(string path, string search)
    {
        AddCodec(new DicomRleLosslessCodec());

        AddCodec(new DicomJpegProcess1Codec());
        AddCodec(new DicomJpegProcess2_4Codec());
        AddCodec(new DicomJpegLossless14Codec());
        AddCodec(new DicomJpegLossless14SV1Codec());

        AddCodec(new DicomJpegLsLosslessCodec());
        AddCodec(new DicomJpegLsNearLosslessCodec());

        AddCodec(new DicomJpeg2000LosslessCodec());
        AddCodec(new DicomJpeg2000LossyCodec());
        AddCodec(new DicomHtJpeg2000LosslessCodec());
        AddCodec(new DicomHtJpeg2000LosslessRpclCodec());
        AddCodec(new DicomHtJpeg2000LossyCodec());
    }

    private void AddCodec(IDicomCodec codec)
    {
        Codecs[codec.TransferSyntax] = codec;
    }
}
```

No broad reflection over arbitrary loaded assemblies should be used. There should be no second public codec registration abstraction beyond fo-dicom's `IDicomCodec` and `ITranscoderManager`.

## Codec Stub Rule

Before an algorithm is implemented, each transfer syntax may have a stub codec so that registration and package layout can be tested.

The current stub codec public class names are:

- `FellowOakDicom.PureCodecs.Rle.DicomRleLosslessCodec`
- `FellowOakDicom.PureCodecs.Jpeg.DicomJpegProcess1Codec`
- `FellowOakDicom.PureCodecs.Jpeg.DicomJpegProcess2_4Codec`
- `FellowOakDicom.PureCodecs.Jpeg.DicomJpegLossless14Codec`
- `FellowOakDicom.PureCodecs.Jpeg.DicomJpegLossless14SV1Codec`
- `FellowOakDicom.PureCodecs.JpegLs.DicomJpegLsLosslessCodec`
- `FellowOakDicom.PureCodecs.JpegLs.DicomJpegLsNearLosslessCodec`
- `FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000LosslessCodec`
- `FellowOakDicom.PureCodecs.Jpeg2000.DicomJpeg2000LossyCodec`
- `FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LosslessCodec`
- `FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LosslessRpclCodec`
- `FellowOakDicom.PureCodecs.Jpeg2000.DicomHtJpeg2000LossyCodec`

The shared `UnimplementedDicomCodec` base is temporary entry-layer infrastructure for stub codecs. It must be replaced or made irrelevant as each family algorithm is implemented.

Stub codecs must:

- Implement `IDicomCodec`.
- Return the correct `TransferSyntax`.
- Return a valid default parameter object when needed.
- Throw `DicomCodecException` from `Encode` and `Decode`.
- Include the transfer syntax name in the exception message.

Stub codecs must not:

- Silently pass through pixel data.
- Produce invalid compressed frames.
- Claim algorithm completion in tests.

## Shared Utilities

Shared utilities should stay small and boring. Use them for cross-family behavior only:

- Safe frame index validation.
- Buffer size checks.
- `IByteBuffer` to managed array conversion helpers.
- Even-length frame wrapping.
- Common `DicomCodecException` wrapping.
- Shared DICOM pixel metadata snapshots.

Do not move family-specific bitstream parsing into the entry assembly.

## Error Boundary

Every public codec call should leave the process alive.

Entry-layer helpers should make it easy for family codecs to wrap unexpected errors:

```csharp
internal static DicomCodecException WrapCodecException(
    DicomTransferSyntax syntax,
    int? frame,
    Exception exception)
```

The exact helper signature can change during implementation, but exception messages should identify:

- Codec family.
- Transfer syntax.
- Operation: encode or decode.
- Frame index when known.

## Test Plan

Entry-layer tests should verify:

- `PureTranscoderManager` constructs successfully.
- `HasCodec` is true for all phase 1 transfer syntaxes.
- `GetCodec` returns the expected codec type for each transfer syntax.
- `CanTranscode(raw, compressed)` is true for each phase 1 syntax.
- `CanTranscode(compressed, raw)` is true for each phase 1 syntax.
- Stub codecs fail with `DicomCodecException` before algorithms are implemented.
- A consumer app can register `PureTranscoderManager` without registering family DLLs directly.

## Completion Criteria

The entry layer is complete when:

- The solution builds with production libraries targeting only `netstandard2.0`.
- The NuGet package includes all family DLLs under `lib/netstandard2.0`.
- A test consumer can install the package and register only `PureTranscoderManager`.
- Every phase 1 transfer syntax is visible through fo-dicom's transcoder manager.
