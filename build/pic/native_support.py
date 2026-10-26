#!/usr/bin/env python3
"""Build native PIC helpers in the same Alpine/PRoot environment as the app."""
import argparse
import json
from pathlib import Path
import platform
import shutil
import sys
import spike


def main(args):
    work, output = args.work.resolve(), args.output.resolve()
    work.mkdir(parents=True, exist_ok=True)
    output.mkdir(parents=True, exist_ok=True)
    result = {"hostArchitecture": platform.machine(), "tools": {}, "success": False, "hardwareTested": False}
    try:
        root, _, prefix, _, env = spike.prepare(work, output, "xc8")
        native = [*prefix, "/usr/bin/env", "-u", "LD_LIBRARY_PATH"]
        distribution = json.loads((spike.REPO / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())["linux"]
        (root / "etc/apk/repositories").write_text("\n".join(distribution["mirror"] + "/" + distribution["branch"] + "/" + r for r in ("main", "community")) + "\n")
        shutil.copyfile("/etc/resolv.conf", root / "etc/resolv.conf")
        (root / "etc/ssl/certs").mkdir(parents=True, exist_ok=True)
        shutil.copyfile("/etc/ssl/certs/ca-certificates.crt", root / "etc/ssl/certs/ca-certificates.crt")
        spike.run([*native, "/sbin/apk", "add", "--no-cache", "python3", "build-base", "libusb-dev", "hidapi-dev", "readline-dev", "popt-dev", "glib-dev", "bison", "flex", "pkgconf"], output / "dependencies.log", env=env)
        tools = root / "opt/tabstudio/microchip"
        tools.mkdir(parents=True, exist_ok=True)
        for source in (spike.REPO / "src/TabStudio.Toolchains/Microchip").iterdir():
            if source.suffix in (".py", ".json", ".c"):
                shutil.copy2(source, tools / source.name)
        shutil.copy2(spike.REPO / "build/pic/assets.lock.json", tools / "assets.lock.json")
        for tool in ("pic32prog", "pk2cmd", "gpsim"):
            arguments = [tool] + (["--accept-pk2cmd-license"] if tool == "pk2cmd" else [])
            _, seconds, _ = spike.run([*native, "python3", "/opt/tabstudio/microchip/programmers.py", *arguments], output / (tool + ".log"), env=env, timeout=1200)
            result["tools"][tool] = {"sourceBuildSeconds": seconds}
        for suffix in ("c", "hex", "elf"):
            shutil.copy2(args.firmware / ("simulation." + suffix), root / "work" / ("simulation." + suffix))
        shutil.copy2(spike.REPO / "build/pic/test_gpsim_integration.py", root / "work/test_gpsim_integration.py")
        spike.run([*native, "PYTHONPATH=/opt/tabstudio/microchip:/opt/tabstudio/microchip/python",
                   "PIC_TEST_GPSIM=/opt/tabstudio/microchip/native/gpsim/bin/gpsim", "PIC_TEST_ELF=/work/simulation.elf",
                   "python3", "/work/test_gpsim_integration.py"], output / "gpsim-dap.log", env=env, timeout=180)
        result["dapIntegration"] = True
        result["success"] = True
    except Exception as error:
        result["error"] = str(error)
        raise
    finally:
        (output / "native-support-result.json").write_text(json.dumps(result, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--firmware", type=Path, required=True)
    main(parser.parse_args())
