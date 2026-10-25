#!/usr/bin/env bash
# Publishes the contents of ci-out/ to the `ci-logs` branch like publish-logs.sh, but through GitHub's Git Data API
# instead of a clone: it uploads the files as blobs, builds the job's folder as a tree, and commits it on top of the
# branch's newest commit, replacing only that folder. Nothing is checked out, so it works on Windows runners, where
# the branch's deepest paths are too long to check out and Git Bash rewrites path-like arguments.
#
# Usage: build/ci/publish-logs-api.sh <job-name> <job-status>    (needs GH_TOKEN and the gh CLI)
set -euo pipefail

job="${1:?job name}"
status="${2:?job status}"
repo="${GITHUB_REPOSITORY:?}"
: "${GH_TOKEN:?}"
export MSYS_NO_PATHCONV=1

branch="${GITHUB_REF_NAME:-main}"
if [ "$branch" = "main" ]; then
  folder="$job"
else
  folder="branches/${branch//\//-}/$job"
fi

# A folder in the workspace, not mktemp: on Windows, mktemp gives an MSYS path (/tmp/...) that the native gh.exe cannot open.
work=".ci-publish"
rm -rf "$work"
mkdir -p "$work"

# Runs gh api; on failure, keeps why, which the script says as notices when it ends: they can be read through the API
# even when the logs are not published.
trap 'cat "$work/notices" 2>/dev/null || true' EXIT
api() {
  local out
  if ! out=$(gh api "$@" 2>&1); then
    echo "::notice title=publish-logs-api::gh api $1 failed: ${out//$'\n'/ }" >> "$work/notices"
    return 1
  fi
  printf '%s' "$out"
}
mkdir -p ci-out
echo "job=${job} branch=${branch} status=${status} sha=${GITHUB_SHA} run=${GITHUB_RUN_ID} time=$(date -u +%FT%TZ)" > ci-out/STATUS

# One blob per file, and one tree entry for it, relative to the job's folder.
entries=""
while IFS= read -r -d '' file; do
  relative="${file#ci-out/}"
  {
    printf '{"encoding":"base64","content":"'
    base64 -w0 "$file"
    printf '"}'
  } > "$work/blob.json"
  sha=$(api "repos/$repo/git/blobs" --input "$work/blob.json" --jq .sha)
  entries="${entries:+$entries,}{\"path\":\"${relative}\",\"mode\":\"100644\",\"type\":\"blob\",\"sha\":\"${sha}\"}"
done < <(find ci-out -type f -print0)

printf '{"tree":[%s]}' "$entries" > "$work/folder.json"
folder_tree=$(api "repos/$repo/git/trees" --input "$work/folder.json" --jq .sha)

# Jobs publish in parallel: build on the newest commit, and try again when another job's commit came first.
for attempt in 1 2 3 4 5 6; do
  head=$(api "repos/$repo/git/ref/heads/ci-logs" --jq .object.sha)
  base_tree=$(api "repos/$repo/git/commits/$head" --jq .tree.sha)
  printf '{"base_tree":"%s","tree":[{"path":"%s","mode":"040000","type":"tree","sha":"%s"}]}' "$base_tree" "$folder" "$folder_tree" > "$work/root.json"
  root=$(api "repos/$repo/git/trees" --input "$work/root.json" --jq .sha)
  printf '{"message":"%s: %s @ %s","tree":"%s","parents":["%s"]}' "$folder" "$status" "${GITHUB_SHA::8}" "$root" "$head" > "$work/commit.json"
  commit=$(api "repos/$repo/git/commits" --input "$work/commit.json" --jq .sha)
  if api -X PATCH "repos/$repo/git/refs/heads/ci-logs" -f sha="$commit" -F force=false > /dev/null; then
    echo "Published ci-out to ci-logs:$folder"
    exit 0
  fi
  sleep $((attempt * 3))
done

echo "Could not publish logs after 6 attempts" >&2
exit 1
