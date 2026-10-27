"""Install the verified original Nordic plugin data and validate its legacy CLI.

The Toolchain Store verifies the complete official archive SHA-256 before extraction.
This second guard binds the actual executable bytes and retains every vendor license/document.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys

import nordic_runtime

PACKAGE = "nrfutil-nrf5sdk-tools-x86_64-unknown-linux-gnu-1.1.0"
SOURCE = Path("/opt/tabstudio/nordic-dfu-download") / PACKAGE


def checked_source(source):
    if source.is_symlink() or not source.is_dir():
        raise ValueError("The verified official Nordic package directory is missing")
    files = list(source.rglob("*"))
    if any(path.is_symlink() or not (path.is_dir() or path.is_file()) for path in files):
        raise ValueError("Nordic package must contain only regular files and directories")
    manifest = json.loads((source / "manifest.json").read_text())
    if manifest.get("name") != "nrfutil-nrf5sdk-tools" or manifest.get("version") != "1.1.0" or manifest.get("platform") != "x86_64-unknown-linux-gnu":
        raise ValueError("Unexpected Nordic package identity/version/platform")
    data = source / "data"
    binary = data / nordic_runtime.BINARY_RELATIVE
    with binary.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != nordic_runtime.BINARY_SHA256:
            raise ValueError("The official legacy executable does not match its reviewed SHA-256")
    license_file = data / "share/nrfutil-nrf5sdk-tools/LICENSE"
    if "LicenseRef-Nordic-1-Clause" not in license_file.read_text():
        raise ValueError("The complete vendor license must remain beside the original tool")
    return data


def install(source=SOURCE, prefix=nordic_runtime.PREFIX, launcher=Path("/usr/local/bin/nrfutil")):
    data = checked_source(source)
    if prefix.is_symlink() or launcher.is_symlink():
        raise ValueError("Nordic installer does not follow an existing prefix/launcher symlink")
    prefix.mkdir(parents=True, exist_ok=True)
    if any(path.is_symlink() for path in prefix.rglob("*")):
        raise ValueError("Nordic installation contains a symlink; no original files were replaced")
    shutil.copytree(data, prefix, dirs_exist_ok=True)
    shutil.copy2(source / "manifest.json", prefix / "vendor-manifest.json")
    shutil.copy2(source / "files", prefix / "vendor-files")
    environment = dict(os.environ)
    if platform.machine() == "aarch64":
        environment[nordic_runtime.BOX64_MARKER] = "1"
    old = os.environ.copy()
    try:
        os.environ.update(environment)
        for arguments, expected in [(["version"], "nrfutil version 6.1.7"),
                                    (["dfu", "serial", "--help"], "UART serial DFU"),
                                    (["dfu", "usb-serial", "--help"], "USB serial DFU")]:
            command, execution_environment = nordic_runtime.command(arguments, prefix)
            result = subprocess.run(command, env=execution_environment, text=True, stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, timeout=90)
            print(result.stdout, end="", flush=True)
            if result.returncode or expected not in result.stdout:
                raise RuntimeError("The actual official Nordic serial CLI failed validation")
    finally:
        os.environ.clear()
        os.environ.update(old)
    # A dedicated named tool selection is the explicit translated route; no other client is redirected.
    translated = "os.environ['" + nordic_runtime.BOX64_MARKER + "'] = '1'\n" if platform.machine() == "aarch64" else ""
    launcher.parent.mkdir(parents=True, exist_ok=True)
    launcher.write_text("#!/usr/bin/python3\nimport os,sys\nsys.path.insert(0, '/opt/tabstudio/embedded')\n"
                        + translated + "import nordic_runtime\nraise SystemExit(nordic_runtime.main())\n")
    launcher.chmod(0o755)
    print("NORDIC_LEGACY_INSTALLED hardware=false translated=" + str(platform.machine() == "aarch64").lower(), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    install()


if __name__ == "__main__":
    main()
