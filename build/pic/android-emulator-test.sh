#!/usr/bin/env bash
# emulator-runner starts a separate shell for each script line. Keep both
# checks and their combined exit status in this single process.
set -euo pipefail

apk=${1:?APK path is required}
output=${2:?Output directory is required}
work=${3:?PIC working directory is required}

adb install -r "$apk"
status=0
python3 build/pic/android_spike.py run --work "$work" --output "$output/pic-android" || status=1
bash build/ci/android-smoke-test.sh "$apk" "$output" 960 || status=1
exit "$status"
