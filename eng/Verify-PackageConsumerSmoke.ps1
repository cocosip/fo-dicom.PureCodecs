param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageId = "fo-dicom.PureCodecs"
$version = "0.1.0-alpha.1"
$packageOutput = Join-Path $repoRoot "artifacts\packages"
$packagePath = Join-Path $packageOutput "$packageId.$version.nupkg"
$mainProject = Join-Path $repoRoot "src\fo-dicom.PureCodecs\fo-dicom.PureCodecs.csproj"
$net472Project = Join-Path $repoRoot "tests\ConsumerSmoke\Net472\ConsumerSmoke.Net472.csproj"
$modernProject = Join-Path $repoRoot "tests\ConsumerSmoke\Modern\ConsumerSmoke.Modern.csproj"
$nugetCacheRoot = Join-Path $repoRoot "artifacts\consumer-smoke\nuget-cache"
$nugetConfigPath = Join-Path $repoRoot "artifacts\consumer-smoke\NuGet.Config"

function Invoke-CheckedDotNet {
    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

New-Item -ItemType Directory -Force -Path $packageOutput | Out-Null
New-Item -ItemType Directory -Force -Path $nugetCacheRoot | Out-Null
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-package" value="$packageOutput" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding UTF8

Invoke-CheckedDotNet pack $mainProject --configuration $Configuration --output $packageOutput

if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Package was not created: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
}
finally {
    $archive.Dispose()
}

$expectedLibs = @(
    "lib/netstandard2.0/fo-dicom.PureCodecs.dll",
    "lib/netstandard2.0/fo-dicom.PureCodecs.Jpeg.dll",
    "lib/netstandard2.0/fo-dicom.PureCodecs.Jpeg2000.dll",
    "lib/netstandard2.0/fo-dicom.PureCodecs.JpegLs.dll",
    "lib/netstandard2.0/fo-dicom.PureCodecs.Rle.dll"
)

foreach ($entry in $expectedLibs) {
    if ($entries -notcontains $entry) {
        throw "Package is missing $entry"
    }
}

$nativeEntries = @($entries | Where-Object {
    $_ -match '(^|/)(runtimes|native)/' -or
    $_ -match '\.(dll|so|dylib)$' -and $_ -notmatch '^lib/netstandard2\.0/fo-dicom\.PureCodecs(\.[^/]+)?\.dll$'
})

if ($nativeEntries.Count -gt 0) {
    throw "Package contains unexpected native/runtime entries: $($nativeEntries -join ', ')"
}

$env:NUGET_PACKAGES = $nugetCacheRoot
Invoke-CheckedDotNet restore $net472Project --configfile $nugetConfigPath
Invoke-CheckedDotNet run --project $net472Project --configuration $Configuration --no-restore
Invoke-CheckedDotNet restore $modernProject --configfile $nugetConfigPath
Invoke-CheckedDotNet run --project $modernProject --configuration $Configuration --no-restore

Write-Host "Package and consumer smoke validation passed for $packagePath"
