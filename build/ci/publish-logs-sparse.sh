#!/usr/bin/env bash
# Publishes the contents of ci-out/ to the `ci-logs` branch like publish-logs.sh, but checks out only the job's own
# folder (a sparse checkout), so it works where the whole branch cannot be checked out, as on Windows runners whose
# paths are too long for the branch's deepest files, and never touches other jobs' folders.
#
# Usage: build/ci/publish-logs-sparse.sh <job-name> <job-status>
set -euo pipefail

job="${1:?job name}"
status="${2:?job status}"
remote="https://x-access-token:${GITHUB_TOKEN:?}@github.com/${GITHUB_REPOSITORY:?}.git"
workdir="$(mktemp -d)"

branch="${GITHUB_REF_NAME:-main}"
if [ "$branch" = "main" ]; then
  folder="$job"
else
  folder="branches/${branch//\//-}/$job"
fi

git config --global core.longpaths true || true
git config --global core.autocrlf false || true
git clone -q --depth 1 --filter=blob:none --no-checkout --branch ci-logs "$remote" "$workdir"
git -C "$workdir" config user.name "tabstudio-ci"
git -C "$workdir" config user.email "ci@users.noreply.github.com"
git -C "$workdir" sparse-checkout set --no-cone "/$folder/"
git -C "$workdir" checkout -q ci-logs

rm -rf "${workdir:?}/${folder}"
mkdir -p "$workdir/$folder"
cp -r ci-out/. "$workdir/$folder/" 2>/dev/null || true
echo "job=${job} branch=${branch} status=${status} sha=${GITHUB_SHA} run=${GITHUB_RUN_ID} time=$(date -u +%FT%TZ)" > "$workdir/$folder/STATUS"

git -C "$workdir" add -A -- "$folder"
git -C "$workdir" commit -qm "${folder}: ${status} @ ${GITHUB_SHA::8}" || exit 0

# Jobs run in parallel; retry on a race with another job's push.
for attempt in 1 2 3 4 5; do
  if git -C "$workdir" push -q "$remote" HEAD:ci-logs; then
    exit 0
  fi
  sleep $((attempt * 3))
  git -C "$workdir" pull -q --rebase --depth 1 "$remote" ci-logs || { git -C "$workdir" rebase --abort || true; }
done
echo "Could not publish logs after 5 attempts" >&2
exit 1
