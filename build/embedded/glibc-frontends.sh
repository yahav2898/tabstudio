#!/usr/bin/env bash
# Executes the real Toolchain Store glibc installer and runtime on the native
# x64/ARM64 CI host. No fake guest runtime, Docker, QEMU, USB board or PC helper.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
work=${1:?usage: glibc-frontends.sh PRIVATE_WORK_DIRECTORY EVIDENCE_DIRECTORY}
output=${2:?usage: glibc-frontends.sh PRIVATE_WORK_DIRECTORY EVIDENCE_DIRECTORY}
work="$(realpath -m "$work")"
output="$(realpath -m "$output")"
mkdir -p "$work" "$output"
source "$root/build/android/native/sources.sh"
bash "$root/build/embedded/build-proot-test.sh" "$work/proot"
dotnet run --project "$root/build/embedded/glibc-frontends-check" -- \
  "$work/proot/proot-$PROOT_VERSION/src/proot" \
  "$work/proot/proot-$PROOT_VERSION/src/loader/loader" \
  "$work/installer" "$output" "${@:3}"
