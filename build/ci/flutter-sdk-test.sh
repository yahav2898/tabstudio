#!/usr/bin/env bash
# Explicit/manual SDK spike on a credential-free glibc Linux x64/ARM64 runner.
set -euo pipefail
sdk="${1:?SDK directory}"
mkdir -p ci-out/flutter
case "$(uname -m)" in
  aarch64) arch=arm64; digest=41b209d2dfb4dde1a33214dc5ac25521272cab43bd40d20467c113c82b6894dc ;;
  x86_64) arch=x64; digest=6dc4f5c13611de54eced525280c78d25999b41b28a9d45922500b80f8dcda2f0 ;;
  *) echo 'Flutter SDK spike requires ARM64 or x64.' >&2; exit 1 ;;
esac
revision=a0e9b9dbf78c8a5ef39b45a7efd40ed2de19c1a7
engine=cb467e31a54207dc987aca5bb0941c4d2e3fd9ed
git clone --depth 1 --single-branch --branch 3.38.0 https://github.com/flutter/flutter.git "$sdk"
test "$(git -C "$sdk" rev-parse HEAD)" = "$revision"
curl -fL --retry 3 "https://storage.googleapis.com/flutter_infra_release/flutter/$engine/dart-sdk-linux-$arch.zip" -o "$sdk/dart.zip"
echo "$digest  $sdk/dart.zip" | sha256sum -c -
mkdir -p "$sdk/bin/cache"
unzip -q "$sdk/dart.zip" -d "$sdk/bin/cache"
echo "$engine" > "$sdk/bin/cache/engine-dart-sdk.stamp"
rm "$sdk/dart.zip"
export PATH="$sdk/bin:$PATH"
export DART_SUPPRESS_ANALYTICS=true
export PUB_CACHE="${RUNNER_TEMP:-/tmp}/flutter-pub-cache"
{
  time flutter --disable-analytics
  flutter --version
  dart --version
  time flutter precache --web
  time flutter create --platforms=web "$sdk/spike_app"
  cd "$sdk/spike_app"
  time flutter pub get
  du -sh "$sdk" "$PUB_CACHE"
} > ci-out/flutter/sdk.log 2>&1
echo "$sdk/bin" >> "${GITHUB_PATH:?Run this script inside the manual GitHub workflow}"
