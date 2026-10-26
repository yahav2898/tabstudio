#!/usr/bin/env python3
"""User-owned XC installations inside TabStudio's existing Alpine/PRoot rootfs.

Every executable download is pinned. Microchip binaries are downloaded directly
from Microchip after consent, kept in private app storage, and never bundled.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import platform
import shutil
import signal
import subprocess
import tarfile
import time
import urllib.error
import urllib.request
import zipfile

TOOLS = Path(__file__).resolve().parent
ASSETS = json.loads((TOOLS / "assets.lock.json").read_text())["assets"]
PACKS = json.loads((TOOLS / "packs.lock.json").read_text())["packs"]
CACHE = TOOLS / "downloads"


def download(asset):
    """Resume downloads, then verify all bytes before installation or execution."""
    CACHE.mkdir(parents=True, exist_ok=True)
    target = CACHE / asset["url"].rsplit("/", 1)[1]
    if not target.exists():
        partial = target.with_name(target.name + ".part")
        offset = partial.stat().st_size if partial.exists() else 0
        request = urllib.request.Request(asset["url"], headers={"User-Agent": "TabStudio-PIC/1", **({"Range": f"bytes={offset}-"} if offset else {})})
        try:
            response = urllib.request.urlopen(request, timeout=120)
        except urllib.error.HTTPError as error:
            if error.code != 416 or not offset:
                raise
            with partial.open("rb") as source:
                complete = hashlib.file_digest(source, "sha256").hexdigest() == asset["sha256"]
            if complete:
                partial.rename(target)
                return target
            partial.unlink()
            return download(asset)
        with response:
            resumed = offset > 0 and response.status == 206
            if not resumed:
                offset = 0
            total = int(response.headers.get("Content-Length", 0)) + offset
            with partial.open("ab" if resumed else "wb") as output:
                previous = time.monotonic()
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
                    offset += len(chunk)
                    if time.monotonic() - previous >= 1:
                        print(f"Downloading {target.name}: {offset} / {total} bytes", flush=True)
                        previous = time.monotonic()
        partial.rename(target)
    with target.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    if actual != asset["sha256"]:
        target.unlink()
        raise ValueError(f"SHA-256 mismatch for {target.name}; the file was not installed")
    return target


def extract_deb(package, root="/"):
    """Only the ar data member is installed. Python handles gzip/xz itself."""
    with package.open("rb") as source:
        if source.read(8) != b"!<arch>\n":
            raise ValueError("Invalid Debian archive")
        while header := source.read(60):
            if len(header) != 60 or header[-2:] != b"`\n":
                raise ValueError("Invalid Debian member")
            name = header[:16].decode("ascii").strip().rstrip("/")
            size = int(header[48:58])
            data = source.read(size)
            if len(data) != size:
                raise ValueError("Truncated Debian member")
            if size % 2:
                source.read(1)
            if name.startswith("data.tar"):
                with tarfile.open(fileobj=io.BytesIO(data), mode="r:*") as archive:
                    archive.extractall(root, filter="data")
                return
    raise ValueError("The Debian archive contains no data member")


def link(path, target):
    path = Path(path)
    if not path.exists() and not path.is_symlink():
        path.symlink_to(target)


def runtime_environment(compiler=None):
    env = dict(os.environ)
    native = "aarch64-linux-gnu" if platform.machine() == "aarch64" else "x86_64-linux-gnu"
    env.update(LD_LIBRARY_PATH="/usr/lib/" + native, BOX64_LD_LIBRARY_PATH="/usr/lib/x86_64-linux-gnu",
               BOX64_NOBANNER="1", BOX64_LOG="0", BOX64_DYNAREC="0" if compiler == "xc32" else "1", BOX64_DYNACACHE="0")
    return env


def translated(command):
    return [str(TOOLS / "box64"), *command] if platform.machine() == "aarch64" else command


def execute(command, timeout, compiler=None):
    print("Running", " ".join(command), flush=True)
    with subprocess.Popen(command, env=runtime_environment(compiler), start_new_session=True,
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True) as process:
        try:
            output, _ = process.communicate(timeout=timeout)
            code = process.returncode
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                output, _ = process.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                output, _ = process.communicate()
            code = 124
    print(output[-6000:], flush=True)
    return code, output


def ensure_pack(device, requested=None):
    candidates = [p for p in PACKS if device.upper() in {d.upper() for d in p["devices"]}
                  and (requested is None or (p["vendor"], p["name"], p["version"]) == requested)]
    if not candidates:
        requested_text = ".".join(requested) if requested else device
        raise LookupError(f"No verified DFP for {requested_text}. Select a pinned pack version supported by this TabStudio release; no different device/version was substituted.")
    pack = max(candidates, key=lambda p: tuple(map(int, p["version"].split("."))))
    destination = TOOLS / "packs" / pack["vendor"] / pack["name"] / pack["version"]
    marker = destination / ".tabstudio-sha256"
    if not marker.exists() or marker.read_text().strip() != pack["sha256"]:
        archive = download(pack)
        pending = destination.with_name(destination.name + ".partial")
        if pending.exists():
            shutil.rmtree(pending)
        pending.mkdir(parents=True)
        with zipfile.ZipFile(archive) as package:
            for entry in package.infolist():
                path = (pending / entry.filename).resolve()
                if not path.is_relative_to(pending.resolve()):
                    raise ValueError("Device pack path leaves its installation directory")
            package.extractall(pending)
        (pending / ".tabstudio-sha256").write_text(pack["sha256"] + "\n")
        if destination.exists():
            shutil.rmtree(destination)
        pending.rename(destination)
        archive.unlink()
    return pack, destination


def write_wrappers(compiler, destination):
    wrappers = Path("/usr/local/bin")
    wrappers.mkdir(parents=True, exist_ok=True)
    import shlex
    binaries = [*(destination / "bin").glob(compiler + "-*")]
    if compiler == "xc8":
        binaries += [destination / "bin/pic-as", destination / "bin/pic-ar"]
    for binary in binaries:
        if not binary.is_file() or not os.access(binary, os.X_OK):
            continue
        lines = ["#!/bin/sh"]
        for key, value in runtime_environment(compiler).items():
            if key.startswith("BOX64_") or key == "LD_LIBRARY_PATH":
                lines.append("export " + key + "=" + shlex.quote(value))
        lines.append("exec " + shlex.join(translated([str(binary)])) + ' "$@"')
        wrapper = wrappers / binary.name
        wrapper.write_text("\n".join(lines) + "\n")
        wrapper.chmod(0o755)


def install(compiler, accepted):
    if not accepted:
        raise ValueError("Accept Microchip's software license before downloading an XC compiler")
    arch = platform.machine()
    if arch not in ("aarch64", "x86_64"):
        raise ValueError("The official XC compilers require 64-bit Android")
    asset = ASSETS[compiler]
    previous = TOOLS / (compiler + ".json")
    if previous.is_file():
        try:
            manifest = json.loads(previous.read_text())
            destination = Path(manifest["compilerRoot"])
            binary = destination / "bin" / ("xc8-cc" if compiler == "xc8" else "xc32-gcc")
            if manifest.get("installerSha256") == asset["sha256"] and binary.is_file():
                code, _ = execute(translated([str(binary), "--version"]), 60, compiler)
                if code == 0:
                    write_wrappers(compiler, destination)
                    print(compiler.upper() + " already matches the verified distribution; support scripts were refreshed.", flush=True)
                    return
        except (OSError, ValueError, KeyError):
            pass
    print("Installing under Microchip's license; no license manager activation. Compiler binaries are never redistributed.", flush=True)
    for deb_arch in (["amd64", "arm64"] if arch == "aarch64" else ["amd64"]):
        for package in ["libc6", "libstdc++6", "libgcc-s1", "zlib1g"]:
            extract_deb(download(ASSETS[package + "-" + deb_arch]))
    for directory in ["aarch64-linux-gnu", "x86_64-linux-gnu"]:
        link("/lib/" + directory, "../usr/lib/" + directory)
    Path("/lib64").mkdir(exist_ok=True)
    link("/lib64/ld-linux-x86-64.so.2", "../usr/lib/x86_64-linux-gnu/ld-linux-x86-64.so.2")
    if arch == "aarch64":
        link("/lib/ld-linux-aarch64.so.1", "../usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1")
        if not (TOOLS / "box64").exists():
            raise ValueError("This APK has no box64 translator")
    installer = download(asset)
    installer.chmod(0o755)
    code, help_text = execute(translated([str(installer), "--help"]), 60, compiler)
    if code:
        raise RuntimeError("The verified official installer could not start")
    destination = Path("/opt/microchip") / compiler / ("v" + asset["version"])
    command = [str(installer), "--mode", "unattended", "--unattendedmodeui", "none",
               "--prefix" if "--prefix" in help_text else "--installdir", str(destination)]
    if "--LicenseType" in help_text:
        command += ["--LicenseType", "FreeMode"]
    code, _ = execute(translated(command), 300 if compiler == "xc8" else 180, compiler)
    method = "official installer"
    if code:
        print(f"Installer exited {code}; extracting Microchip's independently verified official distribution", flush=True)
        archive = download(ASSETS[compiler + "-archive"])
        final = Path("/opt/microchip") / compiler / ("v" + asset["version"] + "-archive-" + asset["sha256"][:12])
        pending = final.with_name(final.name + ".partial")
        if pending.exists():
            shutil.rmtree(pending)
        pending.mkdir(parents=True)
        with tarfile.open(archive, "r:xz") as package:
            package.extractall(pending, filter="data")
        command_name = "xc8-cc" if compiler == "xc8" else "xc32-gcc"
        matches = list(pending.rglob("bin/" + command_name))
        if len(matches) != 1:
            raise ValueError("The official distribution has an unexpected compiler layout")
        relative = matches[0].parent.parent.relative_to(pending)
        if final.exists():
            raise ValueError("A previous installation directory already exists; keep it intact and remove the broken installation through the Store before retrying")
        pending.rename(final)
        destination = final / relative
        method = "verified official archive"
        archive.unlink()
    binary = destination / "bin" / ("xc8-cc" if compiler == "xc8" else "xc32-gcc")
    code, version = execute(translated([str(binary), "--version"]), 60, compiler)
    if code:
        raise RuntimeError("Installed compiler failed its version check")
    write_wrappers(compiler, destination)
    manifest = {"version": asset["version"], "compilerRoot": str(destination), "installerSha256": asset["sha256"],
                "installMethod": method, "versionOutput": version, "installedAt": time.time(),
                "box64ExecutionMode": "interpreter" if compiler == "xc32" else "dynarec"}
    pending_manifest = TOOLS / (compiler + ".json.partial")
    pending_manifest.write_text(json.dumps(manifest, indent=2) + "\n")
    pending_manifest.replace(TOOLS / (compiler + ".json"))
    installer.unlink()
    print(f"{compiler.upper()} {asset['version']} is installed; device packs are verified and installed per selected device.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", choices=["xc8", "xc32"], required=True)
    parser.add_argument("--accept-microchip-license", action="store_true")
    args = parser.parse_args()
    install(args.compiler, args.accept_microchip_license)
