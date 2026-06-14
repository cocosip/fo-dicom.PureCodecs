#!/usr/bin/env bash
set -euo pipefail

configuration="Release"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for --configuration." >&2
        exit 2
      fi
      configuration="$2"
      shift 2
      ;;
    -h|--help)
      echo "Usage: $0 [--configuration Release|Debug]"
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required to run package consumer smoke validation." >&2
  exit 1
fi

if ! command -v unzip >/dev/null 2>&1; then
  echo "unzip is required to inspect the generated NuGet package." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
package_id="fo-dicom.PureCodecs"
main_project="$repo_root/src/fo-dicom.PureCodecs/fo-dicom.PureCodecs.csproj"
modern_project="$repo_root/tests/ConsumerSmoke/Modern/ConsumerSmoke.Modern.csproj"
package_output="$repo_root/artifacts/packages"
nuget_cache_root="$repo_root/artifacts/consumer-smoke/nuget-cache"
nuget_config_path="$repo_root/artifacts/consumer-smoke/NuGet.Config"

version="$(dotnet msbuild "$main_project" -getProperty:Version)"
if [[ -z "$version" ]]; then
  echo "Could not read <Version> from $main_project." >&2
  exit 1
fi

package_path="$package_output/$package_id.$version.nupkg"

invoke_checked_dotnet() {
  dotnet "$@"
}

mkdir -p "$package_output" "$nuget_cache_root" "$(dirname "$nuget_config_path")"
cat > "$nuget_config_path" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-package" value="$package_output" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

invoke_checked_dotnet pack "$main_project" --configuration "$configuration" --output "$package_output"

if [[ ! -f "$package_path" ]]; then
  echo "Package was not created: $package_path" >&2
  exit 1
fi

package_entries="$(unzip -Z1 "$package_path")"

expected_libs=(
  "lib/netstandard2.0/fo-dicom.PureCodecs.dll"
  "lib/netstandard2.0/fo-dicom.PureCodecs.Jpeg.dll"
  "lib/netstandard2.0/fo-dicom.PureCodecs.Jpeg2000.dll"
  "lib/netstandard2.0/fo-dicom.PureCodecs.JpegLs.dll"
  "lib/netstandard2.0/fo-dicom.PureCodecs.Rle.dll"
)

for entry in "${expected_libs[@]}"; do
  if ! grep -Fxq "$entry" <<< "$package_entries"; then
    echo "Package is missing $entry" >&2
    exit 1
  fi
done

native_entries="$(
  grep -E '(^|/)(runtimes|native)/|\.(dll|so|dylib)$' <<< "$package_entries" \
    | grep -Ev '^lib/netstandard2\.0/fo-dicom\.PureCodecs(\.[^/]+)?\.dll$' \
    || true
)"

if [[ -n "$native_entries" ]]; then
  echo "Package contains unexpected native/runtime entries:" >&2
  echo "$native_entries" >&2
  exit 1
fi

export NUGET_PACKAGES="$nuget_cache_root"
invoke_checked_dotnet restore "$modern_project" --configfile "$nuget_config_path"
invoke_checked_dotnet run --project "$modern_project" --configuration "$configuration" --no-restore

echo "Package and modern consumer smoke validation passed for $package_path"
