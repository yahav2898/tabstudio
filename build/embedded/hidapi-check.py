#!/usr/bin/env python3
"""Actual distro HIDAPI ABI/interposition proof using an explicitly synthetic USB transport.

Requires signed distro hidapi-dev and libusb-dev. The client remains linked to
hidapi-hidraw, as Alpine's unmodified OpenOCD is. No real USB nodes are opened.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def run(arguments, **options):
    return subprocess.run(list(map(str, arguments)), check=True, text=True, **options)


def locate(prefix, name):
    for relative in ("usr/lib", "usr/lib/x86_64-linux-gnu", "usr/lib/aarch64-linux-gnu", "lib/x86_64-linux-gnu", "lib/aarch64-linux-gnu"):
        path = prefix / relative / name
        if path.exists():
            return path
    raise FileNotFoundError("Install the signed hidapi runtime/development package; missing " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prefix", type=Path, default=Path("/"), help="Optional extracted distro package root for host verification")
    args = parser.parse_args()
    prefix = args.prefix.resolve()
    hidraw = locate(prefix, "libhidapi-hidraw.so.0")
    hidusb = locate(prefix, "libhidapi-libusb.so.0")
    include = prefix / "usr/include"
    if not (include / "hidapi/hidapi.h").is_file() or not (include / "libusb-1.0/libusb.h").is_file():
        raise FileNotFoundError("Install signed hidapi-dev and libusb-dev headers")
    with tempfile.TemporaryDirectory(prefix="tabstudio-hidapi-") as temporary:
        folder = Path(temporary)
        fake = folder / "fake-usb.so"
        bridge = folder / "libusb-fd.so"
        client = folder / "hidraw-client"
        cc = [shutil.which("cc") or "cc", "-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-I", include]
        run([*cc, "-shared", "-fPIC", ROOT / "build/embedded/hidapi-check/fake_libusb.c", "-o", fake, "-pthread"])
        run([*cc, "-shared", "-fPIC", ROOT / "src/TabStudio.Toolchains/Microchip/usb_fd.c", "-o", bridge, "-Wl,-l:libusb-1.0.so.0", "-ldl", "-pthread"])
        run([*cc, ROOT / "build/embedded/hidapi-check/client.c", hidraw, "-o", client, "-ldl", "-Wl,-rpath," + str(hidraw.parent)])
        dynamic = run(["readelf", "-d", client], capture_output=True).stdout
        assert "libhidapi-hidraw.so.0" in dynamic, dynamic
        environment = dict(os.environ)
        environment.pop("LD_PRELOAD", None)
        environment["LD_LIBRARY_PATH"] = str(hidraw.parent) + (":" + environment["LD_LIBRARY_PATH"] if environment.get("LD_LIBRARY_PATH") else "")
        original = run([client, "check-original-linkage"], env=environment, timeout=5, capture_output=True).stdout
        assert "ORIGINAL_CLIENT_LINKED_HIDRAW version=" in original, original
        print(original, end="", flush=True)
        version = original.split("version=", 1)[1].strip()
        descriptor = os.open(os.devnull, os.O_RDWR)
        try:
            environment.update(LD_PRELOAD=str(bridge) + ":" + str(hidusb) + ":" + str(fake), TABSTUDIO_USB_FD="3", TABSTUDIO_EXPECT_HID_VERSION=version)
            run([client], env=environment, pass_fds=(descriptor,), preexec_fn=lambda: os.dup2(descriptor, 3), timeout=10)
            # HID-only probes may have no interrupt OUT endpoint. The unchanged
            # HIDAPI backend must then use SET_REPORT through the same granted fd.
            run([client], env={**environment, "TABSTUDIO_TEST_HID_CONTROL_ONLY": "1"}, pass_fds=(descriptor,), preexec_fn=lambda: os.dup2(descriptor, 3), timeout=10)
        finally:
            os.close(descriptor)
        # On Alpine the real CLI is present too. This proves preload resolution
        # and its unchanged hidraw DT_NEEDED linkage, not a physical HID exchange.
        if prefix == Path("/") and shutil.which("openocd"):
            original = run(["readelf", "-d", shutil.which("openocd")], capture_output=True).stdout
            assert "libhidapi-hidraw.so.0" in original or "libhidapi-libusb.so.0" in original, original
            run(["openocd", "--version"], env={**os.environ, "LD_PRELOAD": str(bridge) + ":" + str(hidusb)}, timeout=5)
            print("UNMODIFIED_OPENOCD_HIDAPI_LOAD_PASSED hardware=false")


if __name__ == "__main__":
    main()
