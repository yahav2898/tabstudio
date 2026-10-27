#!/usr/bin/env python3
"""Build exact SHA-256-verified upstream flashing sources natively in Alpine.

The Store installs signed compiler/development packages first. Sources, their
licenses and Cargo.lock remain beside the resulting executable. WCH uses the
pinned lockfile and dynamic system libusb, rather than its vendored USB backend.
No target, USB connection, protection operation or vendor toolchain is used.
"""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess

RECIPES = {
    "bossac": {"directory": "BOSSA-1.9.1", "marker": "src/bossac.cpp", "command": ("make", "-j2", "VERSION=1.9.1", "WX_CXXFLAGS=", "WX_LIBS=", "bin/bossac"),
               "binary": "bin/bossac", "check": ("--help",), "exit": 1, "signature": "BOSSA", "version_output": True},
    "teensy_loader_cli": {"directory": "teensy_loader_cli-2.3", "marker": "teensy_loader_cli.c", "command": ("make", "-j2", "OS=LINUX", "teensy_loader_cli"),
                         "binary": "teensy_loader_cli", "check": ("--list-mcus",), "exit": 1, "signature": "TEENSY41", "version_output": False},
    "wchisp": {"directory": "wchisp-0.3.0", "marker": "Cargo.lock", "command": ("cargo", "build", "--locked", "--release", "--no-default-features", "--jobs", "2"),
               "binary": "target/release/wchisp", "check": ("--version",), "exit": 0, "signature": "wchisp", "version_output": True},
    "mspdebug": {"directory": "mspdebug-0.26", "marker": "COPYING", "command": ("make", "-j2", "WITHOUT_READLINE=1"),
                 "binary": "mspdebug", "check": ("--version",), "exit": 0, "signature": "MSPDebug", "version_output": True},
}


def launcher(executable, firmware=None):
    setup = ""
    if firmware is not None:
        setup = "os.environ.setdefault('MSPDEBUG_TI3410_FW', " + repr(str(firmware)) + ")\n"
    return "#!/usr/bin/python3\nimport os, sys\n" + setup + "os.execv(" + repr(str(executable)) + ", [" + repr(str(executable)) + "] + sys.argv[1:])\n"


def install(root, tool, version, bin_directory="/usr/local/bin", run=subprocess.run):
    recipe = RECIPES[tool]
    root = Path(root).resolve(strict=True)
    source = root / recipe["directory"]
    if not source.is_dir() or not (source / recipe["marker"]).is_file():
        raise ValueError("Verified source layout does not match the pinned " + tool + " recipe")
    environment = {key: value for key, value in os.environ.items()
                   if key not in ("LD_PRELOAD", "LD_LIBRARY_PATH", "TABSTUDIO_USB_FD", "TABSTUDIO_USB_DEVICE", "TABSTUDIO_USB_DEVPATH")}
    if tool == "bossac":
        # Upstream relies on glibc's transitive includes for select()/timeval.
        # Its COMMON_CXXFLAGS appends CXXFLAGS, retaining the upstream standard,
        # version, warnings and optimization options while making musl explicit.
        environment["CXXFLAGS"] = (environment.get("CXXFLAGS", "") + " -include sys/select.h -include sys/time.h").strip()
    print("Building " + tool + " " + version + " from verified upstream source", flush=True)
    run(list(recipe["command"]), cwd=str(source), env=environment, check=True, timeout=1800)
    executable = source / recipe["binary"]
    if not executable.is_file() or not executable.resolve(strict=True).is_relative_to(source):
        raise ValueError("Source build did not produce the expected executable")
    with executable.open("rb") as binary:
        if binary.read(4) != b"\x7fELF":
            raise ValueError("Source build did not produce a native ELF executable")
    result = run([str(executable), *recipe["check"]], env=environment, stdout=subprocess.PIPE,
                 stderr=subprocess.STDOUT, text=True, timeout=60, check=False)
    if result.returncode != recipe["exit"] or recipe["signature"] not in result.stdout:
        raise ValueError("Built " + tool + " failed its non-hardware capability check")
    if recipe["version_output"] and re.search(r"(?<![0-9.])" + re.escape(version) + r"(?![0-9.])", result.stdout) is None:
        raise ValueError("Built tool did not report the pinned version")
    output = root / "bin" / tool
    output.parent.mkdir(parents=True, exist_ok=True)
    if executable.resolve(strict=True) != output.resolve():
        shutil.copy2(executable, output)
    output.chmod(0o755)
    firmware = None
    if tool == "mspdebug" and (source / "ti_3410.fw.ihex").is_file():
        firmware = root / "lib" / "mspdebug" / "ti_3410.fw.ihex"
        firmware.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / "ti_3410.fw.ihex", firmware)
    destination = Path(bin_directory)
    destination.mkdir(parents=True, exist_ok=True)
    wrapper = destination / tool
    temporary = wrapper.with_name(tool + ".tabstudio-partial")
    try:
        temporary.write_text(launcher(output, firmware), encoding="utf-8")
        temporary.chmod(0o755)
        os.replace(temporary, wrapper)
    finally:
        temporary.unlink(missing_ok=True)
    if tool == "wchisp":
        # Retain source/licenses/lock; discard reproducible Rust intermediates after copying the binary.
        shutil.rmtree(source / "target")
    print(result.stdout.strip(), flush=True)
    print("Installed source-pinned " + tool + " " + version + "; no hardware was accessed", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tool", choices=RECIPES)
    parser.add_argument("root")
    parser.add_argument("version")
    arguments = parser.parse_args()
    install(arguments.root, arguments.tool, arguments.version)


if __name__ == "__main__":
    main()
