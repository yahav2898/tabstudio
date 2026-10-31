#!/usr/bin/env bash
# Builds the APK's PRoot (Termux's fork with TabStudio's thread-wait and --net-map patches) for a Linux CI runner,
# so container tests run in the same runtime as the tablet. No Docker, root daemon or namespaces are used; sudo
# only installs build dependencies. Prints and, on GitHub Actions, exports TABSTUDIO_CONTAINER_PROOT/_LOADER.
set -euo pipefail
repo="$(cd "$(dirname "$0")/../.." && pwd)"
native="$repo/build/android/native"
out="$(realpath -m "${1:?usage: containers-proot.sh output-directory}")"
source "$native/sources.sh"
mkdir -p "$out/tmp"
if ! [ -f /usr/include/talloc.h ]; then
  sudo apt-get update -qq && sudo apt-get install -y -qq build-essential libtalloc-dev gawk unzip curl pkg-config > /dev/null
fi
src="$out/proot-$PROOT_VERSION"
if ! [ -x "$src/src/proot" ]; then
  curl -fsSL --retry 3 "$PROOT_URL" -o "$out/proot.zip"
  echo "$PROOT_SHA256  $out/proot.zip" | sha256sum -c -
  rm -rf "$src" && unzip -qo "$out/proot.zip" -d "$out"
  patch -d "$src" -p1 < "$native/proot-thread-waits.patch"
  bash "$native/apply-net-map.sh" "$src"
  printf '#if !defined(__ASSEMBLER__) && __STDC_HOSTED__\n#include <string.h>\n#include <sys/ioctl.h>\n#endif\n' > "$out/includes.h"
  CPPFLAGS="-DARG_MAX=131072 -DVERSION=\\\"$PROOT_VERSION\\\"" CFLAGS="-O2 -include $out/includes.h" PROOT_UNBUNDLE_LOADER=. \
    make -C "$src/src" -j"$(nproc)" HAS_LOADER_32BIT= proot loader/loader > "$out/proot-build.log" 2>&1 || { tail -80 "$out/proot-build.log"; exit 1; }
fi
echo "TABSTUDIO_CONTAINER_PROOT=$src/src/proot"
echo "TABSTUDIO_CONTAINER_LOADER=$src/src/loader/loader"
echo "TABSTUDIO_CONTAINER_TMP=$out/tmp"
if [ -n "${GITHUB_ENV:-}" ]; then
  printf 'TABSTUDIO_CONTAINER_PROOT=%s\nTABSTUDIO_CONTAINER_LOADER=%s\nTABSTUDIO_CONTAINER_TMP=%s\n' "$src/src/proot" "$src/src/loader/loader" "$out/tmp" >> "$GITHUB_ENV"
fi
