#!/usr/bin/env bash
# Publishes the contents of ci-out/ to the `ci-logs` branch, so the latest build logs, test results and
# screenshots can be read without access to the Actions UI. One folder per job; the newest run wins.
#
# Usage: build/ci/publish-logs.sh <job-name> <job-status>
set -euo pipefail

job="${1:?job name}"
status="${2:?job status}"
remote="https://x-access-token:${GITHUB_TOKEN:?}@github.com/${GITHUB_REPOSITORY:?}.git"
workdir="$(mktemp -d)"

git -C "$workdir" init -q
git -C "$workdir" config user.name "tabstudio-ci"
git -C "$workdir" config user.email "ci@users.noreply.github.com"

# Keep other jobs' folders: start from the current branch if it exists.
if git -C "$workdir" fetch -q --depth 1 "$remote" ci-logs 2>/dev/null; then
  git -C "$workdir" checkout -q -b ci-logs FETCH_HEAD
else
  git -C "$workdir" checkout -q --orphan ci-logs
fi

rm -rf "${workdir:?}/${job}"
mkdir -p "$workdir/$job"
cp -r ci-out/. "$workdir/$job/" 2>/dev/null || true
echo "job=${job} status=${status} sha=${GITHUB_SHA} run=${GITHUB_RUN_ID} time=$(date -u +%FT%TZ)" > "$workdir/$job/STATUS"

git -C "$workdir" add -A
git -C "$workdir" commit -qm "${job}: ${status} @ ${GITHUB_SHA::8}" || exit 0

# Jobs run in parallel; retry on a race with another job's push.
for attempt in 1 2 3 4 5; do
  if git -C "$workdir" push -q "$remote" HEAD:ci-logs; then
    exit 0
  fi
  sleep $((attempt * 3))
  git -C "$workdir" fetch -q --depth 1 "$remote" ci-logs
  git -C "$workdir" rebase -q FETCH_HEAD || { git -C "$workdir" rebase --abort; git -C "$workdir" reset -q --soft FETCH_HEAD; git -C "$workdir" commit -qm "${job}: ${status}"; }
done
echo "Could not publish logs after 5 attempts" >&2
