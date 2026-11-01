#!/usr/bin/env bash
# emulator-runner starts a separate shell for each script line. Keep both
# checks and their combined exit status in this single process.
set -euo pipefail

apk=${1:?APK path is required}
output=${2:?Output directory is required}
work=${3:?PIC working directory is required}
probe_apk=${4:?Disposable PIC probe APK path is required}
shard=${5:-all}
case "$shard" in
  all|managed|ui|native|compiled|containers) ;;
  *) echo "Unknown self-test shard: $shard" >&2; exit 2 ;;
esac

mkdir -p "$output"
# The job's own log cannot be read once published; keep this script's output with the files that are.
exec > >(tee -a "$output/emulator-test.log") 2>&1

status=0
# Run the existing timed IDE check on the same clean device as main. Afterwards
# stop its instrumentation/app before the independent compiler's memory test.
bash build/ci/android-smoke-test.sh "$apk" "$output" 900 "$shard" || status=1
# Every IDE shard must pass; the separate app-UID compiler assertion belongs to
# the native shard (and remains part of an unsharded manual run).
if [[ "$shard" != all && "$shard" != native ]]; then
    exit "$status"
fi
adb shell am force-stop com.tabstudio.ide || true
adb install -r "$probe_apk"
python3 build/pic/android_spike.py run --work "$work" --output "$output/pic-android" || status=1
exit "$status"

