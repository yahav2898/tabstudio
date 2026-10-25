#!/usr/bin/env bash
# Spike: installs TabStudio on the emulator, pushes the spike's apps and script to the app's external files folder,
# where the self test finds them and runs the Windows Compatibility spike instead of its usual steps.
# Usage: emulator.sh <apk> <output-folder> <timeout-seconds>
set -euo pipefail
apk="$1"; out="$2"; timeout="$3"
adb wait-for-device
adb install -r "$apk"
dir=/sdcard/Android/data/com.tabstudio.ide/files/spike
adb shell mkdir -p "$dir/apps"
adb push spike-apps/. "$dir/apps/" > /dev/null
adb push build/windows-compat/spike/spike.py "$dir/"
adb shell ls -R "$dir" | head -n 20
exec bash build/ci/android-smoke-test.sh "$apk" "$out" "$timeout"
