"""Execute the unchanged official legacy Nordic serial DFU binary.

Its x64-only vendor bundle runs natively on x64 and through the explicit Box64
route on ARM64. No Python modules or vendor executable bytes are patched.
"""
import hashlib
import os
from pathlib import Path
import platform
import subprocess
import sys

import box64_runtime

PREFIX = Path("/opt/tabstudio/nordic-dfu")
BINARY_RELATIVE = Path("lib/nrfutil-nrf5sdk-tools/pc_nrfutil_legacy_v6.1.7")
BINARY_SHA256 = "f45cf2f552d2ccbe5dc045a1e0725415d7e65c7ebd813ad4555f21c448cf8065"
X64_SHIM = Path("/opt/tabstudio/embedded/serial-posix-x86_64.so")
NATIVE_SHIM = Path("/opt/tabstudio/embedded/serial-posix.so")
BOX64_MARKER = "TABSTUDIO_NORDIC_BOX64"


def command(arguments, prefix=PREFIX, architecture=None):
    architecture = architecture or platform.machine()
    binary = prefix / BINARY_RELATIVE
    if binary.is_symlink() or not binary.is_file():
        raise ValueError("The original official Nordic legacy executable is missing")
    with binary.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != BINARY_SHA256:
            raise ValueError("The original Nordic executable changed after its pinned installation")
    if architecture not in ("x86_64", "aarch64"):
        raise ValueError("The official legacy DFU route requires x64 or explicit ARM64 Box64")
    if architecture == "aarch64" and os.environ.get(BOX64_MARKER) != "1":
        raise ValueError("Nordic's legacy DFU binary is x64-only. Select the explicit ARM64 Box64 route")
    serial = bool(os.environ.get("TABSTUDIO_SERIAL_DEVICE"))
    shim = X64_SHIM if architecture == "aarch64" else NATIVE_SHIM
    return box64_runtime.command(str(binary), arguments, architecture=architecture,
                                 serial_shim=str(shim) if serial else None)


def main():
    arguments, environment = command(sys.argv[1:])
    # Match the existing PIC/PlatformIO child-launch policy for PRoot/Box64.
    subprocess._USE_VFORK = False
    subprocess._USE_POSIX_SPAWN = False
    result = subprocess.call(arguments, env=environment)
    return result if result >= 0 else 128 - result


if __name__ == "__main__":
    raise SystemExit(main())
