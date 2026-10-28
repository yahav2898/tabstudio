#!/usr/bin/env bash
# Adds TabStudio's --net-map extension (native/containers/net_map.c) to a pinned PRoot source tree.
# Containers share the device's network; the extension rewrites the ports they bind and the service addresses they
# connect to, so two containers can listen on the same port. See src/TabStudio.Containers/Runtime/NetworkMap.cs.
set -euo pipefail
native="$(cd "$(dirname "$0")" && pwd)"
source_dir=${1:?usage: apply-net-map.sh PINNED_PROOT_SOURCE}
patch -d "$source_dir" -p1 < "$native/proot-net-map.patch"
mkdir -p "$source_dir/src/extension/net_map"
cp "$native/../../../native/containers/net_map.c" "$source_dir/src/extension/net_map/net_map.c"
