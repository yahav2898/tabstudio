"""Private open-source Box64 runtime, using the existing verified PIC extractor.

Never installs Microchip software, replaces the guest's libc, or registers binfmt.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import subprocess
import sys

ROOT = Path("/opt/tabstudio/box64")


def command(binary, arguments=(), architecture=None, root=ROOT):
    architecture = architecture or platform.machine()
    env = dict(os.environ)
    if architecture == "x86_64":
        return [str(binary), *arguments], env
    if architecture != "aarch64":
        raise ValueError("Box64 requires an ARM64 or x64 host")
    native = root / "runtime/arm64/usr/lib/aarch64-linux-gnu"
    guest = root / "runtime/amd64/usr/lib/x86_64-linux-gnu"
    env.update(LD_LIBRARY_PATH=str(native), BOX64_LD_LIBRARY_PATH=str(guest),
               BOX64_NOBANNER="1", BOX64_LOG="0", BOX64_DYNAREC="0", BOX64_DYNACACHE="0")
    # Use the matching pinned loader explicitly. Ubuntu's libc and loader stay intact.
    return [str(native / "ld-linux-aarch64.so.1"), "--library-path", str(native),
            str(root / "box64"), str(binary), *arguments], env


def prepare(root=ROOT):
    spec = importlib.util.spec_from_file_location("tabstudio_shared_pic_extractor", root / "support/install.py")
    shared = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(shared)
    assets = json.loads((root / "support/assets.lock.json").read_text())["assets"]
    for arch in ("amd64", "arm64") if platform.machine() == "aarch64" else ("amd64",):
        destination = root / "runtime" / arch
        destination.mkdir(parents=True, exist_ok=True)
        for package in ("libc6", "libstdc++6", "libgcc-s1", "zlib1g"):
            name = package + "-" + arch
            archive = root / "downloads" / (name + ".deb")
            digest = assets[name]["sha256"]
            with archive.open("rb") as source:
                actual = hashlib.file_digest(source, "sha256").hexdigest()
            if actual != digest:
                raise ValueError("Unverified runtime archive: " + name)
            marker = destination / (".tabstudio-" + name)
            if not marker.exists() or marker.read_text() != digest:
                shared.extract_deb(archive, destination)
                marker.write_text(digest)
    if platform.machine() == "aarch64":
        # Verify the private native loader can execute the actual bundled translator.
        invocation, environment = command("--version", root=root)
        result = subprocess.run(invocation, env=environment, text=True, capture_output=True, timeout=30)
        print(result.stdout, end="")
        print(result.stderr, end="", file=sys.stderr)
        if result.returncode or "Box64" not in result.stdout + result.stderr:
            raise RuntimeError("Bundled Box64/private ARM64 runtime did not pass its real version check")
    print("BOX64_RUNTIME_PREPARED hardware=false", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=["prepare"])
    parser.parse_args()
    prepare()


if __name__ == "__main__":
    main()
