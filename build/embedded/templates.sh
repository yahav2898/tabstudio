#!/usr/bin/env bash
# Hardware-free compilation of the app's canonical PlatformIO starters through the real tablet runtime.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
work=${1:?usage: templates.sh PRIVATE_WORK EVIDENCE_DIRECTORY GROUP}
output=${2:?usage: templates.sh PRIVATE_WORK EVIDENCE_DIRECTORY GROUP}
group=${3:?usage: templates.sh PRIVATE_WORK EVIDENCE_DIRECTORY GROUP}
work="$(realpath -m "$work")"
output="$(realpath -m "$output")"
mkdir -p "$work/native" "$output"
source "$root/build/android/native/sources.sh"
if [ "$(uname -m)" = aarch64 ]; then
  python3 "$root/build/pic/build_box64.py" "$work/native"
fi
bash "$root/build/embedded/build-proot-test.sh" "$work/proot"
dotnet run --project "$root/build/embedded/template-build-check" -- \
  "$work/proot/proot-$PROOT_VERSION/src/proot" \
  "$work/proot/proot-$PROOT_VERSION/src/loader/loader" \
  "$work" "$output" "$group" auto
