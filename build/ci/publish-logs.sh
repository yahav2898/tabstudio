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
#
# That push is the slow part (8 to 9 minutes for the IDE job's 170 MB of screenshots), and about half of those files
# come out the same as on the folder's last publish, so GitHub has them already: only the others are pushed, and the
# folder's tree is built on GitHub from every file's path and hash.
publish_through_api() {
  command -v gh > /dev/null && command -v jq > /dev/null || return 1
  [ -z "${TABSTUDIO_PUBLISH_REMOTE:-}" ] || return 1
  export GH_TOKEN="${GH_TOKEN:-$GITHUB_TOKEN}"
  local repo="$GITHUB_REPOSITORY" api_dir folder_tree staged temp head base root commit published=""
  api_dir="$(mktemp -d)" || return 1
  rm -f "$workdir/.git/index"
  git -C "$staging" --git-dir="$workdir/.git" --work-tree="$staging" add -A . || return 1
  git -C "$workdir" -c core.quotePath=false ls-files -s > "$api_dir/files" || return 1
  : > "$api_dir/known"
  if head="$(gh api "repos/$repo/git/ref/heads/ci-logs" --jq .object.sha 2> /dev/null)"; then
    gh api "repos/$repo/git/trees/$head:$folder?recursive=1" --jq '.tree[] | select(.type == "blob") | .sha' \
      > "$api_dir/known" 2> /dev/null || : > "$api_dir/known"
  fi
  # A tree of just the new files, each named by its hash, on a commit with no parent.
  awk -F'\t' 'FILENAME == ARGV[1] { known[$1] = 1; next } { split($1, f, " "); if (!(f[2] in known)) print "100644 blob " f[2] "\t" f[2] }' \
    "$api_dir/known" "$api_dir/files" | sort -u > "$api_dir/new"
  staged="$(git -C "$workdir" mktree < "$api_dir/new")" || return 1
  staged="$(git -C "$workdir" commit-tree "$staged" -m "${folder}: staged @ ${GITHUB_SHA::8}")" || return 1
  temp="ci-logs-staging/${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT:-1}-${job}"
  git -C "$workdir" push -q "$remote_url" "+$staged:refs/heads/$temp" || return 1
  echo "Pushed $(wc -l < "$api_dir/new") of $(wc -l < "$api_dir/files") files; the others are on GitHub already"
  jq -Rn '{tree: [inputs | split("\t") as [$meta, $path] | ($meta | split(" ")) as [$mode, $sha]
                  | {path: $path, mode: $mode, type: "blob", sha: $sha}]}' < "$api_dir/files" > "$api_dir/folder.json"
  folder_tree="$(gh api "repos/$repo/git/trees" --input "$api_dir/folder.json" --jq .sha)" || folder_tree=""
  # The same files make the same tree; anything else (a name git had to quote, say) goes to the fallback.
  if [ "$folder_tree" != "$(git -C "$workdir" write-tree)" ]; then
    git -C "$workdir" push -q "$remote_url" ":refs/heads/$temp" || true
    return 1
  fi
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
