#!/usr/bin/env bash
# Trial only: prints the build files of a few packages, to read what their targets do.
set -u
out=ci-out/dump.txt
mkdir -p ci-out /tmp/dump && cd /tmp/dump
dotnet new console -o app --force > /dev/null 2>&1
cd app
for p in "Microsoft.CodeAnalysis.ResxSourceGenerator 3.11.0-beta1.26226.5" "ThisAssembly.Strings 2.1.6" "ThisAssembly.Resources 2.1.6" "MSTest.TestFramework 3.6.4" "MSTest.TestFramework 2.2.10" "Wcwidth.Sources 2.0.0" "ManualStunts 1.0.0-beta.3" "Stunts 1.0.0-beta.3"; do
  set -- $p
  dotnet add package "$1" --version "$2" > /dev/null 2>&1 || echo "could not add $1 $2" >> "$GITHUB_WORKSPACE/$out"
done
cd "$GITHUB_WORKSPACE"
for dir in "$NUGET_PACKAGES"/microsoft.codeanalysis.resxsourcegenerator "$NUGET_PACKAGES"/thisassembly.strings "$NUGET_PACKAGES"/thisassembly.resources "$NUGET_PACKAGES"/mstest.testframework "$NUGET_PACKAGES"/wcwidth.sources "$NUGET_PACKAGES"/manualstunts "$NUGET_PACKAGES"/stunts; do
  echo "=================== $dir" >> "$out"
  find "$dir" -type f | sed "s|$NUGET_PACKAGES/||" | head -80 >> "$out"
  find "$dir" -type f \( -name '*.props' -o -name '*.targets' -o -name '*.nuspec' \) | while read -r f; do echo "----- $f" >> "$out"; head -c 6000 "$f" >> "$out"; echo >> "$out"; done
done
