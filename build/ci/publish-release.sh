#!/usr/bin/env bash
# Publishes a versioned release with the APK CI built and tested, and the notes in docs/releases/<version>.md.
#
# CI runs it after every job has passed:
# - on main, with "auto": publishes the version in Directory.Build.props once, the first time main builds
#   with that version and its release notes exist. Bumping the version and adding notes is how to release.
# - for a pushed v* tag: publishes (or refreshes) that tag's release.
#
# Usage: build/ci/publish-release.sh <auto|tag> <apk>
# Needs GH_TOKEN (contents: write), GH_REPO (owner/name) and GITHUB_SHA.
set -euo pipefail

mode="${1:?auto or tag}"
apk="${2:?apk}"
sha="${GITHUB_SHA:?}"

if [ "$mode" = "auto" ]; then
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
  tag="v${version}"
  if [ ! -f "docs/releases/${version}.md" ]; then
    echo "No release notes for ${version} (docs/releases/${version}.md): nothing to publish."
    exit 0
  fi
  if gh release view "$tag" > /dev/null 2>&1; then
    echo "${tag} is already published."
    exit 0
  fi
else
  tag="$mode"
  version="${tag#v}"
fi

notes="docs/releases/${version}.md"
test -f "$notes" || { echo "Release notes are missing: ${notes}" >&2; exit 1; }

staging="$(mktemp -d)"
cp "$apk" "$staging/TabStudio-${version}.apk"

# Main's runs no longer cancel each other, so an older version's run can finish after a newer one's: only the
# highest version published is marked latest, which is where the README's Download link goes.
newest="$({ gh release list --limit 200 --json tagName,isPrerelease --jq '.[] | select(.isPrerelease | not) | .tagName'
           echo "$tag"; } | sed 's/^v//' | sort -V | tail -n 1)"
latest="--latest"
[ "$newest" = "$version" ] || latest="--latest=false"

# GitHub sometimes answers a release request with a 500 (0.21.4's first try): try again, and a release that was
# created before the error is completed instead of created twice.
for attempt in 1 2 3 4; do
  if gh release view "$tag" > /dev/null 2>&1; then
    # A re-run of the same tag replaces the notes and the APK.
    gh release edit "$tag" --title "TabStudio ${version}" --notes-file "$notes" "$latest" &&
      gh release upload "$tag" "$staging/TabStudio-${version}.apk" --clobber && break
  elif gh release create "$tag" "$staging/TabStudio-${version}.apk" --target "$sha" "$latest" \
         --title "TabStudio ${version}" --notes-file "$notes"; then
    break
  fi
  [ "$attempt" -lt 4 ] || { echo "Could not publish ${tag}" >&2; exit 1; }
  sleep $((attempt * 15))
done

rm -rf "$staging"
gh release view "$tag" --json tagName,url,assets --jq '"\(.tagName) \(.url) \([.assets[] | "\(.name) (\(.size) bytes)"] | join(", "))"'
