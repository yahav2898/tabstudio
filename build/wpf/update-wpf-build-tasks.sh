#!/usr/bin/env bash
# Replaces the WPF build tasks in src/TabStudio.WindowsCompatibility/Building/Wpf/BuildTasks with those of a .NET SDK
# (Sdks/Microsoft.NET.Sdk.WindowsDesktop/tools/net10.0/PresentationBuildTasks.dll) and prints the version and hash
# for NOTICE.txt, which must be updated by hand with them.
#
# Usage: build/wpf/update-wpf-build-tasks.sh <sdk-version>     for example 10.0.402
set -euo pipefail

version="${1:?SDK version, such as 10.0.402}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
target="$repo/src/TabStudio.WindowsCompatibility/Building/Wpf/BuildTasks"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

curl -sSL https://dot.net/v1/dotnet-install.sh -o "$work/dotnet-install.sh"
bash "$work/dotnet-install.sh" --version "$version" --install-dir "$work/dotnet" --no-path > /dev/null
source="$work/dotnet/sdk/$version/Sdks/Microsoft.NET.Sdk.WindowsDesktop/tools/net10.0/PresentationBuildTasks.dll"
test -f "$source" || { echo "SDK $version has no $source." >&2; exit 1; }
cp "$source" "$target/PresentationBuildTasks.dll"

echo "Copied the WPF build tasks of SDK $version. For NOTICE.txt:"
strings -e l "$target/PresentationBuildTasks.dll" | grep -A1 -E '^(ProductVersion|FileVersion)$' || true
(cd "$target" && sha256sum PresentationBuildTasks.dll)
