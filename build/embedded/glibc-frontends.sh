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
if [ "$#" -eq 2 ]; then
  mkdir -p "$work/installer/native" "$work/installer/uno-blink"
  # Same patched source build as the APK, without binfmt or a privileged guest.
  if [ "$(uname -m)" = aarch64 ]; then
    python3 "$root/build/pic/build_box64.py" "$work/installer/native"
  fi
  x86_64-linux-gnu-gcc -static -O2 -Wall -Wextra -Werror \
    "$root/build/embedded/box64-check/child-fd.c" -o "$work/installer/uno-blink/boxed-spawn"
fi
bash "$root/build/embedded/build-proot-test.sh" "$work/proot"
dotnet run --project "$root/build/embedded/glibc-frontends-check" -- \
  "$work/proot/proot-$PROOT_VERSION/src/proot" \
  "$work/proot/proot-$PROOT_VERSION/src/loader/loader" \
  "$work/installer" "$output" "${@:3}"
