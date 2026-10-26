#!/usr/bin/env bash
# Exercise the APK's PRoot patch on Linux before the longer emulator run.
set -euo pipefail
native="$(cd "$(dirname "$0")/.." && pwd)"
source "$native/sources.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
curl -fsSL --connect-timeout 20 --max-time 120 --retry 3 -o "$work/proot.zip" "$PROOT_URL"
echo "$PROOT_SHA256  $work/proot.zip" | sha256sum -c --quiet -
unzip -q "$work/proot.zip" -d "$work"
cd "$work/proot-$PROOT_VERSION"
patch -p1 < "$native/proot-thread-waits.patch"
make -C src -j2 > "$work/build.log" 2>&1 || { tail -n 60 "$work/build.log"; exit 1; }
cc -std=c11 -Wall -Wextra -Werror -pthread "$native/tests/ptrace-threads.c" -o "$work/ptrace-threads"
# The same fixture must pass on the kernel and under PRoot. No retry can hide a failure.
timeout -k 5 25 "$work/ptrace-threads"
timeout -k 5 25 src/proot -r / "$work/ptrace-threads"
