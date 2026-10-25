#!/usr/bin/env bash
# Collects what the .NET SDK and Uno.Sdk do for an Uno Platform app, for TabStudio to do the same on the device.
set -uo pipefail
out="${1:?output folder}"
mkdir -p "$out"
work="$(mktemp -d)"
export NUGET_PACKAGES="$work/packages"
cd "$work"

dotnet --version > "$out/dotnet-version.txt" 2>&1
dotnet new install Uno.Templates > "$out/templates-install.txt" 2>&1
dotnet new unoapp --help > "$out/unoapp-help.txt" 2>&1
dotnet new unoapp -preset blank -o UnoApp1 > "$out/new-blank.txt" 2>&1
(cd UnoApp1 && find . -type f | sort > "$out/blank-files.txt" && tar czf "$out/blank-template.tgz" .)
dotnet new unoapp -preset recommended -o UnoRecommended > "$out/new-recommended.txt" 2>&1
(cd UnoRecommended && find . -type f | sort > "$out/recommended-files.txt" && tar czf "$out/recommended-template.tgz" .)

cd UnoApp1/UnoApp1
dotnet build -p:TargetFrameworks=net10.0-desktop -v:n -p:EmitCompilerGeneratedFiles=true > "$out/build-desktop.log" 2>&1
echo "build exit: $?" >> "$out/build-desktop.log"
grep -E "csc\.dll|/csc " "$out/build-desktop.log" | head -n 3 > "$out/csc-command.txt"
obj="obj/Debug/net10.0-desktop"
cp "$obj"/*.editorconfig "$out/" 2>/dev/null
cp "$obj"/*.cs "$out/" 2>/dev/null
find "$obj" -maxdepth 1 -type f | sort > "$out/obj-files.txt"
(find "$obj/generated" -type f | sort > "$out/generated-files.txt"; tar czf "$out/generated.tgz" "$obj/generated") 2>/dev/null
find bin -type f | sort > "$out/bin-files.txt"
cp obj/project.assets.json "$out/project.assets.json" 2>/dev/null
python3 - "$out/project.assets.json" > "$out/packages.txt" <<'PY'
import json, sys
assets = json.load(open(sys.argv[1]))
for name in sorted(assets["libraries"]):
    print(name, assets["libraries"][name].get("type"))
PY
du -s "$NUGET_PACKAGES"/* 2>/dev/null | sort -n -r | head -n 60 > "$out/package-sizes.txt"
find "$NUGET_PACKAGES" -name "*.nupkg" -printf "%s %p\n" | sort -n -r | head -n 40 > "$out/nupkg-sizes.txt"
sdk="$(ls -d "$NUGET_PACKAGES"/uno.sdk/* 2>/dev/null | head -n 1)"
if [ -n "$sdk" ]; then
  find "$sdk" -type f | sort > "$out/uno-sdk-files.txt"
  cp "$sdk/targets/netstandard2.0/packages.json" "$out/uno-sdk-packages.json" 2>/dev/null || find "$sdk" -name packages.json -exec cp {} "$out/uno-sdk-packages.json" \;
fi
winui="$(ls -d "$NUGET_PACKAGES"/uno.winui/* 2>/dev/null | head -n 1)"
[ -n "$winui" ] && find "$winui" -type f -printf "%s %P\n" | sort -k2 > "$out/uno-winui-files.txt"
for p in uno.winrt uno.foundation uno.fonts.opensans uno.fonts.fluent skiasharp harfbuzzsharp uno.winui.runtime.skia uno.winui.runtime.skia.x11 uno.ui.hotdesign uno.resizetizer uno.winui.devserver; do
  d="$(ls -d "$NUGET_PACKAGES/$p"/* 2>/dev/null | head -n 1)"
  [ -n "$d" ] && { echo "== $p $(basename "$d")"; find "$d" -type f -printf "%s %P\n" | sort -k2 | grep -v -E "\.(png|xml)$" | head -n 80; }
done > "$out/other-packages.txt"
echo done > "$out/DONE"
