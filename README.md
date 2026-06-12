# fo-dicom.PureCodecs

`fo-dicom.PureCodecs` is a pure C# replacement package for `fo-dicom.Codecs`.

The package is being built as one NuGet package that contains separate managed codec-family assemblies for RLE, JPEG, JPEG-LS, and JPEG 2000 / HTJ2K. Production assemblies target `netstandard2.0` only.

NuGet package versions are managed centrally in `Directory.Packages.props`. Project files keep `PackageReference` items versionless; add or update package versions in `Directory.Packages.props`.

## Usage

```csharp
using FellowOakDicom;
using FellowOakDicom.PureCodecs;

new DicomSetupBuilder()
    .RegisterServices(services => services.AddFellowOakDicom()
        .AddTranscoderManager<PureTranscoderManager>())
    .Build();
```

The project includes the package layout, registration entry point, centralized package version management, and the first RLE codec implementation. Remaining codec families are added in later development phases.
