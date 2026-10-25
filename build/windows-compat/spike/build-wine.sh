#!/bin/sh
# Spike: builds Wine for Alpine Linux on aarch64, with its Windows side as ARM64 PE files compiled by Clang, into
# /opt/tabstudio/wine, and packs it as <out>/wine.tar.gz. Runs in an alpine container on an arm64 machine.
# Usage: build-wine.sh <out-dir> [version]
set -eu
out="$1"; version="${2:-11.0}"
mkdir -p "$out"
if [ -f "$out/wine.tar.gz" ]; then echo "Wine $version is already built."; exit 0; fi
start=$(date +%s)
apk add build-base clang lld llvm flex bison perl linux-headers curl xz \
  freetype-dev fontconfig-dev libx11-dev libxext-dev libxrandr-dev libxi-dev libxcursor-dev libxrender-dev \
  libxinerama-dev libxcomposite-dev libxfixes-dev libxxf86vm-dev mesa-dev gnutls-dev libunwind-dev > "$out/apk.txt" 2>&1 \
  || { tail -n 30 "$out/apk.txt"; exit 1; }
curl -fsSL "https://gitlab.alpinelinux.org/alpine/aports/-/raw/3.24-stable/community/wine/APKBUILD" -o "$out/alpine-APKBUILD.txt" || true
cd /tmp
curl -fsSL "https://dl.winehq.org/wine/source/$version/wine-$version.tar.xz" -o wine.tar.xz \
  || curl -fsSL "https://dl.winehq.org/wine/source/${version%%.*}.x/wine-$version.tar.xz" -o wine.tar.xz
tar -xJf wine.tar.xz
cd "wine-$version"
CFLAGS="-O2" CROSSCFLAGS="-O2" ./configure --prefix=/opt/tabstudio/wine --disable-tests \
  --without-wayland --without-alsa --without-pulse --without-oss --without-gstreamer --without-cups --without-sane \
  --without-krb5 --without-netapi --without-pcap --without-usb --without-v4l2 --without-capi --without-gphoto \
  --without-sdl --without-udev --without-dbus --without-vulkan > "$out/configure.txt" 2>&1 || { tail -n 60 "$out/configure.txt"; exit 1; }
tail -n 40 "$out/configure.txt"
grep -iE "cross|mingw|PE |aarch64-windows|target" config.log | head -n 40 > "$out/configure-cross.txt" || true
configured=$(date +%s)
make -j"$(nproc)" > "$out/make.txt" 2>&1 || { tail -n 80 "$out/make.txt"; exit 1; }
built=$(date +%s)
make install-lib DESTDIR=/tmp/dest > "$out/install.txt" 2>&1 || { tail -n 40 "$out/install.txt"; exit 1; }
find /tmp/dest/opt/tabstudio/wine -type f -name '*.so' -exec strip --strip-unneeded {} + 2> /dev/null || true
for program in /tmp/dest/opt/tabstudio/wine/bin/*; do strip --strip-unneeded "$program" 2> /dev/null || true; done
tar -czf "$out/wine.tar.gz" -C /tmp/dest/opt/tabstudio wine
{
  echo "SPIKE wine_build_configure_seconds $((configured - start))"
  echo "SPIKE wine_build_make_seconds $((built - configured)) on $(nproc) cores"
  echo "SPIKE wine_build_installed $(du -sk /tmp/dest/opt/tabstudio/wine | cut -f1)KiB"
  echo "SPIKE wine_build_tarball $(du -k "$out/wine.tar.gz" | cut -f1)KiB"
  echo "SPIKE wine_build_pe_dlls $(find /tmp/dest/opt/tabstudio/wine/lib/wine -path '*-windows*' -name '*.dll' | wc -l)"
  ls /tmp/dest/opt/tabstudio/wine/lib/wine
} | tee "$out/build-results.txt"
