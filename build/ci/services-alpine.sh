#!/usr/bin/env bash
# Prepare the tablet's Alpine and Termux PRoot on a native Linux CI runner (x86_64 or aarch64).
# No Docker, chroot, root daemon, or host PostgreSQL/Redis is used. sudo only installs host build dependencies.
set -euo pipefail
out="$(realpath -m "${1:?usage: services-alpine.sh output-directory}")"
mkdir -p "$out" "$out/tmp" "$out/rootfs"
sudo apt-get update -qq
sudo apt-get install -y -qq build-essential libtalloc-dev gawk unzip curl python3 pkg-config
version=5.1.107.101
curl -fsSL --retry 3 "https://github.com/termux/proot/archive/v$version.zip" -o "$out/proot.zip"
echo "a66743f50c7b0bac1913b83bf55e139d679c80a0ca2808eec76432a65640017f  $out/proot.zip" | sha256sum -c -
unzip -qo "$out/proot.zip" -d "$out"
# This harness runs only native 64-bit guests; disable the optional 32-bit loader (GCC on ARM64 has no -m32).
# Same includes as the Android build; the upstream sources need these with recent libc/compiler headers.
printf '#if !defined(__ASSEMBLER__) && __STDC_HOSTED__\n#include <string.h>\n#include <sys/ioctl.h>\n#endif\n' > "$out/includes.h"
CPPFLAGS="-DARG_MAX=131072 -DVERSION=\\\"$version\\\"" \
  CFLAGS="-O2 -include $out/includes.h" PROOT_UNBUNDLE_LOADER=. \
  make -C "$out/proot-$version/src" -j2 HAS_LOADER_32BIT= proot loader/loader > "$out/proot-build.log" 2>&1 || { tail -80 "$out/proot-build.log"; exit 1; }
export TABSTUDIO_SERVICE_PROOT="$out/proot-$version/src/proot"
export TABSTUDIO_SERVICE_LOADER="$out/proot-$version/src/loader/loader"
export TABSTUDIO_SERVICE_ROOTFS="$out/rootfs"
arch=$(uname -m)
case "$arch" in x86_64|aarch64) ;; *) echo "Unsupported runner architecture: $arch" >&2; exit 1 ;; esac
python3 - "$out" "$arch" <<'PY'
import hashlib,json,pathlib,sys,urllib.request
out,arch=pathlib.Path(sys.argv[1]),sys.argv[2]
catalog=json.loads(pathlib.Path('src/TabStudio.Toolchains/Catalog/toolchains.json').read_text())['linux']
item=catalog['rootfs'][arch]
url=f"{catalog['mirror']}/{catalog['branch']}/releases/{arch}/{item['file']}"
with urllib.request.urlopen(url,timeout=90) as response: data=response.read()
assert hashlib.sha256(data).hexdigest()==item['sha256'],'Alpine rootfs SHA-256 mismatch'
(out/'rootfs.tar.gz').write_bytes(data)
(out/'rootfs/etc/apk').mkdir(parents=True,exist_ok=True)
(out/'repositories').write_text(''.join(f"{catalog['mirror']}/{catalog['branch']}/{repo}\n" for repo in catalog['repositories']))
PY
# Extract as the runner's ordinary user, exactly as the app does.
tar -xzf "$out/rootfs.tar.gz" -C "$out/rootfs" --no-same-owner
cp /etc/resolv.conf "$out/rootfs/etc/resolv.conf"
cp "$out/repositories" "$out/rootfs/etc/apk/repositories"
export PROOT_TMP_DIR="$out/tmp" PROOT_LOADER="$TABSTUDIO_SERVICE_LOADER"
"$TABSTUDIO_SERVICE_PROOT" --kill-on-exit --link2symlink --sysvipc -L -0 -r "$out/rootfs" -b /dev -b /proc -b /sys -w / \
 /bin/sh -ec 'apk add --no-cache postgresql18 postgresql18-client redis gzip; psql --version; redis-server --version' > "$out/packages.log" 2>&1 || { cat "$out/packages.log"; exit 1; }
cat "$out/packages.log"
if [ -n "${GITHUB_ENV:-}" ]; then
  printf 'TABSTUDIO_SERVICE_PROOT=%s\nTABSTUDIO_SERVICE_LOADER=%s\nTABSTUDIO_SERVICE_ROOTFS=%s\n' "$TABSTUDIO_SERVICE_PROOT" "$TABSTUDIO_SERVICE_LOADER" "$TABSTUDIO_SERVICE_ROOTFS" >> "$GITHUB_ENV"
fi
