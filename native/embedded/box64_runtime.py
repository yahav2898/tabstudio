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
import struct
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
    # The installed translator's PT_INTERP names its private loader. Box64's
    # own execve hooks then start children through that same loader too.
    return [str(root / "box64"), str(binary), *arguments], env


def set_translator_loader(binary, interpreter):
    """Retarget only our translator copy's PT_INTERP, without moving ELF segments.

    Compiler/package binaries are never modified. The chosen canonical private
    loader path fits the APK translator's existing GNU/Linux interpreter field.
    """
    with binary.open("r+b") as output:
        header = output.read(64)
        if len(header) != 64 or header[:7] != b"\x7fELF\x02\x01\x01" or header[18:20] != b"\xb7\x00":
            raise ValueError("Translator is not an ARM64 little-endian ELF")
        table = struct.unpack_from("<Q", header, 32)[0]
        size, count = struct.unpack_from("<HH", header, 54)
        if size < 56 or count > 1024:
            raise ValueError("Invalid translator program headers")
        for index in range(count):
            output.seek(table + index * size)
            entry = output.read(56)
            if len(entry) != 56:
                raise ValueError("Truncated translator program headers")
            if struct.unpack_from("<I", entry)[0] != 3:
                continue
            offset, length = struct.unpack_from("<Q", entry, 8)[0], struct.unpack_from("<Q", entry, 32)[0]
            replacement = str(interpreter).encode("utf-8") + b"\0"
            if not 1 <= length <= 4096 or len(replacement) > length:
                raise ValueError("Private translator loader does not fit its existing interpreter field")
            output.seek(offset)
            previous = output.read(length)
            if len(previous) != length or b"\0" not in previous:
                raise ValueError("Invalid translator interpreter field")
            output.seek(offset)
            output.write(replacement.ljust(length, b"\0"))
            return
    raise ValueError("The bundled translator has no dynamic interpreter field")


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
        loader = root / "ld.so"
        target = root / "runtime/arm64/usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1"
        if not target.is_file():
            raise ValueError("The verified ARM64 runtime has no matching loader")
        if loader.is_symlink():
            loader.unlink()
        elif loader.exists():
            raise ValueError("The private translator loader path is occupied")
        loader.symlink_to(target)
        set_translator_loader(root / "box64", loader)
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
