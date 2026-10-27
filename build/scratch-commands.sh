#!/usr/bin/env bash
# Scratch build for agent/live: compile what changed with warnings as errors, then run the tests named in
# build/scratch-tests.txt (one "project|filter" per line). Lives only on scratch/** branches.
set -uo pipefail
status=0
mkdir -p ci-out
while IFS= read -r project; do
  [ -z "$project" ] && continue
  name=$(basename "$project")
  if ! dotnet build "$project" -c Release -p:TreatWarningsAsErrors=true > "ci-out/build-$name.log" 2>&1; then
    status=1
  fi
  echo "== build $project"
  grep -E ": (error|warning) " "ci-out/build-$name.log" | sed -E 's#/home/runner/work/tabstudio/tabstudio/##g; s# \[[^]]*\]$##' | sort -u | head -n 150
done < build/scratch-builds.txt
if [ $status -ne 0 ]; then
  echo "BUILD FAILED"
  exit 1
fi
while IFS='|' read -r project filter; do
  [ -z "$project" ] && continue
  name=$(basename "$project")
  args=()
  [ -n "${filter:-}" ] && args=(--filter "$filter")
  echo "== test $project ${filter:-}"
  if ! timeout 25m dotnet test "$project" -c Release --no-build "${args[@]}" --blame-hang-timeout 8m --blame-hang-dump-type none --logger "console;verbosity=normal" > "ci-out/test-$name.log" 2>&1; then
    status=1
  fi
  grep -E "^\s+(Failed|Passed|Skipped) |Error Message:|Assert\.|Expected|Actual|at TabStudio|Total tests|Passed!|Failed!|error|Exception" "ci-out/test-$name.log" | grep -v "^\s*Passed " | head -n 200
done < build/scratch-tests.txt
exit $status
