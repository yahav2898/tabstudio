#!/usr/bin/env python3
"""Unchanged official wlink/nusb through production PRoot, against a synthetic tracer-only USB backend.

No physical USB device, hardware capture or physical flash success is represented by this fixture.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import tarfile
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
ARCHIVE_URL = "https://github.com/ch32-rs/wlink/releases/download/v0.1.3/wlink-v0.1.3-linux-x64.tar.gz"
ARCHIVE_SHA = "b0d55a32adf55214e7b6bf29b5eb45c61a13252622e2be3bf5dba7573e4359d8"
BINARY_SHA = "3e6d96c2dfffc56c839e90e507245a698b5264387d073d6e517a31dc1002d13a"
SOURCE_URL = "https://raw.githubusercontent.com/ch32-rs/wlink/7ed4b99321fb8e88eb05f7553211b39d41196a6c/src/flash_op.rs"
SOURCE_SHA = "e0376cbec3a39ccba787210a8fae8d78f7d5ff05f2eb76095903cb220761cdbf"
LOADER_SHA = "9d65b0efbfb7eabf44db6f0b54d26a3578f4094bbadaf67993872025ee5b149f"
WIRE_SHA = "c80c9f055ece8b0a16ebce1c8f2381bcb44c45a7708681de43ca9acb958a528b"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def download(url: str, digest: str, target: Path, limit: int) -> bytes:
    if target.exists() and sha(target) == digest:
        return target.read_bytes()
    request = urllib.request.Request(url, headers={"User-Agent": "TabStudio-WCH-Synthetic-Protocol-Check"})
    with urllib.request.urlopen(request, timeout=90) as response:
        data = response.read(limit + 1)
    if len(data) > limit or hashlib.sha256(data).hexdigest() != digest:
        raise RuntimeError("Pinned upstream artifact hash/size mismatch: " + url)
    target.write_bytes(data)
    return data


def build(work: Path) -> tuple[Path, Path, Path]:
    backend = work / "kernel-backend.so"
    raw = work / "raw-urb-client"
    unit = work / "backend-unit-client"
    subprocess.run(["cc", "-shared", "-fPIC", "-std=c17", "-Wall", "-Wextra", "-Werror", str(HERE / "kernel_backend.c"), "-ldl", "-pthread", "-o", str(backend)], check=True)
    for target, flags in [(raw, ["-static"]), (unit, ["-DNATIVE_BACKEND_UNIT"])]:
        subprocess.run(["cc", *flags, "-std=c17", "-Wall", "-Wextra", "-Werror", str(HERE / "urb_client.c"), "-o", str(target)], check=True)
    return backend, raw, unit


def backend_unit(backend: Path, unit: Path, output: Path) -> None:
    for mode in ["queue-cancel", "unprotect"]:
        left, right = socket.socketpair()
        try:
            right.shutdown(socket.SHUT_WR)
            descriptor = left.fileno()
            log = output / ("backend-" + mode + "-" + uuid.uuid4().hex + ".log")
            environment = {**os.environ, "LD_PRELOAD": str(backend), "TABSTUDIO_TEST_WCH_LOG": str(log)}
            def inherit():
                os.dup2(descriptor, 3)
            result = subprocess.run([str(unit), mode], env=environment, pass_fds=(descriptor,), preexec_fn=inherit, capture_output=True, text=True, timeout=10)
            print(result.stdout, end="")
            if result.returncode:
                raise RuntimeError("Synthetic native backend contract failed: " + result.stderr)
            text = log.read_text()
            if ("VIOLATION protection write" in text) != (mode == "unprotect"):
                raise RuntimeError("The backend's independent unsafe-wire oracle did not behave as expected")
            if mode == "queue-cancel" and "WCH_RAW_QUEUED_CANCEL_REAP_PASSED" not in result.stdout:
                raise RuntimeError("Missing native queued/cancel proof marker")
        finally:
            left.close()
            right.close()
    print("WCH_SYNTHETIC_BACKEND_QUEUE_CANCEL_AND_ORACLE_PASSED", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--proot", type=Path, required=True)
    parser.add_argument("--wlink", type=Path)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--backend-unit-only", action="store_true")
    args = parser.parse_args()
    work, output = args.work.resolve(), args.output.resolve()
    work.mkdir(parents=True, exist_ok=True)
    output.mkdir(parents=True, exist_ok=True)
    backend, raw, unit = build(work)
    backend_unit(backend, unit, output)
    if args.backend_unit_only:
        return
    if args.wlink is not None:
        wlink = args.wlink.resolve()
    else:
        archive = work / "wlink-v0.1.3-linux-x64.tar.gz"
        download(ARCHIVE_URL, ARCHIVE_SHA, archive, 8 * 1024 * 1024)
        wlink = work / "official-wlink"
        with tarfile.open(archive, "r:gz") as package:
            member = package.getmember("wlink-linux-x64/wlink")
            if not member.isfile() or member.size > 20 * 1024 * 1024:
                raise RuntimeError("Unexpected official binary archive member")
            with package.extractfile(member) as data:
                wlink.write_bytes(data.read())
        wlink.chmod(0o755)
    if sha(wlink) != BINARY_SHA:
        raise RuntimeError("The provided wlink is not the verified, unchanged official x64 executable")
    source = download(SOURCE_URL, SOURCE_SHA, work / "upstream-flash_op.rs", 1024 * 1024).decode("utf-8")
    match = re.search(r"pub const CH32V003: \[u8; 498\] = \[(.*?)\];", source, re.S)
    if not match:
        raise RuntimeError("The pinned upstream loader declaration changed")
    content = re.sub(r"//[^\n]*|/\*.*?\*/", "", match.group(1), flags=re.S)
    loader = bytes(int(value, 16) for value in re.findall(r"0x([0-9a-fA-F]{1,2})\b", content))
    if len(loader) != 498 or hashlib.sha256(loader).hexdigest() != LOADER_SHA:
        raise RuntimeError("The primary-source V003 loader changed")
    wire = loader + b"\xff" * 14
    if hashlib.sha256(wire).hexdigest() != WIRE_SHA:
        raise RuntimeError("Invalid canonical loader padding")
    loader_path = work / "upstream-v003-loader-padded.bin"
    loader_path.write_bytes(wire)
    artifacts = work / "dotnet-artifacts"
    environment = {**os.environ, "DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER": "1", "MSBUILDDISABLENODEREUSE": "1"}
    subprocess.run([args.dotnet, "build", str(HERE), "--artifacts-path", str(artifacts), "-m:1", "-p:UseSharedCompilation=false", "-warnaserror"], cwd=ROOT, env=environment, check=True, timeout=300)
    dll = artifacts / "bin/wch-link-check/debug/wch-link-check.dll"
    for acceleration in ["enabled", "disabled"]:
        mode_environment = dict(environment)
        if acceleration == "disabled":
            mode_environment["PROOT_NO_SECCOMP"] = "1"
        else:
            mode_environment.pop("PROOT_NO_SECCOMP", None)
        subprocess.run([args.dotnet, str(dll), str(args.proot.resolve()), str(wlink), str(backend), str(raw), str(loader_path), str(output / acceleration)], cwd=ROOT, env=mode_environment, check=True, timeout=300)
    (output / "pins.json").write_text(json.dumps(dict(archiveUrl=ARCHIVE_URL, archiveSha256=ARCHIVE_SHA, binarySha256=BINARY_SHA, sourceUrl=SOURCE_URL, sourceSha256=SOURCE_SHA, loaderWireSha256=WIRE_SHA, hardwareVerified=False, androidVerified=False), indent=2) + "\n")


if __name__ == "__main__":
    main()
