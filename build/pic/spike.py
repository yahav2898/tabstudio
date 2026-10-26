#!/usr/bin/env python3
"""Phase 0: official XC installers + DFPs in Alpine/PRoot, without binfmt or root.

Nothing proprietary is uploaded: the result directory contains logs, measurements,
and the firmware we compiled. The compiler/environment stay in --work.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import signal
import subprocess
import time
import zipfile

REPO = Path(__file__).resolve().parents[2]
LOCK = json.loads((REPO / "build/pic/assets.lock.json").read_text())["assets"]


def fetch(asset, cache, name):
    dest = cache / name
    dest.parent.mkdir(parents=True, exist_ok=True)
    if not dest.exists():
        subprocess.run(["curl", "-fLSs", "--retry", "3", "--connect-timeout", "30",
                        "--max-time", "1200", asset["url"], "-o", str(dest) + ".part"], check=True)
        Path(str(dest) + ".part").rename(dest)
    with dest.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if digest != asset["sha256"]:
        raise ValueError(f"SHA-256 mismatch for {name}: {digest}")
    return dest


def run(command, log, *, env=None, timeout=900, check=True):
    print("+", " ".join(map(str, command)), flush=True)
    start = time.monotonic()
    with log.open("w") as output:
        with subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT,
                              env=env, start_new_session=True) as process:
            try:
                code = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
                code = 124
    text = log.read_text(errors="replace")
    print(text[-6000:], flush=True)
    if check and code:
        raise RuntimeError(f"{log.name}: exit {code}")
    return code, time.monotonic() - start, text


def unpack_deb(package, root):
    member = next(name for name in subprocess.check_output(["ar", "t", package], text=True).splitlines()
                  if name.startswith("data.tar"))
    compression = {".xz": "J", ".gz": "z", ".zst": "--zstd"}.get(Path(member).suffix, "")
    flags = ["--zstd", "-xf"] if compression == "--zstd" else [f"-x{compression}f"]
    with subprocess.Popen(["ar", "p", package, member], stdout=subprocess.PIPE) as source:
        subprocess.run(["tar", "--no-same-owner", *flags, "-", "-C", root], stdin=source.stdout, check=True)
        source.stdout.close()
        if source.wait():
            raise RuntimeError(f"Cannot extract {package}")


def prepare(work, out, compiler):
    cache = work / "downloads"
    cache.mkdir(parents=True, exist_ok=True)
    arch = {"aarch64": "aarch64", "arm64": "aarch64", "x86_64": "x86_64"}[platform.machine()]
    catalog = json.loads((REPO / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())["linux"]
    image = catalog["rootfs"][arch]
    root = work / "alpine"
    root.mkdir(exist_ok=True)
    if not (root / "etc/alpine-release").exists():
        archive = fetch({**image, "url": f'{catalog["mirror"]}/{catalog["branch"]}/releases/{arch}/{image["file"]}'}, cache, image["file"])
        subprocess.run(["tar", "--no-same-owner", "-xzf", archive, "-C", root], check=True)
    for directory in ["dev", "proc", "sys", "tmp", "root", "work", "usr/local/bin", "lib64"]:
        (root / directory).mkdir(parents=True, exist_ok=True)
    for deb_arch in (["amd64", "arm64"] if arch == "aarch64" else ["amd64"]):
        for package in ["libc6", "libstdc++6", "libgcc-s1", "zlib1g"]:
            name = f"{package}-{deb_arch}"
            unpack_deb(fetch(LOCK[name], cache, name + ".deb"), root)
    # Debian's merged /usr layout alongside Alpine's musl: keep musl's loader intact.
    for directory in ["aarch64-linux-gnu", "x86_64-linux-gnu"]:
        link = root / "lib" / directory
        if not link.exists():
            link.symlink_to(f"../usr/lib/{directory}")
    loader = root / "lib/ld-linux-aarch64.so.1"
    if arch == "aarch64" and not loader.exists():
        loader.symlink_to("../usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1")
    loader = root / "lib64/ld-linux-x86-64.so.2"
    if not loader.exists():
        loader.symlink_to("../usr/lib/x86_64-linux-gnu/ld-linux-x86-64.so.2")
    if arch == "aarch64":
        unpack_deb(fetch(LOCK["box64-arm64"], cache, "box64.deb"), root)
    source = work / "proot-5.1.107.101"
    if not source.exists():
        with zipfile.ZipFile(fetch(LOCK["proot"], cache, "proot.zip")) as archive:
            archive.extractall(work)
            for entry in archive.infolist():
                mode = entry.external_attr >> 16
                if mode and not entry.is_dir():
                    (work / entry.filename).chmod(mode & 0o777)
    proot = source / "src/proot"
    if not proot.exists():
        env = {**os.environ, "PROOT_UNBUNDLE_LOADER": ".", "CFLAGS": "-O2",
               "CPPFLAGS": '-DVERSION=\\\"5.1.107.101\\\"'}
        # This spike runs only 64-bit tools. PRoot also tries an optional -m32 loader,
        # which GCC on ARM64 cannot build (the Android NDK build uses Clang).
        run(["make", "-C", str(source / "src"), "-j2", "HAS_LOADER_32BIT=", "proot", "loader/loader"],
            out / "proot-build.log", env=env)
    env = {**os.environ, "PROOT_LOADER": str(source / "src/loader/loader"),
           "PROOT_TMP_DIR": str(work / "tmp"), "PROOT_NO_SECCOMP": "1"}
    (work / "tmp").mkdir(exist_ok=True)
    native = "aarch64-linux-gnu" if arch == "aarch64" else "x86_64-linux-gnu"
    guest_env = ["/usr/bin/env", "-i", "HOME=/root", "USER=root", "TMPDIR=/tmp", "LANG=C",
                 "PATH=/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin",
                 f"LD_LIBRARY_PATH=/usr/lib/{native}",
                 "BOX64_LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu",
                 # Compiler/installer subprocesses parse each other's stdout. A box64
                 # banner is not compiler output and breaks those integrity checks.
                 "BOX64_LOG=0", "BOX64_NOBANNER=1", "BOX64_DYNAREC=1", "BOX64_DYNACACHE=0"]
    prefix = [str(proot), "--kill-on-exit", "--link2symlink", "--sysvipc", "-L", "-0",
              "-r", str(root), "-b", "/dev", "-b", "/proc", "-b", "/sys",
              "-b", f"{root / 'tmp'}:/dev/shm", "-b", f"{cache}:/downloads",
              "-b", f"{out}:/results", "-w", "/work", *guest_env]
    translate = ["/usr/bin/box64"] if arch == "aarch64" else []
    return root, cache, prefix, translate, env


def spike(args):
    work, out = args.work.resolve(), args.output.resolve()
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    result = {"compiler": args.compiler, "hostArchitecture": platform.machine(),
              "kernel": platform.release(), "runner": os.environ.get("RUNNER_NAME"),
              "cpu": subprocess.check_output(["lscpu"], text=True),
              "compileSeconds": [], "success": False}
    try:
        # An explicit acceptance is required; the IDE must display the license before setting this.
        asset = LOCK[args.compiler]
        print(f'Microchip XC compiler license: {asset["licenseUrl"]}', flush=True)
        print("Installed for this user under Microchip's license; never redistributed. No activation or license manager.", flush=True)
        if not args.accept_microchip_license:
            raise ValueError("Read the Microchip license and pass --accept-microchip-license to install.")
        root, cache, prefix, translate, env = prepare(work, out, args.compiler)
        result["box64Version"] = LOCK["box64-arm64"]["version"] if translate else None
        installer = fetch(asset, cache, args.compiler + ".run")
        installer.chmod(0o755)
        install_guest = "/downloads/" + installer.name
        _, _, help_text = run([*prefix, *translate, install_guest, "--help"], out / "installer-help.log", env=env)
        dest = f"/opt/microchip/{args.compiler}/v{asset['version']}"
        destination_option = "--prefix" if "--prefix" in help_text else "--installdir"
        command = [*prefix, *translate, install_guest, "--mode", "unattended", "--unattendedmodeui", "none",
                   destination_option, dest]
        # Old releases use FreeMode; new releases are license free and omit this option.
        if "--LicenseType" in help_text:
            command += ["--LicenseType", "FreeMode"]
        code, elapsed, _ = run(command, out / "installer.log", env=env, timeout=300, check=False)
        result.update(installSeconds=elapsed, installerExitCode=code, installMethod="installer")
        compiler = "xc8-cc" if args.compiler == "xc8" else "xc32-gcc"
        if code:
            # Microchip also publishes a byte-verified compressed distribution. The
            # installer's self-check must not prevent testing the compiler itself.
            archive = fetch(LOCK[args.compiler + "-archive"], cache, args.compiler + ".tar.xz")
            unpacked = root / "opt/tabstudio/archives" / args.compiler
            unpacked.mkdir(parents=True, exist_ok=True)
            run(["tar", "--no-same-owner", "-xJf", str(archive), "-C", str(unpacked)],
                out / "archive-extract.log", env=env)
            binaries = list(unpacked.rglob("bin/" + compiler))
            if len(binaries) != 1:
                raise RuntimeError(f"Expected one {compiler} in the official archive, found {len(binaries)}")
            dest = "/" + binaries[0].parent.parent.relative_to(root).as_posix()
            result["installMethod"] = "official archive (installer self-check failed)"
        program = f"{dest}/bin/{compiler}"
        result["compilerFile"] = subprocess.check_output(["file", str(root / program.lstrip("/"))], text=True).strip()
        code, _, version = run([*prefix, *translate, program, "--version"], out / "version.log", env=env, check=False, timeout=30)
        result["versionExitCode"] = code
        result["executionMode"] = "dynarec" if translate else "native"
        if code and translate:
            run([*prefix, "/usr/bin/env", "BOX64_LOG=2", "BOX64_NOBANNER=0", *translate, program, "--version"],
                out / "version-diagnostic.log", env=env, check=False, timeout=30)
            interpreted = ["/usr/bin/env", "BOX64_DYNAREC=0", *translate]
            code, _, version = run([*prefix, *interpreted, program, "--version"], out / "version-interpreter.log", env=env, check=False, timeout=60)
            result["interpreterVersionExitCode"] = code
            if not code:
                translate = interpreted
                result["executionMode"] = "interpreter"
            else:
                # A diagnostic control, not a substitute for success in Alpine/PRoot.
                native_lib = root / "usr/lib/aarch64-linux-gnu"
                diagnostic_env = {**os.environ, "BOX64_LD_LIBRARY_PATH": str(root / "usr/lib/x86_64-linux-gnu"),
                                  "BOX64_LOG": "2", "BOX64_NOBANNER": "0", "BOX64_DYNACACHE": "0"}
                control, _, _ = run([str(native_lib / "ld-linux-aarch64.so.1"), "--library-path", str(native_lib),
                                     str(root / "usr/bin/box64"), str(root / program.lstrip("/")), "--version"],
                                    out / "version-outside-proot-control.log", env=diagnostic_env, check=False, timeout=30)
                result["outsideProotVersionExitCode"] = control
        if code:
            raise RuntimeError(f"{compiler} --version failed (exit {code}); see version diagnostics")
        result["version"] = version
        family = "pic18" if args.compiler == "xc8" else "pic32"
        pack_dir = root / f"opt/tabstudio/packs/{family}"
        pack_dir.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(fetch(LOCK[family + "-dfp"], cache, family + ".atpack")) as archive:
            archive.extractall(pack_dir)
        source = REPO / "samples/PIC" / ("PIC18Blink" if family == "pic18" else "PIC32Blink") / "main.c"
        shutil.copy2(source, root / "work/main.c")
        pack = f"/opt/tabstudio/packs/{family}/xc8" if family == "pic18" else f"/opt/tabstudio/packs/{family}/xc32"
        device = "18F45K22" if family == "pic18" else "32MX250F128B"
        options = [f"-mcpu={device}", f"-mdfp={pack}", "-O0", "-o", "/results/blink.elf", "main.c"]
        if family == "pic32":
            options += ["-g", "-Wl,--defsym=_min_heap_size=0,--report-mem"]
        for i in range(3):
            _, elapsed, _ = run([*prefix, *translate, program, *options], out / f"compile-{i}.log", env=env, timeout=180)
            result["compileSeconds"].append(elapsed)
        if family == "pic32":
            run([*prefix, *translate, f"{dest}/bin/xc32-bin2hex", "/results/blink.elf"], out / "bin2hex.log", env=env)
        hex_files = list(out.glob("*.hex"))
        if not hex_files:
            raise RuntimeError("The compiler did not produce a HEX file")
        result["hexSha256"] = hashlib.sha256(hex_files[0].read_bytes()).hexdigest()
        result["success"] = True
    except Exception as error:
        result["error"] = str(error)
        raise
    finally:
        (out / "result.json").write_text(json.dumps(result, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", choices=["xc8", "xc32"], required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--accept-microchip-license", action="store_true")
    spike(parser.parse_args())
