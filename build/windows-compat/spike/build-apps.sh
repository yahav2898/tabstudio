#!/usr/bin/env bash
# Spike: builds the Windows Forms and WPF spike apps with the .NET SDK on Linux (EnableWindowsTargeting), and records
# what the WPF build used: the SDK's PresentationBuildTasks and the markup compiler's outputs.
# Usage: build-apps.sh <apps-out> <logs-out>
set -euo pipefail
apps="$(realpath -m "$1")"; logs="$(realpath -m "$2")"
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
mkdir -p "$apps" "$logs"
cp -r "$here/apps/." "$work/"
for app in SpikeForms SpikeWpf; do
  dotnet build "$work/$app" -c Release -v:n -bl:"$logs/$app.binlog" > "$logs/$app-build.log" 2>&1 || { tail -n 60 "$logs/$app-build.log"; exit 1; }
  mkdir -p "$apps/$app"
  cp -r "$work/$app/bin/Release/net10.0-windows/." "$apps/$app/"
done
sdk="$(dirname "$(command -v dotnet)")"
sdk="$(dirname "$(readlink -f "$(command -v dotnet)")")"
{
  dotnet --info | head -n 30
  echo "== WindowsDesktop SDK"
  find "$sdk/sdk" -path '*Microsoft.NET.Sdk.WindowsDesktop*' -type f | sed "s|$sdk/||" | sort
  echo "== Markup compiler outputs"
  find "$work/SpikeWpf/obj" -type f | sed "s|$work/||" | sort
  echo "== Targeting packs"
  ls "$sdk/packs" 2> /dev/null || true
  ls ~/.nuget/packages | grep -i windowsdesktop || true
} > "$logs/sdk-info.txt" 2>&1
mkdir -p "$logs/wpf-obj" "$logs/pbt"
cp -r "$work/SpikeWpf/obj/Release/net10.0-windows/." "$logs/wpf-obj/" || true
find "$sdk/sdk" -path '*Microsoft.NET.Sdk.WindowsDesktop/tools*' -type f \( -name '*.dll' -o -name '*.targets' -o -name '*.props' -o -name '*.json' \) -exec cp --parents {} "$logs/pbt/" \; 2> /dev/null || true
find "$sdk/sdk" -path '*Microsoft.NET.Sdk.WindowsDesktop/targets*' -type f -exec cp --parents {} "$logs/pbt/" \; 2> /dev/null || true
ls -la "$apps"/*
