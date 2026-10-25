#!/usr/bin/env bash
# Installs TabStudio on a running emulator or device, starts it and waits for the probe's verdict in logcat
# (a TABSTUDIO_PROBE_OK or TABSTUDIO_PROBE_FAIL line under the TabStudio tag). Leaves a screenshot, the
# relevant logcat lines and the verdict in the output folder, and fails unless the probe passed.
#
# Usage: build/ci/android-smoke-test.sh <apk> <output-folder> [timeout-seconds]
set -euo pipefail

apk="${1:?apk}"
out="${2:?output folder}"
timeout="${3:-150}"
package="com.tabstudio.ide"
activity="com.tabstudio.ide.MainActivity"

mkdir -p "$out"
adb wait-for-device
echo "Device: Android $(adb shell getprop ro.build.version.release | tr -d '\r')" \
  "(API $(adb shell getprop ro.build.version.sdk | tr -d '\r')), $(adb shell getprop ro.product.cpu.abi | tr -d '\r')," \
  "screen $(adb shell wm size | tr -d '\r' | sed 's/.*: //')"

adb install -r "$apk"
adb logcat -c
started=$(date +%s)
adb shell am start -W -n "$package/$activity"

verdict=""
while [ -z "$verdict" ] && [ $(($(date +%s) - started)) -lt "$timeout" ]; do
  sleep 3
  verdict="$(adb logcat -d -s TabStudio | grep -m 1 -oE 'TABSTUDIO_PROBE_(OK|FAIL).*' || true)"
  if [ -z "$verdict" ] && [ -z "$(adb shell pidof "$package" | tr -d '\r' || true)" ]; then
    verdict="TABSTUDIO_PROBE_FAIL TabStudio is not running: it crashed or was killed before the probe finished."
  fi
done
elapsed=$(($(date +%s) - started))

# Let the page draw its final state before the screenshot.
sleep 3
adb exec-out screencap -p > "$out/screen.png"
adb logcat -d -v threadtime > "$out/logcat-full.txt"
grep -E 'TabStudio|mono|monodroid|AndroidRuntime|DOTNET|FATAL' "$out/logcat-full.txt" > "$out/logcat.txt" || true

verdict="${verdict:-TABSTUDIO_PROBE_TIMEOUT no verdict after ${timeout} s}"
echo "$verdict" | tee "$out/verdict.txt"
echo "Seconds from launch to verdict: $elapsed" | tee -a "$out/verdict.txt"

case "$verdict" in
  TABSTUDIO_PROBE_OK*)
    # The filtered log is enough when everything worked; keep the full one only to debug a failure.
    rm -f "$out/logcat-full.txt"
    ;;
  *)
    exit 1
    ;;
esac
