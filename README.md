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

## Package Consumer Smoke Validation

The repository includes smoke scripts that pack `fo-dicom.PureCodecs`, install the generated NuGet package into sample consumer apps, and verify that the package contains only the expected managed `netstandard2.0` assemblies.

Use the platform-native script in CI:

```bash
bash ./eng/verify-package-consumer-smoke.sh
```

The shell script is intended for Linux and macOS jobs. It validates the package layout and the modern .NET consumer smoke test.

```powershell
.\eng\Verify-PackageConsumerSmoke.ps1 -RequireNet472
```

The PowerShell script is intended for Windows jobs. It validates the same package checks and modern consumer smoke test, and also runs the .NET Framework 4.7.2 consumer smoke test.
