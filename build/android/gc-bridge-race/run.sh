#!/usr/bin/env bash
# Installs and starts the GcBridgeRace reproduction on a running emulator or device, and waits until it crashes or
# the time is up. Writes verdict.txt (CRASHED with Mono's message, or SURVIVED), the app's log and, on a crash,
# the tombstone's backtrace to the output folder.
#
# Usage: run.sh <apk> <output-folder> [seconds]
set -uo pipefail

apk="${1:?apk}"
out="${2:?output folder}"
seconds="${3:-600}"
package="com.example.gcbridgerace"

mkdir -p "$out"
adb wait-for-device
adb shell setprop debug.mono.log "gc,mono_log_level=debug,mono_log_mask=gc"
adb install -r "$apk"
adb logcat -c
adb shell am start -W -n "$package/$package.MainActivity"
started=$(date +%s)
verdict=""
while [ -z "$verdict" ] && [ $(($(date +%s) - started)) -lt "$seconds" ]; do
  sleep 5
  if [ -z "$(adb shell pidof "$package" | tr -d '\r')" ]; then
    verdict="CRASHED after $(($(date +%s) - started)) s"
  fi
done
adb logcat -d -v threadtime > "$out/logcat-full.txt"
grep -E 'GcBridgeRace|F monodroid|Fatal signal|Assertion at|not found in remsets|F DEBUG' "$out/logcat-full.txt" > "$out/logcat.txt" || true
grep -E ' Mono *:' "$out/logcat-full.txt" | grep -E 'CONCURRENT|GC_MAJOR|objects [0-9]{5,} ' > "$out/gc.txt" || true
abort="$(grep -m 1 -E 'F monodroid: |Fatal signal .*gcbridgerace|\* Assertion at' "$out/logcat-full.txt" | sed 's/^.*: //')"
verdict="${verdict:-SURVIVED $seconds s}${abort:+: $abort}"
echo "$verdict" | tee "$out/verdict.txt"
grep -m 1 'not found in remsets' "$out/logcat-full.txt" | tee -a "$out/verdict.txt" || true
tail -n 1 "$out/logcat.txt" | grep ALIVE >> "$out/verdict.txt" || true
grep -c 'objects [0-9][0-9][0-9][0-9][0-9][0-9] ' "$out/logcat-full.txt" | sed 's/^/bridge walks over 100,000 objects: /' | tee -a "$out/verdict.txt"
exit 0
