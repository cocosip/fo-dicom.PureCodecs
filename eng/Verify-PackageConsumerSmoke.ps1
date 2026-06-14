param(
    [string]$Configuration = "Release",
    [switch]$SkipNet472,
    [switch]$RequireNet472
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageId = "fo-dicom.PureCodecs"
$packageOutput = Join-Path $repoRoot "artifacts/packages"
$mainProject = Join-Path $repoRoot "src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj"
$net472Project = Join-Path $repoRoot "tests/ConsumerSmoke/Net472/ConsumerSmoke.Net472.csproj"
$modernProject = Join-Path $repoRoot "tests/ConsumerSmoke/Modern/ConsumerSmoke.Modern.csproj"
$nugetCacheRoot = Join-Path $repoRoot "artifacts/consumer-smoke/nuget-cache"
$nugetConfigPath = Join-Path $repoRoot "artifacts/consumer-smoke/NuGet.Config"
$isWindows = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT

function Invoke-CheckedDotNet {
    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$version = Invoke-CheckedDotNet msbuild $mainProject "-getProperty:Version"
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read <Version> from $mainProject"
}

$packagePath = Join-Path $packageOutput "$packageId.$version.nupkg"

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
if ($SkipNet472) {
    Write-Host ".NET Framework 4.7.2 consumer smoke skipped by -SkipNet472."
}
elseif ($isWindows) {
    Invoke-CheckedDotNet restore $net472Project --configfile $nugetConfigPath
    Invoke-CheckedDotNet run --project $net472Project --configuration $Configuration --no-restore
}
elseif ($RequireNet472) {
    throw ".NET Framework 4.7.2 consumer smoke requires Windows. Run this script on a Windows CI job."
}
else {
    Write-Host ".NET Framework 4.7.2 consumer smoke skipped because this host is not Windows."
}

Invoke-CheckedDotNet restore $modernProject --configfile $nugetConfigPath
Invoke-CheckedDotNet run --project $modernProject --configuration $Configuration --no-restore

Write-Host "Package and consumer smoke validation passed for $packagePath"
