#!/usr/bin/env bash
# A pinned source revision for the C ABI and server comparison; no fetched executable code runs on Android.
set -euo pipefail
out=${1:?output directory}
abi=${2:-host}
root=$(cd "$(dirname "$0")/../.." && pwd)
revision=4762ad7316dcdec20016ab5985fb46a27902204d
source_dir=${TABSTUDIO_LLAMA_SOURCE:-$root/artifacts/llama.cpp}
if [ ! -f "$source_dir/include/llama.h" ]; then
  mkdir -p "$source_dir"
  archive="$source_dir/source.tar.gz"
  curl -fL --retry 3 "https://codeload.github.com/ggml-org/llama.cpp/tar.gz/$revision" -o "$archive"
  echo "06b6a3a1c2d12becc05d02c701274ab1f6d033399b4a20a166b919289e19233c  $archive" | sha256sum -c -
  tar -xzf "$archive" --strip-components=1 -C "$source_dir"
  rm "$archive"
fi
args=(-DCMAKE_BUILD_TYPE=Release -DLLAMA_SOURCE="$source_dir" -DGGML_VULKAN=OFF)
if [ "$abi" != host ]; then
  ndk=${ANDROID_NDK_HOME:-${ANDROID_NDK_ROOT:-}}
  [ -n "$ndk" ] || ndk=$(find "${ANDROID_HOME:?}/ndk" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)
  args+=(-DCMAKE_TOOLCHAIN_FILE="$ndk/build/cmake/android.toolchain.cmake" -DANDROID_ABI="$abi" -DANDROID_PLATFORM=android-28 -DANDROID_STL=c++_static)
fi
# Vulkan is opt-in: CPU/NEON remains usable on devices without a working Vulkan driver.
[ "${TABSTUDIO_AI_VULKAN:-0}" != 1 ] || args+=(-DGGML_VULKAN=ON)
cmake -S "$root/native/local-ai" -B "$out/build" "${args[@]}"
targets=(tabstudio-ai tabstudio-ai-bench)
[ "${TABSTUDIO_AI_BUILD_SERVER:-1}" != 1 ] || targets+=(llama-server)
cmake --build "$out/build" --target "${targets[@]}" --parallel "${CMAKE_BUILD_PARALLEL_LEVEL:-2}"
mkdir -p "$out/bin"
cp "$out/build/libtabstudio-ai.so" "$out/build/tabstudio-ai-bench" "$out/bin/"
[ "${TABSTUDIO_AI_BUILD_SERVER:-1}" != 1 ] || cp "$out/build/bin/llama-server" "$out/bin/"
