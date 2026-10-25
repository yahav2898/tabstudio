#!/usr/bin/env bash
# Replaces the Razor compiler in src/TabStudio.Compilation/Razor/Compiler with the one of a .NET SDK, after checking
# that it was built against the Roslyn version TabStudio pins (Microsoft.CodeAnalysis in Directory.Packages.props).
# Prints the hashes for NOTICE.txt, which must be updated by hand with the new version and commit.
#
# Usage: build/razor/update-razor-compiler.sh <sdk-version>     for example 10.0.113
set -euo pipefail

version="${1:?SDK version, such as 10.0.113}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
target="$repo/src/TabStudio.Compilation/Razor/Compiler"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

curl -sSL https://dot.net/v1/dotnet-install.sh -o "$work/dotnet-install.sh"
bash "$work/dotnet-install.sh" --version "$version" --install-dir "$work/dotnet" --no-path > /dev/null
source="$work/dotnet/sdk/$version/Sdks/Microsoft.NET.Sdk.Razor/source-generators"

# The Razor compiler references the Roslyn it was built against; its major.minor must match TabStudio's.
pinned="$(grep -oP 'Include="Microsoft.CodeAnalysis.CSharp" Version="\K[0-9]+\.[0-9]+' "$repo/Directory.Packages.props")"
cat > "$work/roslyn.fsx" <<'FSX'
let assembly = System.Reflection.Assembly.LoadFile fsi.CommandLineArgs.[1]
for reference in assembly.GetReferencedAssemblies() do
    if reference.Name = "Microsoft.CodeAnalysis" then printfn "%d.%d" reference.Version.Major reference.Version.Minor
FSX
shipped="$("$work/dotnet/dotnet" fsi --quiet "$work/roslyn.fsx" "$source/Microsoft.CodeAnalysis.Razor.Compiler.dll" || true)"
if [ -z "$shipped" ] || [ "$shipped" != "$pinned" ]; then
  echo "The Razor compiler of SDK $version needs Roslyn ${shipped:-(unknown)}, but TabStudio pins Microsoft.CodeAnalysis $pinned." >&2
  echo "Pick an SDK from the feature band whose Roslyn matches, or update Microsoft.CodeAnalysis first." >&2
  exit 1
fi

for file in Microsoft.CodeAnalysis.Razor.Compiler.dll Microsoft.AspNetCore.Razor.Utilities.Shared.dll Microsoft.Extensions.ObjectPool.dll; do
  if [ -f "$source/$file" ]; then
    cp "$source/$file" "$target/$file"
  else
    echo "Note: SDK $version has no $file; check the folder's contents and the project's references." >&2
  fi
done

echo "Copied the Razor compiler of SDK $version. Hashes for NOTICE.txt:"
(cd "$target" && sha256sum ./*.dll)
