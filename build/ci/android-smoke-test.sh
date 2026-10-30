#!/usr/bin/env bash
# Installs TabStudio on a running emulator or device and starts it in self-test mode: the app opens the
# sample solution, presses F5, answers the program's question and logs the outcome under the TabStudio tag
# (a TABSTUDIO_SELFTEST_OK or TABSTUDIO_SELFTEST_FAIL line). Leaves a screenshot, the relevant logcat lines
# and the verdict in the output folder, and fails unless the self test passed.
#
# Desktop mode is tested too, on a simulated second display (Android's overlay display, a presentation display
# like a monitor on USB-C or HDMI): the app moves the IDE there, uses the tablet's Toolbox and Properties panel,
# and asks this script in its log (TABSTUDIO_DESKTOP_STEP lines) to type on the keyboard and take screenshots,
# which it does with adb. (The app clicks the display itself: adb's taps have no mouse button events.)
#
# Usage: build/ci/android-smoke-test.sh <apk> <output-folder> [timeout-seconds] [all|managed|ui|native|performance|flutter]
#
# The opt-in flutter mode runs only the experimental Flutter web self test (tabstudio.selftest.flutter).
set -euo pipefail

apk="${1:?apk}"
out="${2:?output folder}"
timeout="${3:-900}"
shard="${4:-all}"
case "$shard" in
  all|managed|ui|native|performance|flutter) ;;
  *) echo "Unknown self-test shard: $shard" >&2; exit 2 ;;
esac
package="com.tabstudio.ide"
activity="com.tabstudio.ide.MainActivity"

mkdir -p "$out"
adb wait-for-device
# boot_completed can come before system_server has published its services, or while it restarts on a slow runner:
# adb install then fails with "Can't find service: package" and am exits 20, with nothing logged to say why.
for _ in $(seq 1 180); do
  services="$(adb shell 'getprop sys.boot_completed; service check package; service check activity' 2> /dev/null | tr -d '\r' || true)"
  [ "$(printf '%s\n' "$services" | grep -cE '^1$|: found$' || true)" -ge 3 ] && break
  sleep 1
done
# Headless CI asks Android not to show error dialogs so a background system app's ANR cannot consume the injected
# input. Chromium's emulator setup uses the same setting; manual device runs leave the device policy alone. Some
# images ignore it (the Pixel Launcher's "isn't responding" dialog covered the app on the overlay display), so
# system_dialogs below also answers the dialogs that are shown anyway.
if [ "${GITHUB_ACTIONS:-false}" = true ]; then
  adb shell settings put global hide_error_dialogs 1
fi
echo "Device: Android $(adb shell getprop ro.build.version.release | tr -d '\r')" \
  "(API $(adb shell getprop ro.build.version.sdk | tr -d '\r')), $(adb shell getprop ro.product.cpu.abi | tr -d '\r')," \
  "screen $(adb shell wm size | tr -d '\r' | sed 's/.*: //')"

if [ "$shard" = performance ]; then
  # The paired latency run measures the editor on one display, without desktop-mode notifications.
  adb shell settings delete global overlay_display_devices > /dev/null 2>&1 || true
else
  # The second display: 1920 × 1080 at 240 dpi.
  adb shell settings put global overlay_display_devices 1920x1080/240
  for _ in $(seq 1 20); do
    [ "$(adb shell dumpsys display | grep -c 'mDisplayId=' || true)" -ge 2 ] && break
    sleep 1
  done
  # Each logical display's name and id, and its flags: the overlay display is a presentation display. Android 10
  # writes DisplayInfo{"Overlay #1, displayId 1", ...}, later versions DisplayInfo{"Overlay #1", displayId 2, ...}.
  adb shell dumpsys display | tr -d '\r' \
    | awk 'match($0, /DisplayInfo\{"[^"]*"(, displayId [0-9]+)?/) {
        name = substr($0, RSTART + 12, RLENGTH - 12)
        gsub(/"/, "", name)
        print name (index($0, "FLAG_PRESENTATION") ? ": a presentation display" : "")
      }' | sort -u > "$out/displays.txt" || true
  echo "Displays:"
  cat "$out/displays.txt"
fi

# Does what a TABSTUDIO_DESKTOP_STEP line asks: type, or take screenshots.
desktop_step() {
  local action argument
  read -r _ action argument <<< "$1"
  case "$action" in
    type)
      echo "Typing on the tablet's keyboard: '${argument//_/ }'"
      adb shell input text "${argument//_/%s}" || echo "Typing failed"
      ;;
    screenshot)
      echo "Taking screenshots of the device"
      # The device's screen, with the overlay display's window drawn over it.
      adb exec-out screencap -p > "$out/desktop-device.png" || true
      # Every other display SurfaceFlinger composes on its own, such as the emulator's -multidisplay displays.
      for id in $(adb shell dumpsys SurfaceFlinger --display-id 2> /dev/null | tr -d '\r' | sed -n 's/^Display \([0-9]*\).*/\1/p' | tail -n +2); do
        adb exec-out screencap -d "$id" -p > "$out/desktop-device-$id.png" 2> /dev/null || true
        [ -s "$out/desktop-device-$id.png" ] || rm -f "$out/desktop-device-$id.png"
      done
      ;;
    *)
      echo "Unknown desktop step: $1"
      ;;
  esac
}

# Answers Android's "<app> isn't responding" dialogs. Another app's dialog (the launcher's, say) gets "Wait", as a
# person would tap, so it stops covering TabStudio and swallowing the self test's input; each one is logged in
# system-dialogs.txt with a screenshot. TabStudio's own dialog is never dismissed: it is a real failure, reported
# as the verdict instead of a timeout (dialog_verdict).
anr_count=0
dialog_verdict=""
system_dialogs() {
  local windows app xml node bounds x1 y1 x2 y2
  windows="$(adb shell dumpsys window windows 2> /dev/null | tr -d '\r' | grep -oE 'Application Not Responding: [^ }]+' | sort -u || true)"
  [ -n "$windows" ] || return 0
  while read -r _ _ _ app; do
    [ -n "$app" ] || continue
    anr_count=$((anr_count + 1))
    adb exec-out screencap -p > "$out/system-dialog-$anr_count.png" 2> /dev/null || true
    if [ "${app%%:*}" = "$package" ]; then
      dialog_verdict="TABSTUDIO_SELFTEST_FAIL TabStudio is not responding: Android showed its ANR dialog (system-dialog-$anr_count.png)."
      return 0
    fi
    xml="$(adb shell 'uiautomator dump /sdcard/tabstudio-dialog.xml > /dev/null 2>&1 && cat /sdcard/tabstudio-dialog.xml' 2> /dev/null | tr -d '\r' || true)"
    node="$(printf '%s' "$xml" | grep -oE '<node [^>]*resource-id="android:id/aerr_wait"[^>]*>' | head -n 1 || true)"
    [ -n "$node" ] || node="$(printf '%s' "$xml" | grep -oE '<node [^>]*text="Wait"[^>]*>' | head -n 1 || true)"
    bounds="$(printf '%s' "$node" | grep -oE 'bounds="\[[0-9]+,[0-9]+\]\[[0-9]+,[0-9]+\]"' | grep -oE '[0-9]+' | tr '\n' ' ' || true)"
    if read -r x1 y1 x2 y2 <<< "$bounds" && [ -n "${y2:-}" ]; then
      adb shell input tap $(((x1 + x2) / 2)) $(((y1 + y2) / 2)) || true
      echo "$(date -u +%T) $app isn't responding: tapped Wait (system-dialog-$anr_count.png)" | tee -a "$out/system-dialogs.txt"
    else
      # No Wait button to tap: stop the unresponsive app, which Android restarts if it is the launcher.
      adb shell am force-stop "${app%%:*}" || true
      echo "$(date -u +%T) $app isn't responding: no Wait button found, stopped it (system-dialog-$anr_count.png)" | tee -a "$out/system-dialogs.txt"
    fi
  done <<< "$windows"
}

# One retry: an install that races the package manager's own start-up fails without anything being wrong.
adb install -r "$apk" || { sleep 10; adb install -r "$apk"; }
adb logcat -c
started=$(date +%s)
if [ "$shard" = flutter ]; then
  # The Flutter web self test needs no injected input or desktop mode.
  adb shell am start -W -n "$package/$activity" --ez tabstudio.selftest true --ez tabstudio.selftest.flutter true
else
  # The shell's instrumentation source permits the existing desktop checks to inject real Android input events.
  # Keep instrumentation active until the emulator is stopped, so the IDE is still present in the screenshots.
  adb shell am instrument -e tabstudio.selftest.shard "$shard" "$package/com.tabstudio.ide.SelfTestInstrumentation"
fi

verdict=""
handled=0
polls=0
while [ -z "$verdict" ] && [ $(($(date +%s) - started)) -lt "$timeout" ]; do
  # Desktop input/screenshot requests are handshakes with the app. Respond
  # promptly so polling latency does not consume its launch-to-verdict budget.
  sleep 1
  log="$(adb logcat -d -s TabStudio | tr -d '\r')"
  steps="$(printf '%s\n' "$log" | grep -oE 'TABSTUDIO_DESKTOP_STEP .*' || true)"
  count="$(printf '%s' "$steps" | grep -c . || true)"
  while [ "$handled" -lt "$count" ]; do
    handled=$((handled + 1))
    desktop_step "$(printf '%s\n' "$steps" | sed -n "${handled}p")"
  done

  verdict="$(printf '%s\n' "$log" | grep -m 1 -oE 'TABSTUDIO_SELFTEST_(OK|FAIL).*' || true)"
  # Every few seconds, answer any "isn't responding" dialog before it swallows the next injected input.
  if [ -z "$verdict" ] && [ $((polls % 5)) -eq 0 ]; then
    system_dialogs
    verdict="$dialog_verdict"
  fi
  polls=$((polls + 1))
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
echo "Shard: $shard" | tee -a "$out/verdict.txt"
echo "Seconds from launch to verdict: $elapsed" | tee -a "$out/verdict.txt"

case "$verdict" in
  TABSTUDIO_SELFTEST_OK*)
    if [ "$elapsed" -ge "$timeout" ]; then
      echo "Self test exceeded its ${timeout}-second launch-to-verdict limit." | tee -a "$out/verdict.txt"
      exit 1
    fi
    # The filtered log is enough when everything worked; keep the full one only to debug a failure.
    rm -f "$out/logcat-full.txt"
    ;;
  *)
    exit 1
    ;;
esac
