# fo-dicom.PureCodecs

`fo-dicom.PureCodecs` is a pure C# replacement package for `fo-dicom.Codecs`.

The package is being built as one NuGet package that contains separate managed codec-family assemblies for RLE, JPEG, JPEG-LS, and JPEG 2000 / HTJ2K. Production assemblies target `netstandard2.0` only.

## Usage

```csharp
using FellowOakDicom;
using FellowOakDicom.PureCodecs;

new DicomSetupBuilder()
    .RegisterServices(services => services.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

The initial project skeleton includes the package layout and registration entry point. Codec implementations are added in later development phases.
