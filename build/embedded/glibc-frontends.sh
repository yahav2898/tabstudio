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
if [ "$#" -eq 2 ] || [ "${3:-}" = embedded-nrfutil ]; then
  mkdir -p "$work/installer/native"
  # The original Nordic client is x64-only; its dedicated ARM launcher uses the
  # same APK translator rather than installing a second emulation environment.
  if [ "$(uname -m)" = aarch64 ]; then
    python3 "$root/build/pic/build_box64.py" "$work/installer/native"
  fi
fi
if [ "$#" -eq 2 ]; then
  mkdir -p "$work/installer/native" "$work/installer/uno-blink"
  # Same patched source build as the APK, without binfmt or a privileged guest.
  x86_64-linux-gnu-gcc -static -O2 -Wall -Wextra -Werror \
    "$root/build/embedded/box64-check/child-fd.c" -o "$work/installer/uno-blink/boxed-spawn"
  python3 - "$root/build/embedded/tests/test_bridges.py" "$work/installer/uno-blink/serial-client.c" <<'PY'
import runpy, sys
from pathlib import Path
Path(sys.argv[2]).write_text(runpy.run_path(sys.argv[1])["CLIENT"])
PY
  x86_64-linux-gnu-gcc -O2 -Wall -Wextra -Werror \
    "$work/installer/uno-blink/serial-client.c" -o "$work/installer/uno-blink/serial-client-x64"
fi
bash "$root/build/embedded/build-proot-test.sh" "$work/proot"
dotnet run --project "$root/build/embedded/glibc-frontends-check" -- \
  "$work/proot/proot-$PROOT_VERSION/src/proot" \
  "$work/proot/proot-$PROOT_VERSION/src/loader/loader" \
  "$work/installer" "$output" "${@:3}"
