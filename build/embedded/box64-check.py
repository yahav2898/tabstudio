#!/usr/bin/env python3
"""Real x86 compiler/child-process proof, run inside the production native guest.

The fixture deliberately fetches an x86 compiler on both runners. Production's
native-first registry policy is tested separately and never forces this override.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys

sys.path.insert(0, "/opt/tabstudio/embedded")
import platformio_box64 as boxed


def run(arguments):
    result = subprocess.run(list(map(str, arguments)), check=True, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180, pass_fds=(3,))
    print(result.stdout, end="", flush=True)
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spawn", required=True, type=Path)
    args = parser.parse_args()
    root = Path.cwd() / "box64-proof"
    root.mkdir(exist_ok=True)
    os.environ["PLATFORMIO_CORE_DIR"] = str(root / "pio")
    os.environ["PLATFORMIO_SYSTEM_TYPE"] = "linux_x86_64"  # fixture only
    from platformio.package.manager.tool import ToolPackageManager
    manager = ToolPackageManager(str(root / "packages"))
    package = manager.install("platformio/toolchain-atmelavr@1.70300.191015", skip_dependencies=True)
    del os.environ["PLATFORMIO_SYSTEM_TYPE"]
    boxed.configure_policy("aarch64")
    entries = [{"name": "1.0.0", "files": [{"system": ["linux_aarch64"]}]},
               {"name": "2.0.0", "files": [{"system": ["linux_x86_64"]}]}]
    if manager.pick_best_registry_version(entries)["name"] != "1.0.0" or manager.pick_best_registry_version(entries[1:])["name"] != "2.0.0":
        raise ValueError("Real PlatformIO native-first/fallback policy failed")
    from platformio.package.meta import PackageSpec
    from unittest.mock import patch
    with patch.object(manager, "fetch_registry_package", return_value={"versions": entries}):
        _, selected = manager.find_best_registry_version(
            [{"id": 1, "owner": {"username": "synthetic"}, "name": "compiler"}], PackageSpec(name="compiler"))
        if selected["name"] != "1.0.0":
            raise ValueError("Owner-less registry lookup hid an older compatible ARM64 artifact")
    run([sys.executable, "-P", str(Path(__file__).with_name("platformio-policy-check.py")), "--x64-client", args.spawn])
    boxed.overlay(package.path)
    compiler = Path(package.path) / "bin/avr-gcc"
    record = json.loads((Path(package.path) / ".tabstudio-box64-overlay.json").read_text())
    original = Path(record["original"]) / "bin/avr-gcc"
    if not boxed.x86_executable(original):
        raise ValueError("The compiler fixture must actually be x86-64")
    from platformio.package.manager.platform import PlatformPackageManager
    from platformio.package.manager.core import get_core_package_dir
    platform_package = PlatformPackageManager().install("platformio/atmelavr@5.3.0", skip_dependencies=True)
    run([sys.executable, "-P", str(Path(__file__).with_name("platformio-child-policy-check.py")),
         "--compiler", original.parent.parent, "--platform", platform_package.path,
         "--scons", get_core_package_dir("tool-scons"),
         "--helper", Path(boxed.__file__).resolve(), "--work", root / "fresh-scons-policy"])
    source = root / "blink.c"
    source.write_text("#include <avr/io.h>\nint main(void){DDRB|=_BV(5);for(;;)PORTB^=_BV(5);}\n")
    firmware = root / "firmware.elf"
    output = run([compiler, "-v", "-Os", "-mmcu=atmega328p", source, "-o", firmware])
    # GCC -v must report real cc1/as/collect2, and a complete ELF must exist.
    if "cc1" not in output or not ("/as " in output or "avr-as" in output) or "collect2" not in output:
        raise ValueError("Original GCC subprogram lookup was not demonstrated")
    data = firmware.read_bytes()
    if data[:4] != b"\x7fELF" or data[18:20] != b"\x53\x00":
        raise ValueError("The translated compiler did not link an AVR ELF")
    spawn = args.spawn.absolute()
    spawn_package = root / "spawn-package"
    spawn_package.mkdir(exist_ok=True)
    import shutil
    shutil.copy2(spawn, spawn_package / "spawn")
    boxed.overlay(spawn_package)
    output = run([spawn_package / "spawn"])
    if "32 repetitions and ENOENT checked" not in output or "BOX64_CHILD_FD_PASSED" not in output:
        raise ValueError("The actual x86 child/FD regression did not complete")
    evidence = {"hardwareVerified": False, "architecture": platform.machine(),
                "translated": platform.machine() == "aarch64", "package": "platformio/toolchain-atmelavr@1.70300.191015",
                "compilerSha256": hashlib.sha256(original.read_bytes()).hexdigest(),
                "firmwareSha256": hashlib.sha256(data).hexdigest(), "childCount": 32, "inheritedFd": 3, "freshSConsPolicyVerified": True}
    (root / "summary.json").write_text(json.dumps(evidence, indent=2))
    print("BOX64_COMPILER_CHILD_FD_PASSED", json.dumps(evidence), flush=True)


if __name__ == "__main__":
    main()
