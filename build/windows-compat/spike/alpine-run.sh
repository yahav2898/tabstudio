#!/bin/sh
# Spike: inside an Alpine Linux container, installs Wine, Xvfb and the tools the spike uses, then runs spike.py.
# Usage: alpine-run.sh <x64|arm64> <spike-dir> <out-dir> [wine-tarball]
set -eu
arch="$1"; spike="$2"; out="$3"; wine_tar="${4:-}"
mkdir -p "$out" /etc/apk/cache
echo "== $(cat /etc/alpine-release) $(uname -m)" | tee "$out/packages.txt"
apk update > /dev/null
tools="xvfb xkbcomp xkeyboard-config mesa-dri-gallium mesa-gl xdotool xwininfo python3"
if [ "$arch" = x64 ]; then
  wine="wine"
else
  # The runtime libraries Wine loads (many with dlopen), for a Wine built in build-wine.sh.
  wine="freetype fontconfig libx11 libxext libxrandr libxi libxcursor libxrender libxinerama libxcomposite libxfixes libxxf86vm gnutls libunwind"
fi
start=$(date +%s)
apk add $wine > "$out/apk-wine.txt" 2>&1 || { cat "$out/apk-wine.txt"; exit 1; }
wine_seconds=$(( $(date +%s) - start ))
wine_download=$(du -sk /etc/apk/cache | cut -f1)
tail -n 2 "$out/apk-wine.txt" | tee -a "$out/packages.txt"
start=$(date +%s)
apk add $tools > "$out/apk-tools.txt" 2>&1 || { cat "$out/apk-tools.txt"; exit 1; }
tools_seconds=$(( $(date +%s) - start ))
total_download=$(du -sk /etc/apk/cache | cut -f1)
tail -n 2 "$out/apk-tools.txt" | tee -a "$out/packages.txt"
{
  echo "SPIKE apk_wine_download ${wine_download}KiB in ${wine_seconds}s"
  echo "SPIKE apk_tools_download $((total_download - wine_download))KiB in ${tools_seconds}s"
  echo "SPIKE rootfs_size $(du -sk / -x 2> /dev/null | cut -f1)KiB"
} | tee "$out/results.txt"
if [ "$arch" = x64 ]; then
  apk info -R wine > "$out/wine-depends.txt" 2>&1 || true
  apk info -s wine >> "$out/wine-depends.txt" 2>&1 || true
fi
ls -lS /etc/apk/cache | head -n 40 > "$out/apk-cache.txt"
if [ -n "$wine_tar" ]; then
  mkdir -p /opt/tabstudio
  start=$(date +%s)
  tar -xzf "$wine_tar" -C /opt/tabstudio
  echo "SPIKE wine_tar $(du -k "$wine_tar" | cut -f1)KiB unpacked in $(( $(date +%s) - start ))s to $(du -sk /opt/tabstudio/wine | cut -f1)KiB" | tee -a "$out/results.txt"
fi
python3 -u "$spike/spike.py" "$spike" "$out" --arch "$arch"
