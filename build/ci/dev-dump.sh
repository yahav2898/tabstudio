#!/usr/bin/env bash
# Trial only: asks the real MSBuild which AdditionalFiles a few projects give their generators.
set -u
out="$GITHUB_WORKSPACE/ci-out/dump.txt"
mkdir -p "$GITHUB_WORKSPACE/ci-out"
probe() { # name url sha project framework
  dir="/tmp/probe/$1"; git init -q "$dir"; git -C "$dir" fetch -q --depth 1 "$2" "$3"; git -C "$dir" -c advice.detachedHead=false checkout -q FETCH_HEAD
  for g in $(find "$dir" -name global.json); do python3 -c "import json,sys;p=sys.argv[1];d=json.load(open(p,encoding='utf-8-sig'));d.pop('sdk',None);json.dump(d,open(p,'w'))" "$g"; done
  echo "=================== $1 $4" >> "$out"
  ( cd "$dir" && dotnet restore "$4" -p:EnableWindowsTargeting=true > /tmp/probe/$1.restore.log 2>&1; tail -3 /tmp/probe/$1.restore.log >> "$out"
    dotnet msbuild "$4" -t:GenerateMSBuildEditorConfigFileShouldRun -p:TargetFramework=$5 -getItem:AdditionalFiles -getItem:EmbeddedResource -getProperty:RootNamespace >> "$out" 2>&1 )
}
probe MudBlazor https://github.com/MudBlazor/MudBlazor ed6b3e023a4780c29c7951da580751733227892a src/MudBlazor/MudBlazor.csproj net10.0
probe moq https://github.com/devlooped/moq bdf16e758e8022c40cef2bfd025181f845bbd2a9 src/Moq.Sdk/Moq.Sdk.csproj netstandard2.0
find "$NUGET_PACKAGES" -maxdepth 1 -iname 'microsoft.codeanalysis.analyzers' -exec find {} -name '*.targets' \; | head -3 | while read -r f; do echo "----- $f" >> "$out"; grep -n -i -B2 -A12 "resx" "$f" | head -60 >> "$out"; done
