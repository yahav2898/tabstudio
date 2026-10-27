#!/usr/bin/env python3
"""Exercise product flash-client installers on the native CI architecture.

Reads the same catalog SHA-256 pins and executes the same helper source as the
Toolchain Store. Version/capability and dynamic-USB linkage are checked without
opening a device. This proves installation, not physical flashing.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import subprocess
import tarfile
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
CLIENTS = {
    "embedded-esptool": ("esptool", "binary"),
    "embedded-picotool": ("picotool", "binary"),
    "embedded-bossac": ("bossac", "source"),
    "embedded-teensy": ("teensy_loader_cli", "source"),
    "embedded-wchisp": ("wchisp", "source"),
    "embedded-mspdebug": ("mspdebug", "source"),
}


def load_helper(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "native/embedded" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def download(definition, cache):
    target = cache / (definition["sha256"] + ".tar.gz")
    if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest() == definition["sha256"]:
        return target
    temporary = target.with_suffix(".partial")
    digest = hashlib.sha256()
    try:
        request = urllib.request.Request(definition["url"], headers={"User-Agent": "TabStudio-native-client-CI"})
        with urllib.request.urlopen(request, timeout=120) as response, temporary.open("wb") as output:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
                digest.update(chunk)
        if digest.hexdigest() != definition["sha256"]:
            raise ValueError("Official archive SHA-256 mismatch: " + definition["url"])
        os.replace(temporary, target)
        return target
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("binary", "source", "all"), default="all")
    parser.add_argument("--tool", action="append", choices=CLIENTS)
    parser.add_argument("--cache", type=Path, default=Path(tempfile.gettempdir()) / "tabstudio-flash-client-cache")
    arguments = parser.parse_args()
    arguments.cache.mkdir(parents=True, exist_ok=True)
    architecture = {"arm64": "aarch64", "AMD64": "x86_64"}.get(platform.machine(), platform.machine())
    catalog = json.loads((ROOT / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())
    definitions = {tool["id"]: tool for tool in catalog["toolchains"]}
    helpers = {"binary": load_helper("install_native_client"), "source": load_helper("install_source_client")}
    for identifier in arguments.tool or CLIENTS:
        tool, mode = CLIENTS[identifier]
        if arguments.mode not in (mode, "all"):
            continue
        definition = definitions[identifier]
        archives = [step["archive"] for step in definition["setup"] if "archive" in step and (not step.get("architectures") or architecture in step["architectures"])]
        if len(archives) != 1:
            raise ValueError("Expected one native archive for " + identifier + "/" + architecture)
        archive_path = download(archives[0], arguments.cache)
        with tempfile.TemporaryDirectory(prefix="tabstudio-" + tool + "-") as directory:
            directory = Path(directory)
            extracted = directory / "upstream"
            extracted.mkdir()
            with tarfile.open(archive_path, "r:gz") as archive:
                archive.extractall(extracted, filter="data")
            helpers[mode].install(extracted, tool, definition["pinnedVersion"], directory / "bin")
            if mode == "source":
                recipe = helpers[mode].RECIPES[tool]
                command = [str(directory / "bin" / tool), *recipe["check"]]
                result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60, check=False)
                if result.returncode != recipe["exit"] or recipe["signature"] not in result.stdout:
                    raise ValueError("Installed PATH wrapper failed for " + tool)
                if tool in ("wchisp", "mspdebug", "teensy_loader_cli"):
                    linked = subprocess.run(["ldd", str(extracted / "bin" / tool)], check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True).stdout
                    required = "libusb-1.0" if tool == "wchisp" else "libusb-0.1"
                    if required not in linked:
                        raise ValueError("Native USB client is not dynamically linked to " + required)
                    print(linked, flush=True)
                if tool == "mspdebug":
                    firmware = directory / "simulation.hex"
                    different = directory / "mismatch.hex"
                    firmware.write_text(":04C000000102030432\n:00000001FF\n", encoding="ascii")
                    different.write_text(":04C000000102030531\n:00000001FF\n", encoding="ascii")
                    subprocess.run([str(directory / "bin" / tool), "-n", "sim", 'prog "' + str(firmware) + '"', 'verify "' + str(firmware) + '"'], check=True, timeout=30)
                    mismatch = subprocess.run([str(directory / "bin" / tool), "-n", "sim", 'prog "' + str(firmware) + '"', 'verify "' + str(different) + '"'], check=False, timeout=30)
                    if mismatch.returncode == 0:
                        raise ValueError("mspdebug accepted mismatching application flash in its real simulator")
            else:
                subprocess.run([str(directory / "bin" / tool), "version"], check=True, timeout=60)
                if tool == "picotool":
                    executable = helpers[mode].locate(extracted, tool)
                    linked = subprocess.run(["ldd", str(executable)], check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True).stdout
                    if "libusb-1.0" not in linked:
                        raise ValueError("Official picotool is not dynamically linked to libusb-1.0")
                    print(linked, flush=True)
            print("TABSTUDIO_FLASH_CLIENT_OK " + identifier + " " + architecture + " " + definition["pinnedVersion"], flush=True)


if __name__ == "__main__":
    main()
