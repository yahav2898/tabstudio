#!/usr/bin/env python3
"""Build the APK's standalone GNU/Linux ARM64 box64 (MIT), executed by PRoot.

It deliberately uses GNU/Linux glibc, with the same pins and patch as the real
ARM64 spike. The Store installs its verified ARM64 glibc runtime. This is not a
JNI library and is never dlopened by Android's Bionic linker.
"""
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile

import spike


def build(output):
    output.mkdir(parents=True, exist_ok=True)
    version = spike.LOCK["box64-source"]["sha256"] + " child-stack-v1, 16KB"
    binary, stamp = output / "libbox64.so", output / "BOX64.VERSION"
    if binary.exists() and stamp.exists() and stamp.read_text() == version:
        return
    with tempfile.TemporaryDirectory(prefix="tabstudio-box64-") as temporary:
        work = Path(temporary)
        root, logs, cache = work / "root", work / "logs", work / "downloads"
        (root / "usr/bin").mkdir(parents=True)
        logs.mkdir()
        cache.mkdir()
        spike.build_current_box64(work, root, logs, cache, fix_clone_stack=True,
                                  target_arch="aarch64", compiler="aarch64-linux-gnu-gcc")
        built = root / "usr/bin/box64"
        subprocess.run(["aarch64-linux-gnu-strip", str(built)], check=True)
        headers = subprocess.check_output(["readelf", "-Wl", str(built)], text=True)
        loads = [line for line in headers.splitlines() if line.lstrip().startswith("LOAD ")]
        if not loads or any(int(line.split()[-1], 16) < 16384 for line in loads):
            raise ValueError("box64's ELF load segments must support Android's 16 KB pages")
        shutil.copy2(built, binary)
        stamp.write_text(version)
        (output / "libbox64.so.sha256").write_text(hashlib.sha256(binary.read_bytes()).hexdigest() + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    build(parser.parse_args().output.resolve())
