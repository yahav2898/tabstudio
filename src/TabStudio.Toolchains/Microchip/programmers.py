"""Verified optional PIC tools, built natively inside Alpine when requested.

No proprietary device database or programmer firmware is distributed. pk2cmd
requires separate acceptance and a user-supplied PK2DeviceFile.dat. PICkit 3
must already have scripting firmware; changing that firmware needs Windows.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import zipfile

from install import ASSETS, TOOLS, download, ensure_pack


def run(command, cwd=None, **kwargs):
    environment = {k: v for k, v in os.environ.items() if k not in ("LD_LIBRARY_PATH", "LD_PRELOAD")}
    print("+ " + " ".join(map(str, command)), flush=True)
    subprocess.run(command, cwd=cwd, env=environment, check=True, timeout=1200, **kwargs)


def python_support():
    destination = TOOLS / "python"
    destination.mkdir(exist_ok=True)
    for name in ("pymcuprog", "pyedbglib", "pyserial", "intelhex", "appdirs", "pyelftools"):
        asset = ASSETS[name]
        marker = destination / ("." + name + "-" + asset["sha256"])
        if marker.exists():
            continue
        with zipfile.ZipFile(download(asset)) as package:
            for entry in package.infolist():
                if not (destination / entry.filename).resolve().is_relative_to(destination.resolve()):
                    raise ValueError("Python support archive escapes its destination")
            package.extractall(destination)
        marker.touch()
    asset = ASSETS["pyyaml"]
    marker = destination / (".pyyaml-" + asset["sha256"])
    if not marker.exists():
        with tarfile.open(download(asset), "r:gz") as archive:
            for member in archive.getmembers():
                if "/lib/yaml/" in member.name and member.isfile():
                    relative = member.name.split("/lib/", 1)[1]
                    target = destination / relative
                    if not target.resolve().is_relative_to(destination.resolve()):
                        raise ValueError("Invalid YAML package path")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(archive.extractfile(member).read())
                elif member.isfile() and member.name.endswith("/LICENSE"):
                    (destination / "PyYAML.LICENSE").write_bytes(archive.extractfile(member).read())
        marker.touch()
    return destination


def install_support(tool, accepted_pk2cmd=False):
    if tool == "pymcuprog":
        python_support()
        return
    if tool not in ("gpsim", "pic32prog", "pk2cmd"):
        raise ValueError("Unknown optional PIC tool")
    if tool == "pk2cmd" and not accepted_pk2cmd:
        raise ValueError("Read and accept pk2cmd's separate Microchip PICkit license before its source is downloaded")
    asset = ASSETS[tool + "-source"]
    destination = TOOLS / "native" / tool
    marker = destination / ".source-sha256"
    binary = destination / "bin" / tool
    if marker.exists() and marker.read_text().strip() == asset["sha256"] and binary.exists():
        return
    destination.mkdir(parents=True, exist_ok=True)
    source = TOOLS / "sources" / tool
    if source.exists():
        shutil.rmtree(source)
    source.mkdir(parents=True)
    with tarfile.open(download(asset), "r:*") as archive:
        archive.extractall(source, filter="data")
    source = next(source.iterdir())
    if tool == "gpsim":
        run([str(source / "configure"), "--disable-gui", "--prefix=" + str(destination),
             "CC=gcc -std=gnu17", "CXXFLAGS=-O2 -std=gnu++11"], source)
        run(["make", "-j2"], source)
        run(["make", "install"], source)
        python_support()
    elif tool == "pic32prog":
        # Upstream's hidapi submodule is replaced by Alpine's signed hidapi
        # package; link libusb dynamically so the explicit-fd view applies.
        include = source / "hidapi/hidapi/hidapi.h"
        include.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile("/usr/include/hidapi/hidapi.h", include)
        hid_library = "/usr/lib/libhidapi-libusb.so"
        if not Path(hid_library).is_file():
            raise ValueError("Install Alpine's hidapi-dev package with its libusb backend before building pic32prog")
        # The copied upstream header is newer than the installed library.
        # -o keeps make from running its submodule bootstrap rule on that
        # system library; it is already supplied by the signed Alpine package.
        run(["make", "-j2", "-o", hid_library, "GITCOUNT=0", "HIDLIB=" + hid_library,
             "LIBS=-lusb-1.0 -lpthread", 'CFLAGS=-O2 -std=gnu17 -fcommon -include sys/select.h -include sys/time.h -Ihidapi/hidapi -DUSE_MPSSE -DGITCOUNT=\\"0\\"'], source)
        binary.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / "pic32prog", binary)
    else:
        source = source / "pk2cmd"
        run(["make", "-j2", "TARGET=linux", "LIBUSB=/usr", "OPTS=-O2 -std=gnu++14 -D_GNU_SOURCE"], source)
        binary.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / "pk2cmd", binary)
    if tool != "gpsim":
        run(["cc", "-std=gnu17", "-O2", "-shared", "-fPIC", str(TOOLS / "usb_fd.c"), "-o", str(TOOLS / "libusb-fd.so"), "-lusb-1.0", "-ldl"])
    marker.write_text(asset["sha256"] + "\n")
    # Keep licenses and corresponding source next to the native tools.
    print(f"{tool} is ready ({asset['version']}); source and licenses retained in {source}", flush=True)


def program(profile, firmware):
    from bootloaders import hex_records
    hex_records(firmware)
    tool, device = profile["protocol"], profile["device"].upper()
    metadata = json.loads(os.environ["TABSTUDIO_USB_DEVICE"])
    if tool == "pymcuprog":
        supported = json.loads((TOOLS / "pymcuprog.devices.json").read_text())["devices"]
        if device not in supported:
            raise ValueError("pymcuprog has no device module for " + device + "; use the Windows Bridge")
        # PICkit 4 and Snap are supported by pymcuprog only in AVR mode. PICkit
        # 5 is not documented by this pinned version. PIC uses PKOB nano/nEDBG.
        if profile.get("tool") != "nedbg":
            raise ValueError("pymcuprog PIC programming requires PKOB nano (nedbg); for PICkit 4/5 or Snap use the Windows Bridge")
        libraries = python_support()
        sys.path.insert(0, str(libraries))
        import hid  # Our permissioned transport, already beside this module.
        sys.modules["hid"] = hid
        pack, directory = ensure_pack(device, tuple(profile["pack"]) if profile.get("pack") else None)
        scripts = directory / "scripts" / device.lower()
        if not scripts.is_dir():
            raise ValueError(f"{pack['name']} has no programming scripts for {device}; use the Windows Bridge")
        # Run erase, write and verify separately so every exit status is checked.
        environment = {**os.environ, "PYTHONPATH": str(TOOLS) + ":" + str(libraries)}
        common = ["-t", "nedbg", "-d", device.lower(), "-p", str(scripts)]
        for action in ("erase", "write", "verify"):
            command = [sys.executable, "-m", "pymcuprog", action, *common]
            if action != "erase": command += ["-f", str(firmware)]
            subprocess.run(command, env=environment, pass_fds=(3,), timeout=300, check=True)
        return
    if tool not in ("pk2cmd", "pic32prog"):
        raise ValueError("No compatible local programmer; use the Windows Bridge")
    if metadata["vendorId"] != 0x04d8 or metadata["productId"] not in (0x0033, 0x900a):
        raise ValueError("Select a PICkit 2 or PICkit 3 with scripting firmware")
    binary = TOOLS / "native" / tool / "bin" / tool
    if not binary.is_file() or not (TOOLS / "libusb-fd.so").is_file():
        raise ValueError("Install the optional " + tool + " support before flashing")
    if tool == "pk2cmd":
        database = Path(profile.get("deviceFile", ""))
        if database.name != "PK2DeviceFile.dat" or not database.is_file():
            raise ValueError("Supply your licensed PK2DeviceFile.dat; it is never bundled or downloaded by TabStudio")
        digest = hashlib.sha256(database.read_bytes()).hexdigest()
        if digest != profile.get("deviceFileSha256"):
            raise ValueError("PK2DeviceFile.dat does not match the SHA-256 recorded in the flash profile")
        command = [str(binary), "-B" + str(database.parent), "-P" + device.removeprefix("PIC"), "-F" + str(firmware), "-M", "-Y", "-W"]
    else:
        if not device.startswith("PIC32"):
            raise ValueError("pic32prog supports PIC32 devices; use pk2cmd or the Windows Bridge for 8-bit PIC")
        command = [str(binary), str(firmware)]
    environment = {**os.environ, "LD_PRELOAD": str(TOOLS / "libusb-fd.so")}
    subprocess.run(command, env=environment, pass_fds=(3,), check=True, timeout=600)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tool", choices=["gpsim", "pymcuprog", "pk2cmd", "pic32prog"])
    parser.add_argument("--accept-pk2cmd-license", action="store_true")
    args = parser.parse_args()
    try:
        install_support(args.tool, args.accept_pk2cmd_license)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print("PIC support: error: " + str(error), file=sys.stderr, flush=True)
        sys.exit(1)
