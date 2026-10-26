#!/usr/bin/env bash
set -euo pipefail
out=${1:?model path}
mkdir -p "$(dirname "$out")"
curl -fL --retry 3 https://huggingface.co/ggml-org/models/resolve/7ae2acaf51ef0706af00804d8fdd6ae9e70829ae/tinyllamas/stories260K.gguf -o "$out.tmp"
echo "047bf46455a544931cff6fef14d7910154c56afbc23ab1c5e56a72e69912c04b  $out.tmp" | sha256sum -c -
mv "$out.tmp" "$out"
