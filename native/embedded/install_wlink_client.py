#!/usr/bin/env python3
"""Build the unchanged pinned WCH-Link source using the existing native installer.

This installs a tool, not a permission to program: managed programming additionally
requires the opt-in WCH USB protocol guard. Version/help checks do not open USB.
"""
import hashlib
import os
from pathlib import Path
import re
import runpy
import shutil
import subprocess
import sys

REVISION = "7ed4b99321fb8e88eb05f7553211b39d41196a6c"
LOCK_SHA256 = "048dc8ff2443e1739065d060c2b196cf53e8ea75dde268750ee99b429f1d91c7"


def native_host(output):
    matches = re.findall(r"^host:\s*([A-Za-z0-9_.-]+)\s*$", output, re.MULTILINE)
    if len(matches) != 1 or not re.fullmatch(r"(?:aarch64|x86_64)-[A-Za-z0-9_.-]*linux-[A-Za-z0-9_.-]+", matches[0]):
        raise ValueError("WCH-Link requires the installed Rust compiler's native ARM64/x64 Linux host, not a bare-metal target")
    return matches[0]


def validate_elf_host(binary, host):
    with Path(binary).open("rb") as stream:
        header = stream.read(20)
    machine = 183 if host.startswith("aarch64-") else 62
    if len(header) != 20 or header[:7] != b"\x7fELF\x02\x01\x01" or int.from_bytes(header[18:20], "little") != machine:
        raise ValueError("The WCH-Link build did not produce the selected native 64-bit ELF architecture")


def install(source_installer, root, bin_directory="/usr/local/bin", run=subprocess.run, check_output=subprocess.check_output):
    root = Path(root).resolve(strict=True)
    source = root / ("wlink-" + REVISION)
    lock = source / "Cargo.lock"
    if source.is_symlink() or lock.is_symlink() or not lock.is_file() or hashlib.sha256(lock.read_bytes()).hexdigest() != LOCK_SHA256:
        raise ValueError("WCH-Link's source/dependency lock does not match its pinned upstream commit")
    environment = {key: value for key, value in os.environ.items()
                   if key not in ("LD_PRELOAD", "LD_LIBRARY_PATH", "TABSTUDIO_USB_FD", "TABSTUDIO_USB_DEVICE", "TABSTUDIO_USB_DEVPATH")}
    host = native_host(check_output(["rustc", "-vV"], env=environment, text=True, timeout=30))
    helper = runpy.run_path(str(source_installer), run_name="tabstudio_shared_source_installer")
    helper["RECIPES"]["wlink"] = {
        "directory": source.name, "marker": "Cargo.lock",
        "command": ("cargo", "build", "--locked", "--release", "--target", host, "--jobs", "2"),
        "binary": "target/" + host + "/release/wlink", "check": ("--version",),
        "exit": 0, "signature": "wlink", "version_output": True,
    }
    def checked_run(command, **arguments):
        # The shared builder owns its private output directory. A global Cargo
        # cache override must not move this tool into a user's firmware build.
        arguments["env"].pop("CARGO_TARGET_DIR", None)
        result = run(command, **arguments)
        if hashlib.sha256(lock.read_bytes()).hexdigest() != LOCK_SHA256:
            raise ValueError("The native build changed the pinned Cargo.lock")
        if command[:2] == ["cargo", "build"]:
            validate_elf_host(source / helper["RECIPES"]["wlink"]["binary"], host)
        return result

    helper["install"](root, "wlink", "0.1.3", bin_directory, checked_run)
    if hashlib.sha256(lock.read_bytes()).hexdigest() != LOCK_SHA256:
        raise ValueError("The native build changed the pinned Cargo.lock")
    shutil.rmtree(source / "target")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Supply the shared source installer and verified WCH-Link extraction directory")
    install(sys.argv[1], sys.argv[2])
