#!/usr/bin/env bash
# Download a pinned source archive. Only transport failures may use an official mirror;
# a completed download or cached file with the wrong hash is always a hard failure.
fetch() (
  set -euo pipefail
  local primary="$1" sha="$2" file="$3" fallback="${4:-}" url partial
  if [ -f "$file" ]; then
    printf '%s  %s\n' "$sha" "$file" | sha256sum -c --quiet - || {
      echo "Native source cache does not match its pinned SHA-256: $file" >&2
      return 1
    }
    return
  fi

  mkdir -p "$(dirname "$file")"
  # The temporary file shares the destination's filesystem, so publishing it is one atomic rename.
  partial="$(mktemp "$file.part.XXXXXX")"
  trap 'rm -f "$partial"' EXIT
  for url in "$primary" "$fallback"; do
    [ -n "$url" ] || continue
    echo "Downloading pinned native source: $url" >&2
    if curl -fsSL --connect-timeout 15 --max-time 60 \
      --retry 3 --retry-delay 1 --retry-max-time 120 --retry-all-errors \
      -o "$partial" "$url"; then
      printf '%s  %s\n' "$sha" "$partial" | sha256sum -c --quiet - || {
        echo "Native source does not match its pinned SHA-256: $url" >&2
        return 1
      }
      mv "$partial" "$file"
      return
    fi
    echo "Native source transport failed: $url" >&2
    rm -f "$partial"
  done
  echo "Could not download pinned native source: $primary" >&2
  return 1
)
