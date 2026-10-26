#!/usr/bin/env bash
# Package a native library; Android loads it from the APK, with no PRoot process or writable executable.
set -euo pipefail
abi=${1:?Android ABI}
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root"
TABSTUDIO_AI_BUILD_SERVER=0 bash build/local-ai/build.sh "artifacts/local-ai/$abi" "$abi"
mkdir -p "artifacts/native/$abi"
cp "artifacts/local-ai/$abi/bin/libtabstudio-ai.so" "artifacts/native/$abi/libtabstudio-ai.so"
