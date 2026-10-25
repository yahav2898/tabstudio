#!/usr/bin/env bash
# Publishes a versioned release for a v* tag: the APK CI built and tested for that tag, with the release
# notes from docs/releases/<version>.md. CI runs it after every job for the tag has passed.
#
# Usage: build/ci/publish-release.sh <tag> <apk>
# Needs GH_TOKEN (contents: write) and GH_REPO (owner/name).
set -euo pipefail

tag="${1:?tag}"
apk="${2:?apk}"
version="${tag#v}"
notes="docs/releases/${version}.md"

test -f "$notes" || { echo "Release notes are missing: ${notes}" >&2; exit 1; }

staging="$(mktemp -d)"
cp "$apk" "$staging/TabStudio-${version}.apk"

if gh release view "$tag" > /dev/null 2>&1; then
  # A re-run of the same tag replaces the notes and the APK.
  gh release edit "$tag" --title "TabStudio ${version}" --notes-file "$notes" --latest
  gh release upload "$tag" "$staging/TabStudio-${version}.apk" --clobber
else
  gh release create "$tag" "$staging/TabStudio-${version}.apk" --verify-tag --latest \
    --title "TabStudio ${version}" --notes-file "$notes"
fi

rm -rf "$staging"
gh release view "$tag" --json tagName,url,assets --jq '"\(.tagName) \(.url) \([.assets[] | "\(.name) (\(.size) bytes)"] | join(", "))"'
