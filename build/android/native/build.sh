#!/usr/bin/env bash
# Builds the native programs that TabStudio packages into its APK to run toolchains on Android: PRoot (Termux's
# fork, the one Termux itself ships) and PRoot's loader, with talloc linked in statically.
#
# Why they are packaged as libraries: Android does not let an app execute files it wrote to its own storage, but
# it does let it execute files in its native library folder, which the package installer fills from the APK's
# lib/<abi>/lib*.so entries. So the two programs are named libproot.so and libproot-loader.so, and
# src/TabStudio.Android/Toolchains.targets adds them to the APK. Every program a toolchain runs is started through
# PRoot, which hands each execve() to the loader; the loader maps the program itself (see
# src/TabStudio.Toolchains/Runtimes/ProotRuntime.cs).
#
# Sources are downloaded at pinned versions and checked against their SHA-256 hashes, the same ones Termux uses.
# PRoot is GPL-2.0-or-later and talloc LGPL-3.0-or-later; both are separate programs inside the APK, built from
# the sources named here, with the local thread-wait patch for Linux ptrace semantics.
#
# Usage: build/android/native/build.sh <output-folder> <abi>...
#   abi: arm64-v8a, armeabi-v7a or x86_64. Each one lands in <output-folder>/<abi>/; an ABI that is already
#   built at these versions is skipped, so CI can cache the folder.
# Needs: the Android NDK ($ANDROID_NDK_HOME, $ANDROID_NDK_LATEST_HOME, $ANDROID_NDK_ROOT or the newest under
# $ANDROID_HOME/ndk), make, python3 (talloc's build system), curl, unzip, patch, readelf and gawk.
set -euo pipefail

native="$(cd "$(dirname "$0")" && pwd)"
source "$native/sources.sh"
source "$native/fetch.sh"
# TabStudio's minimum Android version (API 28, Android 9).
API=28
patch_sha="$(sha256sum "$native/proot-thread-waits.patch" | cut -d ' ' -f1)"
usb_patch_sha="$(sha256sum "$native/proot-usb-fd.patch" | cut -d ' ' -f1)"
wch_guard_sha="$(cat "$native/proot-wch-guard.patch" "$native/apply-wch-guard.sh" "$native/../../../native/embedded/wch_guard.c" "$native/../../../native/embedded/wch_guard.h" "$native/../../../native/embedded/wch_loader.h" "$native/../../../native/embedded/wch_usbfs.c" "$native/../../../native/embedded/wch_usbfs.h" | sha256sum | cut -d ' ' -f1)"
shim_hash="$(sha256sum "$native/jvm-mapping.c" | cut -d ' ' -f1)"
net_map_sha="$(cat "$native/proot-net-map.patch" "$native/apply-net-map.sh" "$native/../../../native/containers/net_map.c" | sha256sum | cut -d ' ' -f1)"
STAMP="proot ${PROOT_VERSION}, talloc ${TALLOC_VERSION}, API ${API}, 16 KB pages, waits ${patch_sha}, USB FD ${usb_patch_sha}, WCH guard ${wch_guard_sha}, JVM mapping ${shim_hash}, net map ${net_map_sha}"

out="$(realpath -m "${1:?usage: build.sh <output-folder> <abi>...}")"
shift
[ $# -gt 0 ] || { echo "build.sh: name at least one ABI (arm64-v8a, armeabi-v7a, x86_64)." >&2; exit 2; }

find_ndk() {
  local candidate
  for candidate in "${ANDROID_NDK_HOME:-}" "${ANDROID_NDK_LATEST_HOME:-}" "${ANDROID_NDK_ROOT:-}"; do
    if [ -n "$candidate" ] && [ -d "$candidate/toolchains/llvm" ]; then
      echo "$candidate"
      return
    fi
  done
  if [ -n "${ANDROID_HOME:-}" ] && [ -d "$ANDROID_HOME/ndk" ]; then
    candidate="$(find "$ANDROID_HOME/ndk" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -n 1)"
    if [ -n "$candidate" ] && [ -d "$candidate/toolchains/llvm" ]; then
      echo "$candidate"
      return
    fi
  fi
  echo "build.sh: no Android NDK found; set ANDROID_NDK_HOME." >&2
  exit 1
}

ndk="$(find_ndk)"
toolchain="$ndk/toolchains/llvm/prebuilt/linux-x86_64"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
echo "NDK: $ndk"

# PRoot's build reads the loader's symbols with readelf and gawk's strtonum(); talloc's build must not find the
# build machine's own libraries through pkg-config.
tools="$work/tools"
mkdir -p "$tools"
command -v gawk > /dev/null || sudo apt-get install -y -qq gawk > /dev/null
ln -s "$(command -v gawk)" "$tools/awk"
command -v readelf > /dev/null || ln -s "$toolchain/bin/llvm-readelf" "$tools/readelf"
printf '#!/bin/sh\nexit 1\n' > "$tools/pkg-config"
chmod +x "$tools/pkg-config"
export PATH="$tools:$PATH"

downloads="$work/downloads"
mkdir -p "$downloads"

build_abi() {
  local abi="$1" target
  case "$abi" in
    arm64-v8a) target=aarch64-linux-android ;;
    armeabi-v7a) target=armv7a-linux-androideabi ;;
    x86_64) target=x86_64-linux-android ;;
    *) echo "build.sh: unknown ABI '$abi'." >&2; exit 2 ;;
  esac

  local dest="$out/$abi"
  if [ -f "$dest/VERSION" ] && [ "$(cat "$dest/VERSION")" = "$STAMP" ]; then
    echo "== $abi: already built ($STAMP)"
    return
  fi

  echo "== $abi: building $STAMP"
  fetch "$PROOT_URL" "$PROOT_SHA256" "$downloads/proot.zip" \
    "https://codeload.github.com/termux/proot/zip/refs/tags/v${PROOT_VERSION}"
  fetch "$TALLOC_URL" "$TALLOC_SHA256" "$downloads/talloc.tar.gz" \
    "https://download.samba.org/pub/talloc/talloc-${TALLOC_VERSION}.tar.gz"

  local dir="$work/$abi"
  local static="$dir/static"
  mkdir -p "$dir" "$static/include" "$static/lib"

  export CC="$toolchain/bin/${target}${API}-clang"
  export AR="$toolchain/bin/llvm-ar"
  export RANLIB="$toolchain/bin/llvm-ranlib"
  export STRIP="$toolchain/bin/llvm-strip"
  export OBJCOPY="$toolchain/bin/llvm-objcopy"
  export OBJDUMP="$toolchain/bin/llvm-objdump"

  # talloc, as a static library: configure with the answers a cross build cannot find out by running programs,
  # then compile talloc.c and the replacements it needs for functions the C library may lack (lib/replace) against
  # the configuration that produced.
  tar -xzf "$downloads/talloc.tar.gz" -C "$dir"
  (
    cd "$dir/talloc-$TALLOC_VERSION"
    cat > cross-answers.txt <<'EOF'
Checking uname sysname type: "Linux"
Checking uname machine type: "dontcare"
Checking uname release type: "dontcare"
Checking uname version type: "dontcare"
Checking simple C program: OK
building library support: OK
Checking for large file support: OK
Checking for -D_FILE_OFFSET_BITS=64: OK
Checking for WORDS_BIGENDIAN: OK
Checking for C99 vsnprintf: OK
Checking for HAVE_SECURE_MKSTEMP: OK
rpath library support: OK
-Wl,--version-script support: FAIL
Checking correct behavior of strtoll: OK
Checking correct behavior of strptime: OK
Checking for HAVE_IFACE_GETIFADDRS: OK
Checking for HAVE_IFACE_IFCONF: OK
Checking for HAVE_IFACE_IFREQ: OK
Checking getconf LFS_CFLAGS: OK
Checking for large file support without additional flags: OK
Checking for working strptime: OK
Checking for HAVE_SHARED_MMAP: OK
Checking for HAVE_MREMAP: OK
Checking for HAVE_INCOHERENT_MMAP: OK
Checking getconf large file support flags work: OK
EOF
    CFLAGS="-O2 -fPIC" ./configure --prefix="$dir/prefix" --disable-rpath --disable-python \
      --cross-compile --cross-answers=cross-answers.txt > "$dir/talloc-configure.log" 2>&1 \
      || { tail -n 40 "$dir/talloc-configure.log"; exit 1; }
    local flags=(-O2 -fPIC -D_GNU_SOURCE=1 -D_XOPEN_SOURCE_EXTENDED=1 -D__STDC_WANT_LIB_EXT1__=1 -Ibin/default -I. -Ilib/replace)
    "$CC" "${flags[@]}" -c talloc.c -o talloc.o
    "$CC" "${flags[@]}" -c lib/replace/replace.c -o replace.o
    "$AR" rcs "$static/lib/libtalloc.a" talloc.o replace.o
    cp talloc.h "$static/include/"
  )

  # PRoot with an unbundled loader: TabStudio passes the loader's path in PROOT_LOADER. 64-bit builds are
  # aligned for devices with 16 KB memory pages.
  unzip -q "$downloads/proot.zip" -d "$dir"
  (cd "$dir/proot-$PROOT_VERSION" && patch -p1 < "$native/proot-thread-waits.patch")
  (cd "$dir/proot-$PROOT_VERSION" && patch -p1 < "$native/proot-usb-fd.patch")
  bash "$native/apply-wch-guard.sh" "$dir/proot-$PROOT_VERSION"
  bash "$native/apply-net-map.sh" "$dir/proot-$PROOT_VERSION"
  (
    cd "$dir/proot-$PROOT_VERSION/src"
    local pages=""
    case "$abi" in arm64-v8a|x86_64) pages="-Wl,-z,max-page-size=16384" ;; esac
    export CPPFLAGS="-DARG_MAX=131072 -DVERSION=\\\"${PROOT_VERSION}\\\""
    # Two headers some Android-only files use without including them: string.h (extension/ashmem_memfd.c) and
    # sys/ioctl.h (extension/sysvipc/sysvipc_shm.c). Not for the loader, which is freestanding C and assembly
    # built with the same flags.
    printf '#if !defined(__ASSEMBLER__) && __STDC_HOSTED__\n#include <string.h>\n#include <sys/ioctl.h>\n#endif\n' > "$dir/android-includes.h"
    export CFLAGS="-I$static/include -O2 -include $dir/android-includes.h -Werror=implicit-function-declaration"
    export LDFLAGS="-L$static/lib $pages"
    export LOADER_LDFLAGS="$pages"
    export PROOT_UNBUNDLE_LOADER=.
    make -j"$(nproc)" V=1 proot loader/loader > "$dir/proot-build.log" 2>&1 || { tail -n 60 "$dir/proot-build.log"; exit 1; }

    mkdir -p "$dest"
    "$STRIP" -o "$dest/libproot.so" proot
    "$STRIP" -o "$dest/libproot-loader.so" loader/loader
  )

  # A freestanding musl preload library: no dependency on bionic, which cannot be loaded inside Alpine.
  local shim_pages=""
  case "$abi" in arm64-v8a|x86_64) shim_pages="-Wl,-z,max-page-size=16384" ;; esac
  "$CC" -shared -nostdlib -fPIC -O2 -fno-stack-protector -fno-unwind-tables -fno-asynchronous-unwind-tables \
    -Wall -Wextra -Werror $shim_pages -Wl,-soname,libtabstudio-jvm.so \
    "$native/jvm-mapping.c" -o "$dest/libtabstudio-jvm.so"

  echo "$STAMP" > "$dest/VERSION"
  file "$dest"/*.so 2> /dev/null || ls -l "$dest"
}

for abi in "$@"; do
  build_abi "$abi"
  if [ "$abi" = arm64-v8a ]; then
    # box64 is a standalone GNU/Linux program mapped by PRoot's loader. It uses
    # the Store's glibc runtime rather than Android's JNI/Bionic loader.
    command -v aarch64-linux-gnu-gcc > /dev/null || sudo apt-get install -y -qq gcc-aarch64-linux-gnu > /dev/null
    command -v cmake > /dev/null || sudo apt-get install -y -qq cmake > /dev/null
    python3 "$(dirname "$0")/../../pic/build_box64.py" "$out/$abi"
  fi
done
