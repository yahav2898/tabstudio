#!/usr/bin/env bash
# SPIKE (temporary): records the hashes and versions the toolchain catalog pins.
set -uo pipefail
out="${1:?output file}"
{
  for arch in x86_64 aarch64 armv7; do
    echo "== alpine $arch"
    curl -fsSL "https://dl-cdn.alpinelinux.org/alpine/v3.24/releases/$arch/latest-releases.yaml" | grep -A8 "flavor: alpine-minirootfs" | grep -E "file:|version:|sha256:|size:"
  done
  echo "== js-debug"
  curl -fsSL https://api.github.com/repos/microsoft/vscode-js-debug/releases/latest | grep -E '"tag_name"|browser_download_url.*dap' | head -n 4
  url="$(curl -fsSL https://api.github.com/repos/microsoft/vscode-js-debug/releases/latest | grep -oE 'https://[^"]*js-debug-dap-v[^"]*\.tar\.gz' | head -n 1)"
  echo "url $url"; curl -fsSL "$url" -o /tmp/js-debug.tgz && sha256sum /tmp/js-debug.tgz && stat -c %s /tmp/js-debug.tgz && tar -tzf /tmp/js-debug.tgz | head -n 5
  echo "== kotlin"
  tag="$(curl -fsSL https://api.github.com/repos/JetBrains/kotlin/releases/latest | grep -oE '"tag_name": "[^"]*"')"
  echo "$tag"; version="$(echo "$tag" | grep -oE 'v[0-9.]+' | tr -d v)"
  curl -fsSL "https://github.com/JetBrains/kotlin/releases/download/v$version/kotlin-compiler-$version.zip" -o /tmp/kotlin.zip && sha256sum /tmp/kotlin.zip && stat -c %s /tmp/kotlin.zip && unzip -l /tmp/kotlin.zip | head -n 6
  echo "== debugpy"
  curl -fsSL https://pypi.org/pypi/debugpy/json | python3 -c "
import json,sys
d=json.load(sys.stdin); print('version', d['info']['version'])
for f in d['urls']:
    if 'none-any' in f['filename']: print(f['filename'], f['size'], f['digests']['sha256'], f['url'])"
  echo "== npm"
  for p in typescript-language-server typescript pyright; do echo "$p $(curl -fsSL https://registry.npmjs.org/$p/latest | python3 -c 'import json,sys; print(json.load(sys.stdin)["version"])')"; done
} > "$out" 2>&1
cat "$out"
