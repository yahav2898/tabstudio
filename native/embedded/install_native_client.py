#!/usr/bin/env python3
"""Validate an official SHA-256-checked native archive and expose one exact CLI.

The Toolchain Store verifies/extracts the archive before calling this script in
its glibc side environment. No network, pip, shell, USB enumeration, patching of
upstream tools or arbitrary tool selection is performed here.
"""
import argparse
import os
from pathlib import Path
import re
import stat
import subprocess

TOOLS = {
    "esptool": {"executable": "esptool", "aliases": ("esptool", "esptool.py"), "arguments": ("version",)},
    "picotool": {"executable": "picotool", "aliases": ("picotool",), "arguments": ("version",)},
}


def locate(root, executable):
    root = Path(root).resolve(strict=True)
    matches = []
    for path in root.rglob(executable):
        if not path.is_file() or not path.resolve(strict=True).is_relative_to(root):
            continue
        with path.open("rb") as source:
            if source.read(4) == b"\x7fELF":
                matches.append(path.resolve(strict=True))
    matches = list(dict.fromkeys(matches))
    if len(matches) != 1:
        raise ValueError("Expected exactly one official native executable named " + executable)
    return matches[0]


def launcher(executable):
    return "#!/usr/bin/python3\nimport os, sys\nos.execv(" + repr(str(executable)) + ", [" + repr(str(executable)) + "] + sys.argv[1:])\n"


def install(root, tool, expected_version, bin_directory="/usr/local/bin", run=subprocess.run):
    definition = TOOLS[tool]
    executable = locate(root, definition["executable"])
    executable.chmod(executable.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
    environment = {key: value for key, value in os.environ.items() if key not in ("LD_PRELOAD", "TABSTUDIO_USB_FD", "TABSTUDIO_USB_DEVICE", "TABSTUDIO_USB_DEVPATH")}
    result = run([str(executable), *definition["arguments"]], check=True, stdout=subprocess.PIPE,
                 stderr=subprocess.STDOUT, text=True, timeout=60, env=environment)
    if re.search(r"(?<![0-9.])" + re.escape(expected_version) + r"(?![0-9.])", result.stdout) is None:
        raise ValueError("Native tool did not report the pinned version " + expected_version)
    destination = Path(bin_directory)
    destination.mkdir(parents=True, exist_ok=True)
    for alias in definition["aliases"]:
        target = destination / alias
        temporary = target.with_name(target.name + ".tabstudio-partial")
        try:
            temporary.write_text(launcher(executable), encoding="utf-8")
            temporary.chmod(0o755)
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)
    print(result.stdout.strip(), flush=True)
    print("Installed native " + tool + " from verified archive: " + str(executable), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tool", choices=TOOLS)
    parser.add_argument("root")
    parser.add_argument("version")
    arguments = parser.parse_args()
    install(arguments.root, arguments.tool, arguments.version)


if __name__ == "__main__":
    main()
