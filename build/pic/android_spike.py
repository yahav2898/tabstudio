#!/usr/bin/env python3
"""Build the PIC18 template under Android's app UID and APK-packaged PRoot.

The emulator is x86-64: this checks box64's portable interpreter on Android,
not ARM dynarec performance. Real ARM64 is checked by the separate runner jobs.
No compiler, device pack, environment archive or proprietary source is uploaded.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import zipfile

import spike

PACKAGE = "com.tabstudio.ide"


def prepare(args):
    if not args.accept_microchip_license:
        raise ValueError("Read Microchip's license and pass --accept-microchip-license")
    print("Microchip compiler license:", spike.LOCK["xc8"]["licenseUrl"], flush=True)
    work, out = args.work.resolve(), args.output.resolve()
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    root, cache, _, _, env = spike.prepare(work, out, "xc8", build_runtime=False)
    spike.build_current_box64(work, root, out, cache, fix_clone_stack=True)
    distribution = root / "opt/microchip"
    distribution.mkdir(parents=True, exist_ok=True)
    archive = spike.fetch(spike.LOCK["xc8-archive"], cache, "xc8.tar.xz")
    spike.run(["tar", "--no-same-owner", "-xJf", str(archive), "-C", str(distribution)], out / "android-extract.log")
    compiler = next(distribution.rglob("bin/xc8-cc"))
    pack = root / "opt/tabstudio/packs/pic18"
    pack.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(spike.fetch(spike.LOCK["pic18-dfp"], cache, "pic18.atpack")) as package:
        package.extractall(pack)
    shutil.copyfile(spike.REPO / "samples/PIC/PIC18Blink/main.c", root / "work/main.c")
    spike.run(["cc", "-static", "-O2", str(spike.REPO / "build/pic/static-spawn.c"), "-o", str(root / "work/static-spawn")], out / "android-guest-probe-build.log")
    (work / "compiler-path.txt").write_text("/" + compiler.relative_to(root).as_posix())
    spike.run(["tar", "-czf", str(work / "android-rootfs.tar.gz"), "-C", str(root), "."], out / "android-pack.log", timeout=300)


def adb_shell(command):
    return subprocess.check_output(["adb", "shell", command], text=True).strip().replace("\r", "")


def native_library_directory(dump, package_paths, abi):
    # Android versions print nativeLibraryDir or legacyNativeLibraryDir. APKs
    # with extractNativeLibs also have a stable lib/<instruction-set> sibling.
    instruction = {"x86_64": "x86_64", "arm64-v8a": "arm64", "armeabi-v7a": "arm", "x86": "x86"}.get(abi)
    native = re.search(r"(?:legacy)?nativeLibraryDir=([^\s]+)", dump, re.IGNORECASE)
    if native:
        return native[1] + "/" + instruction if native[1].endswith("/lib") and instruction else native[1]
    apk = next((line.removeprefix("package:") for line in package_paths.splitlines() if line.endswith("/base.apk")), None)
    if not apk or not instruction:
        raise RuntimeError("Cannot locate the installed APK's native library directory")
    return apk.rsplit("/", 1)[0] + "/lib/" + instruction


def guest_command(native, base, arguments):
    # env interprets a randomized APK ...==/lib executable path as another
    # assignment. POSIX shell assignments validate the variable name instead.
    variables = {"PROOT_LOADER": native + "/libproot-loader.so", "PROOT_TMP_DIR": base + "/tmp", "PROOT_NO_SECCOMP": "1"}
    return " ".join(key + "=" + shlex.quote(value) for key, value in variables.items()) + " " + shlex.join(arguments)


def run(args):
    work, out = args.work.resolve(), args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    result = {"emulatorAbi": adb_shell("getprop ro.product.cpu.abi"),
              "androidApi": adb_shell("getprop ro.build.version.sdk"),
              "box64Available": False, "linkedFirmware": False, "success": False}
    try:
        # run-as is enabled only in the CI emulator APK. No adbd root, shell-UID
        # PRoot or permissive SELinux substitute is used for the app UID check.
        home = adb_shell(shlex.join(["run-as", PACKAGE, "pwd"]))
        result["uid"] = adb_shell(shlex.join(["run-as", PACKAGE, "id"]))
        if not home.startswith("/data/") or "uid=0(" in result["uid"]:
            raise RuntimeError("The Android check must run as TabStudio's app UID")
        dump = adb_shell("dumpsys package " + PACKAGE)
        (out / "android-package.txt").write_text(dump)
        native = native_library_directory(dump, adb_shell("pm path " + PACKAGE), result["emulatorAbi"])
        result["nativeLibraryDirectory"] = native
        for library in ("libproot.so", "libproot-loader.so"):
            # Android's test is a shell builtin, not an executable for run-as.
            adb_shell(shlex.join(["run-as", PACKAGE, "sh", "-c", "test -r " + shlex.quote(native + "/" + library)]))
        base = home + "/files/pic-phase0"
        # Android 10's toybox tar otherwise tries to restore the runner's UID
        # on every file, which an unprivileged app correctly cannot do. -o is
        # its --no-same-owner option: keep all files owned by this app UID.
        setup = f"mkdir -p {shlex.quote(base)}/rootfs {shlex.quote(base)}/results {shlex.quote(base)}/tmp && cd {shlex.quote(base)}/rootfs && tar -xozf -"
        # Stream into the app UID. SELinux need not permit the app to read a
        # shell-owned file from /data/local/tmp, even when its Unix mode is 0644.
        with (work / "android-rootfs.tar.gz").open("rb") as archive:
            subprocess.run(["adb", "shell", "-T", shlex.join(["run-as", PACKAGE, "sh", "-c", setup])], stdin=archive, check=True)
        root = base + "/rootfs"
        prefix = [native + "/libproot.so", "--kill-on-exit", "--link2symlink", "--sysvipc", "-L", "-0", "-r", root,
                  "-b", "/dev", "-b", "/proc", "-b", "/sys", "-b", root + "/tmp:/dev/shm", "-b", base + "/results:/results", "-w", "/work",
                  "/usr/bin/env", "-i", "HOME=/root", "TMPDIR=/tmp", "PATH=/usr/bin:/bin", "LANG=C",
                  "LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu", "BOX64_LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu",
                  "BOX64_DYNAREC=0", "BOX64_AVX=0", "BOX64_LOG=0", "BOX64_NOBANNER=1", "BOX64_DYNACACHE=0", "/usr/bin/box64"]
        def command(arguments, boxed=True):
            inner = guest_command(native, base, [*(prefix if boxed else prefix[:-1]), *arguments])
            return ["adb", "shell", shlex.join(["run-as", PACKAGE, "sh", "-c", inner])]
        # Infrastructure failures are hard errors, never an unsupported-Box64
        # skip. This must execute Alpine as the app UID before the Box64 probe.
        spike.run(command(["/bin/true"], boxed=False), out / "android-proot-native-probe.log", timeout=30)
        result["prootNativeProbe"] = True
        code, _, _ = spike.run(command(["-v"]), out / "android-box64-probe.log", timeout=30, check=False)
        if not code:
            code, _, _ = spike.run(command(["/work/static-spawn"]), out / "android-box64-guest-probe.log", timeout=30, check=False)
        result["box64Available"] = code == 0
        if code:
            result["conditionalSkip"] = "box64 cannot execute on this emulator ABI; see Android probe log. ARM64 runner proof is separate."
            return
        compiler = (work / "compiler-path.txt").read_text()
        spike.run(command([compiler, "--version"]), out / "android-xc8-version.log", timeout=60)
        _, seconds, _ = spike.run(command([compiler, "-mcpu=18F14K22", "-mdfp=/opt/tabstudio/packs/pic18/xc8", "-O0", "-o", "/results/blink.elf", "main.c"]), out / "android-xc8-build.log", timeout=300)
        firmware = subprocess.check_output(["adb", "exec-out", shlex.join(["run-as", PACKAGE, "cat", base + "/results/blink.hex"])])
        (out / "android-blink.hex").write_bytes(firmware)
        result.update(success=True, linkedFirmware=True, compileSeconds=seconds, hexSha256=hashlib.sha256(firmware).hexdigest())
    except Exception as error:
        result["error"] = str(error)
        raise
    finally:
        (out / "android-result.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)
        if "base" in locals():
            # This is only the disposable compiler check's private directory.
            # Release space before the IDE's larger, independent self test.
            adb_shell(shlex.join(["run-as", PACKAGE, "rm", "-r", base]))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=["prepare", "run"])
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--accept-microchip-license", action="store_true")
    args = parser.parse_args()
    (prepare if args.stage == "prepare" else run)(args)
