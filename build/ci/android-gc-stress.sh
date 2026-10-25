#!/usr/bin/env bash
# Runs TabStudio's emulator self test (build/ci/android-smoke-test.sh) several times in a row on one running
# emulator or device, each time from a fresh install of the app's data, and sorts every run into passed, failed or
# crashed. Meant for the GC stress workflow (.github/workflows/gc-stress.yml), whose builds may check their own heap,
# so a corrupted heap shows as a crash with Mono's message naming the failed check. Fails when any run crashed;
# a self test that failed without crashing is reported as a warning, since the emulator job in ci.yml checks that.
#
# For each run, <output-folder>/run-<n>/ keeps what android-smoke-test.sh leaves (verdict, screenshots, filtered
# log) plus gc.txt, the collector's own lines (one per collection, and any check that failed); a run that did not
# pass also keeps logcat-full.txt and, when the app aborted, crash.txt with Mono's message and the native
# backtrace, and the full tombstone (every thread's backtrace, the memory map and the memory around the registers)
# when the emulator lets adb run as root. summary.txt has one line per run.
#
# Usage: build/ci/android-gc-stress.sh <apk> <output-folder> <runs> [timeout-seconds-per-run]
set -uo pipefail

apk="${1:?apk}"
out="${2:?output folder}"
runs="${3:?number of runs}"
timeout="${4:-900}"
package="com.tabstudio.ide"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

mkdir -p "$out"
: > "$out/summary.txt"

# The collector's own log, one line per collection (GC_MINOR, GC_MAJOR_CONCURRENT_START, ...), under the Mono tag,
# to see what it was doing when a check failed. The app's runtime reads this property at startup, in release
# builds too; it overrides MONO_LOG_LEVEL and MONO_LOG_MASK from the environment.
adb shell setprop debug.mono.log "gc,mono_log_level=debug,mono_log_mask=gc"
# Root, where the system image allows it (userdebug emulator images do), to read the full tombstones of a crash.
adb root > /dev/null 2>&1 && adb wait-for-device
adb shell rm -f '/data/tombstones/*' > /dev/null 2>&1 || true
passed=0
failed=0
crashed=0

for run in $(seq 1 "$runs"); do
  dir="$out/run-$run"
  mkdir -p "$dir"
  # Every run starts like a first launch: no extracted sample, toolchains or settings from the run before.
  adb shell pm clear "$package" > /dev/null 2>&1 || true

  echo "::group::Run $run of $runs"
  bash "$here/android-smoke-test.sh" "$apk" "$dir" "$timeout"
  status=$?
  echo "::endgroup::"

  # android-smoke-test.sh cleared the log before starting the app, so this is this run's log only.
  adb logcat -d -v threadtime > "$dir/logcat-all.txt" 2> /dev/null || true
  # Mono's GC trace (tag Mono) and SGen's checks (tag mono-gc), plus the runtime's fatal messages.
  grep -E ' (Mono|mono-gc|monodroid) *:|SGen|sgen' "$dir/logcat-all.txt" > "$dir/gc.txt" || true

  verdict="$(head -n 1 "$dir/verdict.txt" 2> /dev/null || echo "no verdict")"
  # The app's own aborts only: a toolchain's program crashing in its own process is not the IDE crashing.
  abort="$(grep -m 1 -E 'F monodroid: |Fatal signal .*tabstudio|\* Assertion at' "$dir/logcat-all.txt" | sed 's/^.*: //' || true)"
  if [ -n "$abort" ]; then
    crashed=$((crashed + 1))
    outcome="CRASHED: $abort"
    # The abort message, what led up to it on the crashing thread, and the tombstone's backtrace.
    {
      grep -E 'F monodroid|F libc|\* Assertion at|E  *: ' "$dir/logcat-all.txt"
      echo
      grep -E 'F DEBUG' "$dir/logcat-all.txt" | head -n 120
    } > "$dir/crash.txt"
    for tombstone in $(adb shell ls /data/tombstones 2> /dev/null | tr -d '\r' | grep '^tombstone_[0-9]*$'); do
      adb pull "/data/tombstones/$tombstone" "$dir/$tombstone.txt" > /dev/null 2>&1 || true
    done
  elif [ "$status" -eq 0 ]; then
    passed=$((passed + 1))
    outcome="passed: ${verdict:0:60}"
  else
    failed=$((failed + 1))
    outcome="FAILED: ${verdict:0:300}"
  fi

  if [ "$status" -eq 0 ] && [ -z "$abort" ]; then
    rm -f "$dir/logcat-all.txt"
  else
    mv "$dir/logcat-all.txt" "$dir/logcat-full.txt"
  fi
  rm -f "$dir"/*.apk
  adb shell rm -f '/data/tombstones/*' > /dev/null 2>&1 || true
  echo "run $run: $outcome" | tee -a "$out/summary.txt"
done

echo "Passed $passed, failed $failed, crashed $crashed of $runs runs." | tee -a "$out/summary.txt"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### ${GC_STRESS_TITLE:-$(basename "$out")}"
    echo '```'
    cat "$out/summary.txt"
    echo '```'
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "$failed" -gt 0 ]; then
  echo "::warning::$failed of $runs self tests failed without crashing; see summary.txt."
fi
[ "$crashed" -eq 0 ]
