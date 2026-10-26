#!/usr/bin/env bash
# Build the APK's exact patched PRoot on the CI host and test raw USBFD opens.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
native="$root/build/android/native"
source "$native/sources.sh"
source "$native/fetch.sh"
work=${1:?usage: build-proot-test.sh WORK_DIRECTORY}
mkdir -p "$work"
fetch "$PROOT_URL" "$PROOT_SHA256" "$work/proot.zip"
unzip -q "$work/proot.zip" -d "$work"
source_dir="$work/proot-$PROOT_VERSION"
patch -d "$source_dir" -p1 < "$native/proot-thread-waits.patch"
patch -d "$source_dir" -p1 < "$native/proot-usb-fd.patch"
PROOT_UNBUNDLE_LOADER=. make -C "$source_dir/src" -j2 HAS_LOADER_32BIT= proot loader/loader
export PROOT_LOADER="$source_dir/src/loader/loader"
python3 "$root/build/embedded/test-proot-usb.py" --proot "$source_dir/src/proot"
PROOT_NO_SECCOMP=1 python3 "$root/build/embedded/test-proot-usb.py" --proot "$source_dir/src/proot"
if [[ -n "${GITHUB_ENV:-}" ]]; then
  printf 'TABSTUDIO_TEST_PROOT=%s\n' "$source_dir/src/proot" >> "$GITHUB_ENV"
  printf 'PROOT_LOADER=%s\n' "$PROOT_LOADER" >> "$GITHUB_ENV"
fi
