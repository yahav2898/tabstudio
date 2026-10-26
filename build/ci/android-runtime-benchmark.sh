#!/usr/bin/env bash
# Runs both APKs on this one emulator, with fresh app data, and keeps an explicit status for each variant.
set -euo pipefail
baseline_apk="${1:?baseline APK}"
jit_apk="${2:?production JIT APK}"
out="${3:?output folder}"
package="com.tabstudio.ide"

mkdir -p "$out/baseline" "$out/jit"
for variant in baseline jit; do
  apk="$baseline_apk"
  [ "$variant" = baseline ] || apk="$jit_apk"
  adb shell am force-stop "$package" >/dev/null 2>&1 || true
  # Install first so pm clear also succeeds on the very first variant. Preserve setup failures too.
  status=0
  adb install -r "$apk" > "$out/$variant/install.txt" 2>&1 || status=$?
  if [ "$status" -eq 0 ]; then
    adb shell pm clear "$package" > "$out/$variant/clear.txt" 2>&1 || status=$?
  fi
  if [ "$status" -eq 0 ]; then
    bash build/ci/android-smoke-test.sh "$apk" "$out/$variant" 360 performance || status=$?
  else
    echo "Benchmark setup failed before launch (exit $status); see install.txt/clear.txt." > "$out/$variant/verdict.txt"
  fi
  echo "$status" > "$out/$variant/exit-code.txt"
  adb shell am force-stop "$package" >/dev/null 2>&1 || true
  if [ "$variant" = baseline ] && [ "$status" -ne 0 ]; then
    echo "::warning::The global-interpreter baseline failed or timed out. Its partial measurements and verdict are retained; this is not a functional pass."
  fi
done

# A deliberately bad baseline must never hide a failed production measurement.
test "$(cat "$out/jit/exit-code.txt")" -eq 0
