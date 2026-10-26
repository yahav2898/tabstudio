#!/usr/bin/env bash
# emulator-runner starts a separate shell for each script line. Keep both
# checks and their combined exit status in this single process.
set -euo pipefail

apk=${1:?APK path is required}
output=${2:?Output directory is required}
work=${3:?PIC working directory is required}
probe_apk=${4:?Disposable PIC probe APK path is required}

status=0
# Run the existing timed IDE check on the same clean device as main. Afterwards
# stop its instrumentation/app before the independent compiler's memory test.
bash build/ci/android-smoke-test.sh "$apk" "$output" 900 || status=1
adb shell am force-stop com.tabstudio.ide
adb install -r "$probe_apk"
python3 build/pic/android_spike.py run --work "$work" --output "$output/pic-android" || status=1
exit "$status"
