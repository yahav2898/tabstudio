#!/usr/bin/env bash
# Installs TabStudio on a running emulator or device and starts it in self-test mode: the app opens the
# sample solution, presses F5, answers the program's question and logs the outcome under the TabStudio tag
# (a TABSTUDIO_SELFTEST_OK or TABSTUDIO_SELFTEST_FAIL line). Leaves a screenshot, the relevant logcat lines
# and the verdict in the output folder, and fails unless the self test passed.
#
# Desktop mode is tested too, on a simulated second display (Android's overlay display, a presentation display
# like a monitor on USB-C or HDMI): the app moves the IDE there, uses the tablet's Toolbox and Properties panel,
# and asks this script in its log (TABSTUDIO_DESKTOP_STEP lines) to type on the keyboard, click the display and
# take screenshots, which it does with adb.
#
# Usage: build/ci/android-smoke-test.sh <apk> <output-folder> [timeout-seconds]
set -euo pipefail

apk="${1:?apk}"
out="${2:?output folder}"
timeout="${3:-300}"
package="com.tabstudio.ide"
activity="com.tabstudio.ide.MainActivity"

mkdir -p "$out"
adb wait-for-device
echo "Device: Android $(adb shell getprop ro.build.version.release | tr -d '\r')" \
  "(API $(adb shell getprop ro.build.version.sdk | tr -d '\r')), $(adb shell getprop ro.product.cpu.abi | tr -d '\r')," \
  "screen $(adb shell wm size | tr -d '\r' | sed 's/.*: //')"

# The second display: 1920 × 1080 at 240 dpi.
adb shell settings put global overlay_display_devices 1920x1080/240
for _ in $(seq 1 20); do
  adb shell dumpsys display | grep -q 'Overlay #1' && break
  sleep 1
done
adb shell dumpsys display | tr -d '\r' | grep -E 'mDisplayId=|FLAG_PRESENTATION|Overlay #1' | sort -u > "$out/displays.txt" || true
echo "Displays:"
cat "$out/displays.txt"

# Does what a TABSTUDIO_DESKTOP_STEP line asks: type, click the second display, or take screenshots.
desktop_step() {
  local action argument1 argument2 argument3
  read -r _ action argument1 argument2 argument3 <<< "$1"
  case "$action" in
    type)
      echo "Typing on the tablet's keyboard: '${argument1//_/ }'"
      adb shell input text "${argument1//_/%s}" || echo "Typing failed"
      ;;
    click)
      echo "Clicking display $argument1 with a mouse at $argument2,$argument3"
      adb shell input mouse -d "$argument1" tap "$argument2" "$argument3" || echo "Clicking failed"
      ;;
    screenshot)
      echo "Taking screenshots of the device"
      adb exec-out screencap -p > "$out/desktop-device.png" || true
      ;;
    *)
      echo "Unknown desktop step: $1"
      ;;
  esac
}

adb install -r "$apk"
adb logcat -c
started=$(date +%s)
adb shell am start -W -n "$package/$activity" --ez tabstudio.selftest true --ez tabstudio.selftest.desktop true

verdict=""
handled=0
while [ -z "$verdict" ] && [ $(($(date +%s) - started)) -lt "$timeout" ]; do
  sleep 3
  log="$(adb logcat -d -s TabStudio | tr -d '\r')"
  steps="$(printf '%s\n' "$log" | grep -oE 'TABSTUDIO_DESKTOP_STEP .*' || true)"
  count="$(printf '%s' "$steps" | grep -c . || true)"
  while [ "$handled" -lt "$count" ]; do
    handled=$((handled + 1))
    desktop_step "$(printf '%s\n' "$steps" | sed -n "${handled}p")"
  done

  verdict="$(printf '%s\n' "$log" | grep -m 1 -oE 'TABSTUDIO_SELFTEST_(OK|FAIL).*' || true)"
  if [ -z "$verdict" ] && [ -z "$(adb shell pidof "$package" | tr -d '\r' || true)" ]; then
    verdict="TABSTUDIO_SELFTEST_FAIL TabStudio is not running: it crashed or was killed before the self test finished."
  fi
done
elapsed=$(($(date +%s) - started))

# Let the page draw its final state before the screenshot.
sleep 3
adb exec-out screencap -p > "$out/screen.png"

# The app's own pictures of both displays while desktop mode was on.
adb pull "/sdcard/Android/data/$package/files/selftest/." "$out/" > /dev/null 2>&1 || true
adb shell settings delete global overlay_display_devices > /dev/null 2>&1 || true

adb logcat -d -v threadtime > "$out/logcat-full.txt"
grep -E 'TabStudio|mono|monodroid|AndroidRuntime|DOTNET|FATAL' "$out/logcat-full.txt" > "$out/logcat.txt" || true

verdict="${verdict:-TABSTUDIO_SELFTEST_TIMEOUT no verdict after ${timeout} s}"
echo "$verdict" | tee "$out/verdict.txt"
echo "Seconds from launch to verdict: $elapsed" | tee -a "$out/verdict.txt"

case "$verdict" in
  TABSTUDIO_SELFTEST_OK*)
    # The filtered log is enough when everything worked; keep the full one only to debug a failure.
    rm -f "$out/logcat-full.txt"
    ;;
  *)
    exit 1
    ;;
esac
