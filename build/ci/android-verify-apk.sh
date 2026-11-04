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
    | grep -E "^(package|sdkVersion|minSdkVersion|targetSdkVersion|application-label:|launchable-activity|uses-permission|native-code)" || true
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

# The IDE's activity handles a change of display size (DeX's, or Android's Display size: density) and of Bold text
# (fontWeightAdjustment) itself; without them in configChanges, Android recreated it and lost the IDE's window.
# The badging is read whole first: piped into grep -q, which stops at the first match, aapt2 could be killed by
# SIGPIPE while still writing, and pipefail made a good APK fail.
badging="$("$build_tools/aapt2" dump badging "$apk" 2>/dev/null || true)"
if ! grep -q "^launchable-activity: name='com.tabstudio.ide.MainActivity'" <<<"$badging"; then
  echo "The APK has no launcher activity com.tabstudio.ide.MainActivity: Android cannot start TabStudio." >&2
  exit 1
fi
xmltree="$(mktemp)"
"$build_tools/aapt2" dump xmltree --file AndroidManifest.xml "$apk" > "$xmltree"
python3 - "$xmltree" <<'PY'
import re, sys
tree = open(sys.argv[1], encoding="utf-8").read().splitlines()
required = {"density": 0x1000, "fontWeightAdjustment": 0x10000000, "fontScale": 0x40000000, "screenSize": 0x400}
for i, line in enumerate(tree):
    if "E: activity" not in line:
        continue
    indent = len(line) - len(line.lstrip())
    attributes = []
    for following in tree[i + 1:]:
        if len(following) - len(following.lstrip()) <= indent:
            break
        attributes.append(following)
    if not any('"com.tabstudio.ide.MainActivity"' in a for a in attributes):
        continue
    changes = [a for a in attributes if "android:configChanges" in a]
    value = int(re.search(r"0x([0-9a-fA-F]+)\s*$", changes[0]).group(1), 16) if changes else 0
    missing = [name for name, bit in required.items() if not value & bit]
    if missing:
        sys.exit(f"MainActivity's configChanges (0x{value:x}) lack {', '.join(missing)}: changing them recreates the IDE.")
    print(f"MainActivity handles its configuration changes itself (configChanges 0x{value:x}).")
    break
else:
    sys.exit("The manifest has no com.tabstudio.ide.MainActivity.")
PY

# apksigner names the signer by scheme ("Signer #1" for v1/v2, "V3.0 Signer" for v3); take the first certificate.
actual="$({ "$build_tools/apksigner" verify --print-certs "$apk" || true; } | sed -n 's/^.*certificate SHA-256 digest: //p' | head -n 1 || true)"
if [ -z "$expected" ] || [ "$actual" != "$expected" ]; then
  echo "The APK is not signed with the development key: expected '${expected}', got '${actual}'." >&2
  exit 1
fi
echo "Signed with the TabStudio development key."
