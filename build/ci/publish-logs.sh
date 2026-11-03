#!/usr/bin/env bash
# Publishes the contents of ci-out/ to the `ci-logs` branch, so the latest build logs, test results and
# screenshots can be read without access to the Actions UI. One folder per job (and per branch for branches
# other than main); the newest run wins.
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

# Even without their files, the branch's trees (one folder per job and per branch) had grown to 6 to 43 minutes of
# fetching per job, and on 0.21.3's main run a runner was shut down in the middle of it. So the fast way never
# fetches ci-logs: it pushes only this job's files, as a commit with no parent on a temporary branch, and GitHub's Git
# Data API then puts that folder into ci-logs over its newest commit. The fetch below stays as the fallback.
publish_through_api() {
  command -v gh > /dev/null || return 1
  [ -z "${TABSTUDIO_PUBLISH_REMOTE:-}" ] || return 1
  export GH_TOKEN="${GH_TOKEN:-$GITHUB_TOKEN}"
  local repo="$GITHUB_REPOSITORY" api_dir folder_tree staged temp head base root commit published=""
  api_dir="$(mktemp -d)" || return 1
  rm -f "$workdir/.git/index"
  git -C "$staging" --git-dir="$workdir/.git" --work-tree="$staging" add -A . || return 1
  folder_tree="$(git -C "$workdir" write-tree)" || return 1
  staged="$(git -C "$workdir" commit-tree "$folder_tree" -m "${folder}: staged @ ${GITHUB_SHA::8}")" || return 1
  temp="ci-logs-staging/${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT:-1}-${job}"
  git -C "$workdir" push -q "$remote_url" "+$staged:refs/heads/$temp" || return 1
  for attempt in $(seq 1 12); do
    if head="$(gh api "repos/$repo/git/ref/heads/ci-logs" --jq .object.sha)" &&
       base="$(gh api "repos/$repo/git/commits/$head" --jq .tree.sha)"; then
      printf '{"base_tree":"%s","tree":[{"path":"%s","mode":"040000","type":"tree","sha":"%s"}]}' \
        "$base" "$folder" "$folder_tree" > "$api_dir/root.json"
      if root="$(gh api "repos/$repo/git/trees" --input "$api_dir/root.json" --jq .sha)"; then
        if [ "$root" = "$base" ]; then published=1; break; fi
        printf '{"message":"%s: %s @ %s","tree":"%s","parents":["%s"]}' \
          "$folder" "$status" "${GITHUB_SHA::8}" "$root" "$head" > "$api_dir/commit.json"
        # Not forced: when another job's commit came first, this fails and the next attempt builds on that one.
        if commit="$(gh api "repos/$repo/git/commits" --input "$api_dir/commit.json" --jq .sha)" &&
           gh api -X PATCH "repos/$repo/git/refs/heads/ci-logs" -f sha="$commit" -F force=false > /dev/null; then
          published=1; break
        fi
      fi
    fi
    sleep $((attempt * 2 + RANDOM % 5))
  done
  # ci-logs now holds the folder's objects, so the temporary branch can go.
  git -C "$workdir" push -q "$remote_url" ":refs/heads/$temp" || true
  [ -n "$published" ]
}
if publish_through_api; then
  echo "Published ci-out to ci-logs:$folder"
  exit 0
fi
echo "Publishing through the API did not work; fetching ci-logs instead" >&2

for attempt in $(seq 1 12); do
  rm -f "$workdir/.git/index"
  parent=""
  if git -C "$workdir" fetch -q --depth 1 --filter=blob:none "$remote_url" ci-logs 2>/dev/null; then
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
  if git -C "$workdir" push -q "$remote_url" "$commit:refs/heads/ci-logs"; then
    exit 0
  fi
  # Jobs on every branch publish here; back off with jitter so they stop colliding.
  sleep $((attempt * 2 + RANDOM % 5))
done
echo "Could not publish logs after 12 attempts" >&2
