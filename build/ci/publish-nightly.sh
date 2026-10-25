#!/usr/bin/env bash
# Points a prerelease at the commit being built and replaces its TabStudio.apk: creates the release and its
# tag the first time, and afterwards moves the tag and refreshes the release notes. CI runs it on every push
# to main with the tag `nightly`.
#
# Usage: build/ci/publish-nightly.sh <tag> <apk>
# Needs GH_TOKEN (contents: write), GH_REPO (owner/name) and GITHUB_SHA.
set -euo pipefail

tag="${1:?tag}"
apk="${2:?apk}"
sha="${GITHUB_SHA:?}"
repo="${GH_REPO:?}"
title="TabStudio nightly"
notes="The latest build of \`main\`: commit ${sha}, built $(date -u '+%Y-%m-%d %H:%M UTC').

Install \`TabStudio.apk\` on an Android 9+ tablet with an ARM processor (64-bit or 32-bit). Every nightly is signed with the public development key in \`build/android\`, so it installs as an update over the previous one. Not a stable release."

if gh release view "$tag" > /dev/null 2>&1; then
  # Move the tag to this commit (or recreate it if it went missing), then refresh the release.
  gh api -X PATCH "repos/${repo}/git/refs/tags/${tag}" -f sha="$sha" -F force=true > /dev/null \
    || gh api -X POST "repos/${repo}/git/refs" -f ref="refs/tags/${tag}" -f sha="$sha" > /dev/null
  gh release edit "$tag" --title "$title" --notes "$notes" --prerelease
else
  gh release create "$tag" --title "$title" --notes "$notes" --prerelease --target "$sha"
fi

# Upload under a fixed name, whatever the local file is called, replacing the previous build.
staging="$(mktemp -d)"
cp "$apk" "$staging/TabStudio.apk"
gh release upload "$tag" "$staging/TabStudio.apk" --clobber
rm -rf "$staging"

echo "Release ${tag} now points at ${sha}:"
gh release view "$tag" --json tagName,isPrerelease,targetCommitish,assets \
  --jq '"\(.tagName) prerelease=\(.isPrerelease) assets=\([.assets[] | "\(.name) (\(.size) bytes)"] | join(", "))"'
