#!/usr/bin/env python3
"""Real patched PRoot + static raw-syscall client, with a synthetic granted FD and no hardware."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--proot", type=Path, required=True)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="tabstudio-usb-fd-") as work:
        binary = Path(work) / "raw-usb-client"
        subprocess.run(["cc", "-static", "-std=gnu17", "-Wall", "-Wextra", "-Werror", str(ROOT / "build/embedded/emulators/usb-fd-raw.c"), "-o", str(binary)], check=True)
        device = Path(work) / "granted-descriptor"
        device.write_bytes(b"GRANTED_USB_HANDLE\n\0")
        descriptor = os.open(device, os.O_RDWR)
        try:
            def inherit():
                os.dup2(descriptor, 3)
            base = {**os.environ, "TABSTUDIO_USB_DEVPATH": "/dev/bus/usb/001/002", "TABSTUDIO_USB_FD": "3"}
            for mode in ["normal", "reused", "disabled"]:
                env = dict(base)
                if mode == "disabled":
                    env.pop("TABSTUDIO_USB_FD")
                result = subprocess.run([str(args.proot.resolve()), "-r", "/", str(binary), mode], env=env,
                                        pass_fds=(descriptor,), preexec_fn=inherit, capture_output=True, text=True, timeout=15)
                print(result.stdout, end="")
                if result.returncode:
                    raise RuntimeError(f"{mode}: {result.returncode}\n{result.stderr}")
                expected = {"normal": "USB_FD_RAW_SYSCALL_PASSED", "reused": "USB_FD_REUSE_REFUSED", "disabled": "USB_FD_DISABLED"}[mode]
                if expected not in result.stdout:
                    raise RuntimeError("Missing completion marker for " + mode)
        finally:
            os.close(descriptor)


if __name__ == "__main__":
    main()
