param(
    [string]$MinimumFoDicomCodecsVersion = "6.0.0-beta1",
    [string]$PackageSource,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "tests/fo-dicom.PureCodecs.Tests/fo-dicom.PureCodecs.Tests.csproj"
$previousStrictValue = $env:PURECODECS_REQUIRE_FIXED_HTJ2K_MULTIFRAME

function ConvertTo-ComparableVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionText,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $numericVersion = ($VersionText -split '[-+]', 2)[0]
    $parsedVersion = $null
    if (-not [Version]::TryParse($numericVersion, [ref]$parsedVersion)) {
        throw "$Description version '$VersionText' is invalid."
    }

    return $parsedVersion
}

ConvertTo-ComparableVersion $MinimumFoDicomCodecsVersion "Minimum fo-dicom.Codecs" | Out-Null
$packageVersionRange = "[$($MinimumFoDicomCodecsVersion.Trim()),)"
$msbuildPackageVersionRange = $packageVersionRange.Replace(",", "%2C")

function Invoke-CheckedDotNet {
    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

try {
    $restoreArguments = @(
        "restore",
        $testProject,
        "-p:FoDicomCodecsVersion=$msbuildPackageVersionRange"
    )
    if (-not [string]::IsNullOrWhiteSpace($PackageSource)) {
        $effectivePackageSource = if (Test-Path -LiteralPath $PackageSource) {
            (Resolve-Path -LiteralPath $PackageSource).Path
        }
        else {
            $PackageSource
        }
        $restoreArguments += "-p:RestoreAdditionalProjectSources=$effectivePackageSource"
    }

    Invoke-CheckedDotNet @restoreArguments
    $assetsPath = Join-Path (Split-Path -Parent $testProject) "obj/project.assets.json"
    $assets = Get-Content -Raw -LiteralPath $assetsPath | ConvertFrom-Json
    $resolvedPackageKeys = @($assets.libraries.PSObject.Properties.Name | Where-Object {
        $_ -like "fo-dicom.Codecs/*"
    })
    if ($resolvedPackageKeys.Count -ne 1) {
        throw "Expected one resolved fo-dicom.Codecs package in $assetsPath; found $($resolvedPackageKeys.Count)."
    }
    $resolvedPackageVersion = ($resolvedPackageKeys[0] -split '/', 2)[1]

    $env:PURECODECS_REQUIRE_FIXED_HTJ2K_MULTIFRAME = "1"
    Invoke-CheckedDotNet test $testProject `
        --configuration $Configuration `
        --no-restore `
        "-p:FoDicomCodecsVersion=$msbuildPackageVersionRange" `
        --filter "FullyQualifiedName~Htj2k_complete_multiframe_native_decode_is_exact_or_isolated_to_known_reference_pooling_defect" `
        --verbosity minimal

    Write-Host "HTJ2K complete multi-frame upstream verification passed with NuGet package fo-dicom.Codecs $resolvedPackageVersion (range $packageVersionRange)."
}
finally {
    $env:PURECODECS_REQUIRE_FIXED_HTJ2K_MULTIFRAME = $previousStrictValue
}
