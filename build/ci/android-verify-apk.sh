#!/usr/bin/env bash
# Checks that an APK is signed with the repository's development key, and writes a report of what is in it:
# size, checksum, package and versions, SDK levels, permissions, native ABIs and the signing certificate.
#
# Usage: build/ci/android-verify-apk.sh <apk> <report-file>
set -euo pipefail

apk="${1:?apk}"
report="${2:?report file}"
keystore="build/android/tabstudio-dev.keystore"
build_tools="$(find "${ANDROID_HOME:?}/build-tools" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -n 1)"

# The fingerprint of the development certificate, in apksigner's format: lowercase hex without colons.
expected="$(keytool -list -v -keystore "$keystore" -storepass tabstudio-dev -alias tabstudio 2>/dev/null \
  | sed -n 's/^[[:space:]]*SHA256: //p' | tr -d ':' | tr 'A-F' 'a-f')"

{
  echo "== file"
  echo "size: $(stat -c %s "$apk") bytes ($(du -h "$apk" | cut -f1))"
  echo "sha256: $(sha256sum "$apk" | cut -d' ' -f1)"
  echo
  echo "== manifest"
  "$build_tools/aapt2" dump badging "$apk" 2>/dev/null \
    | grep -E "^(package|sdkVersion|targetSdkVersion|application-label|launchable-activity|uses-permission|native-code)" || true
  echo
  echo "== native libraries per ABI"
  unzip -l "$apk" | awk '{ print $4 }' | grep '^lib/' | cut -d/ -f2 | sort | uniq -c || true
  echo
  echo "== largest entries"
  unzip -lv "$apk" | awk 'NR > 3 && $1 ~ /^[0-9]+$/ { print $3, $1, $8 }' | sort -rn | head -n 15 \
    | awk '{ printf "%10d compressed %10d uncompressed  %s\n", $1, $2, $3 }' || true
  echo
  echo "== signature"
  "$build_tools/apksigner" verify --verbose --print-certs "$apk"
  echo "expected development certificate SHA-256: $expected"
} > "$report" 2>&1
cat "$report"

actual="$("$build_tools/apksigner" verify --print-certs "$apk" | sed -n 's/^Signer #1 certificate SHA-256 digest: //p')"
if [ -z "$expected" ] || [ "$actual" != "$expected" ]; then
  echo "The APK is not signed with the development key: expected '${expected}', got '${actual}'." >&2
  exit 1
fi
echo "Signed with the TabStudio development key."
