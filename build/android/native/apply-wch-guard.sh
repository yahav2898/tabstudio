#!/usr/bin/env bash
# Apply the independently auditable protocol broker to the pinned PRoot tree.
set -euo pipefail
native="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$native/../../.." && pwd)"
source_dir=${1:?usage: apply-wch-guard.sh PINNED_PROOT_SOURCE}
patch -d "$source_dir" -p1 < "$native/proot-wch-guard.patch"
for name in wch_guard.c wch_guard.h wch_loader.h wch_usbfs.c wch_usbfs.h; do
  cp "$repo/native/embedded/$name" "$source_dir/src/syscall/$name"
done
