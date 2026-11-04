#!/usr/bin/env bash
# Publishes the contents of ci-out/ to the `ci-logs` branch, so the latest build logs, test results and
# screenshots can be read without access to the Actions UI. One folder per job (and per branch for branches
# other than main); the newest run wins.
#
# Usage: build/ci/publish-logs.sh <job-name> <job-status>
set -euo pipefail

# A watchdog over the whole publish: with every Git call under a limit, publishing still held the SSH job for 17
# minutes, past its 20, after its tests had passed. The publish runs in its own process group, which is killed with
# everything it started once the deadline and a minute have passed; the logs are an aid, never the job's verdict.
if [ -z "${TABSTUDIO_PUBLISH_INNER:-}" ]; then
  total=$(( ${TABSTUDIO_PUBLISH_DEADLINE:-240} + ${TABSTUDIO_PUBLISH_GRACE:-60} ))
  TABSTUDIO_PUBLISH_INNER=1 setsid bash "$0" "$@" < /dev/null &
  pid=$!
  for _ in $(seq 1 "$total"); do
    if ! kill -0 "$pid" 2> /dev/null; then
      wait "$pid" || true
      exit 0
    fi
    sleep 1
  done
  kill -KILL -- "-$pid" 2> /dev/null || kill -KILL "$pid" 2> /dev/null || true
  echo "::warning::Publishing the logs of ${1:-the job} to ci-logs took too long and was stopped; they are in the job's artifact." >&2
  exit 0
fi

# The fetch below leaves out files' contents; nothing may fetch them later behind the limits' back.
export GIT_NO_LAZY_FETCH=1

job="${1:?job name}"
status="${2:?job status}"
remote="https://x-access-token:${GITHUB_TOKEN:?}@github.com/${GITHUB_REPOSITORY:?}.git"
workdir="$(mktemp -d)"

git -C "$workdir" init -q
git -C "$workdir" config user.name "tabstudio-ci"
git -C "$workdir" config user.email "ci@users.noreply.github.com"

# main publishes to <job>/; any other branch to branches/<branch>/<job>/, with '/' in the name replaced by '-'.
branch="${GITHUB_REF_NAME:-main}"
if [ "$branch" = "main" ]; then
  folder="$job"
else
  folder="branches/${branch//\//-}/$job"
fi

# Stage this job's folder once, then lay it over the newest ci-logs commit on every attempt. Rebuilding from the tip,
# instead of rebasing an older commit, means a race with another job's push can never put that job's folder back.
staging="$(mktemp -d)"
cp -r ci-out/. "$staging/" 2>/dev/null || true
echo "job=${job} branch=${branch} status=${status} sha=${GITHUB_SHA} run=${GITHUB_RUN_ID} time=$(date -u +%FT%TZ)" > "$staging/STATUS"

# The branch's tree holds every job's logs and screenshots, gigabytes of them: checking it out filled the runner's
# disk ("Disk quota exceeded") and failed a job whose tests had finished. Fetch its trees without their files, lay
# this job's folder over them in the index, and commit that, so only this job's files are ever on disk.
remote_url="${TABSTUDIO_PUBLISH_REMOTE:-$remote}"

# Every job of every branch publishes here, and a fetch or push can stall on the busy branch: one did until the SSH job's
# 20 minutes ran out, failing a job whose tests had passed. Each Git call gets a limit, and publishing gives up after
# four minutes with a warning (ten still ran the SSH job past its 20); the logs are an aid, never the job's verdict.
limit="${TABSTUDIO_PUBLISH_GIT_TIMEOUT:-60}"
deadline=$(( $(date +%s) + ${TABSTUDIO_PUBLISH_DEADLINE:-240} ))
for attempt in $(seq 1 12); do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    break
  fi
  rm -f "$workdir/.git/index"
  parent=""
  if timeout -k 5 "$limit" git -C "$workdir" fetch -q --depth 1 --filter=blob:none "$remote_url" ci-logs 2>/dev/null; then
    parent="$(git -C "$workdir" rev-parse FETCH_HEAD)"
    git -C "$workdir" read-tree "$parent"
  fi
  rm -rf "${workdir:?}/${folder}"
  mkdir -p "$workdir/$folder"
  cp -r "$staging/." "$workdir/$folder/"
  # Only this folder: entries elsewhere stay as the tip has them, and files gone from it are removed from it.
  git -C "$workdir" add -A -- "$folder"
  tree="$(git -C "$workdir" write-tree)"
  if [ -n "$parent" ] && [ "$tree" = "$(git -C "$workdir" rev-parse "$parent^{tree}")" ]; then
    exit 0
  fi
  commit="$(git -C "$workdir" commit-tree "$tree" ${parent:+-p "$parent"} -m "${folder}: ${status} @ ${GITHUB_SHA::8}")"
  if timeout -k 5 "$limit" git -C "$workdir" push -q "$remote_url" "$commit:refs/heads/ci-logs"; then
    exit 0
  fi
  # Jobs on every branch publish here; back off with jitter so they stop colliding.
  sleep $((attempt * 2 + RANDOM % 5))
done
echo "::warning::Could not publish the logs of ${job} to ci-logs; they are in the job's artifact." >&2
