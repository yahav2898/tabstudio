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


def build_current_box64(work, root, out, cache, *, fix_clone_stack=False, target_arch=None, compiler=None):
    """Use upstream's recommended PRoot/Android signal handling, without changing XC."""
    archive = fetch(LOCK["box64-source"], cache, "box64-source.tar.gz")
    source_parent = work / "box64-source"
    source_parent.mkdir(exist_ok=True)
    run(["tar", "--no-same-owner", "-xzf", str(archive), "-C", str(source_parent)], out / "box64-extract.log")
    source = next(source_parent.iterdir())
    if fix_clone_stack:
        # A raw clone(CLONE_VM|CLONE_VFORK|SIGCHLD, child_stack, ...) is
        # posix_spawn, not the stack-less vfork syscall. Use the existing clone
        # implementation, which sets up the child's emulated SP and entry point.
        syscall = source / "src/emu/x64syscall.c"
        text = syscall.read_text()
        old = "if((R_EDI&~0xff)==0x4100) {"
        if text.count(old) != 1:
            raise RuntimeError("Pinned box64 clone implementation changed")
        syscall.write_text(text.replace(old, "if((R_EDI&~0xff)==0x4100 && !R_RSI) {"))
    build = work / "box64-build"
    native_arm = (target_arch or platform.machine()) == "aarch64"
    cross = (["-DCMAKE_SYSTEM_NAME=Linux", "-DCMAKE_SYSTEM_PROCESSOR=" + target_arch,
              "-DCMAKE_C_COMPILER=" + compiler, "-DCMAKE_ASM_COMPILER=" + compiler] if compiler else [])
    run(["cmake", "-S", str(source), "-B", str(build), "-DARM_DYNAREC=" + ("ON" if native_arm else "OFF"),
         "-DLD80BITS=" + ("OFF" if native_arm else "ON"), "-DNOALIGN=" + ("OFF" if native_arm else "ON"), "-DBAD_SIGNAL=ON",
         "-DCMAKE_BUILD_TYPE=Release", "-DNOGIT=1", "-DCMAKE_EXE_LINKER_FLAGS=-Wl,-z,max-page-size=16384", *cross], out / "box64-configure.log")
    run(["cmake", "--build", str(build), "--parallel", "4"], out / "box64-build.log", timeout=900)
    shutil.copy2(build / "box64", root / "usr/bin/box64")


def verify_clone_fix(work, root, out, cache, prefix, env):
    """Experimental translator change; require a static posix_spawn regression."""
    build_current_box64(work, root, out, cache, fix_clone_stack=True)
    source = REPO / "build/pic/static-spawn.c"
    binary = root / "work/static-spawn"
    run(["x86_64-linux-gnu-gcc", "-static", "-O2", str(source), "-o", str(binary)], out / "spawn-build.log")
    run([*prefix, "/usr/bin/box64", "/work/static-spawn"], out / "spawn-fixed-box64.log", env=env, timeout=30)


def prepare(work, out, compiler, *, build_runtime=True):
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
    if not build_runtime:
        return root, cache, [], [], dict(os.environ)
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
                 "BOX64_LOG=0", "BOX64_NOBANNER=1", "BOX64_DYNAREC=" + ("0" if compiler == "xc32" else "1"),
                 "BOX64_AVX=" + ("0" if compiler == "xc32" else "2"), "BOX64_DYNACACHE=0"]
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
        if translate:
            # Phase 0 established that unmodified releases lose posix_spawn's
            # child stack. Start with the measured working build, including for
            # the installer, instead of repeating the known failing path.
            verify_clone_fix(work, root, out, cache, prefix, env)
            result["box64Version"] = LOCK["box64-source"]["version"] + " (child-stack fix, BAD_SIGNAL=ON)"
            result["box64CloneStackFix"] = True
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
        code, elapsed, _ = run(command, out / "installer.log", env=env, timeout=60 if args.compiler == "xc32" else 300, check=False)
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
            result["installMethod"] = f"official archive (installer exit {code})"
        program = f"{dest}/bin/{compiler}"
        result["compilerFile"] = subprocess.check_output(["file", str(root / program.lstrip("/"))], text=True).strip()
        code, _, version = run([*prefix, *translate, program, "--version"], out / "version.log", env=env, check=False, timeout=30)
        result["versionExitCode"] = code
        result["executionMode"] = ("interpreter" if args.compiler == "xc32" else "dynarec") if translate else "native"
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
                build_current_box64(work, root, out, cache)
                result["box64Version"] = LOCK["box64-source"]["version"] + " (upstream source, BAD_SIGNAL=ON)"
                code, _, version = run([*prefix, *translate, program, "--version"], out / "version-current-box64.log", env=env, check=False, timeout=30)
                result["currentBox64VersionExitCode"] = code
                if code:
                    verify_clone_fix(work, root, out, cache, prefix, env)
                    result["box64CloneStackFix"] = True
                    code, _, version = run([*prefix, *translate, program, "--version"], out / "version-clone-fixed-box64.log", env=env, check=False, timeout=30)
                    result["cloneFixedVersionExitCode"] = code
            if code:
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
        template = REPO / "samples/PIC" / ("PIC18Blink" if family == "pic18" else "PIC32Blink")
        source = REPO / "build/pic/pic18-spike.c" if family == "pic18" else template / "main.c"
        shutil.copy2(source, root / "work/main.c")
        # XC32 6 uses the pack root; older XC32 versions used its xc32 subfolder.
        pack = f"/opt/tabstudio/packs/{family}/xc8" if family == "pic18" else f"/opt/tabstudio/packs/{family}"
        device = "18F45K22" if family == "pic18" else "32MX250F128B"
        processor = f"-mcpu={device}" if family == "pic18" else f"-mprocessor={device}"
        options = [processor, f"-mdfp={pack}", "-O0", "-o", "/results/blink.elf", "main.c"]
        if family == "pic32":
            options += ["-g", "-Wl,--defsym=_min_heap_size=0,--report-mem"]
        for i in range(3):
            code, elapsed, _ = run([*prefix, *translate, program, *options], out / f"compile-{i}.log", env=env, timeout=180, check=False)
            if code and translate and i == 0:
                result["initialCompileExitCode"] = code
                run([*prefix, "/usr/bin/env", "BOX64_LOG=2", "BOX64_NOBANNER=0", *translate, program, "-v", *options],
                    out / "compile-diagnostic.log", env=env, timeout=180, check=False)
                interpreted = ["/usr/bin/env", "BOX64_DYNAREC=0", "BOX64_AVX=0", "/usr/bin/box64"]
                code, elapsed, _ = run([*prefix, *interpreted, program, *options], out / "compile-interpreter.log", env=env, timeout=300, check=False)
                result["interpreterCompileExitCode"] = code
                if not code:
                    translate = interpreted
                    result["executionMode"] = "interpreter"
                else:
                    if not result["box64Version"].startswith(LOCK["box64-source"]["version"]):
                        build_current_box64(work, root, out, cache)
                        result["box64Version"] = LOCK["box64-source"]["version"] + " (upstream source, BAD_SIGNAL=ON)"
                    translate = ["/usr/bin/box64"]
                    result["executionMode"] = "dynarec"
                    code, elapsed, _ = run([*prefix, *translate, program, *options], out / "compile-current-box64.log", env=env, timeout=180, check=False)
                    if code and not result.get("box64CloneStackFix"):
                        verify_clone_fix(work, root, out, cache, prefix, env)
                        result["box64CloneStackFix"] = True
                        code, elapsed, _ = run([*prefix, *translate, program, *options], out / "compile-clone-fixed-box64.log", env=env, timeout=180, check=False)
            if code:
                raise RuntimeError(f"{compiler} compile failed (exit {code}); see compile diagnostics")
            result["compileSeconds"].append(elapsed)
        if family == "pic32":
            run([*prefix, *translate, f"{dest}/bin/xc32-bin2hex", "/results/blink.elf"], out / "bin2hex.log", env=env)
        hex_files = list(out.glob("*.hex"))
        if not hex_files:
            raise RuntimeError("The compiler did not produce a HEX file")
        result["hexSha256"] = hashlib.sha256(hex_files[0].read_bytes()).hexdigest()
        # Exercise the shipped Store wrappers and MPLAB driver, not just a direct
        # compiler command. The already verified DFP is copied into its real
        # per-version install layout; no compiler is uploaded as an artifact.
        native = [*prefix, "/usr/bin/env", "-u", "LD_LIBRARY_PATH"]
        repositories = root / "etc/apk/repositories"
        catalog = json.loads((REPO / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())["linux"]
        repositories.write_text(f'{catalog["mirror"]}/{catalog["branch"]}/main\n{catalog["mirror"]}/{catalog["branch"]}/community\n')
        shutil.copyfile("/etc/resolv.conf", root / "etc/resolv.conf")
        (root / "etc/ssl/certs").mkdir(parents=True, exist_ok=True)
        shutil.copyfile("/etc/ssl/certs/ca-certificates.crt", root / "etc/ssl/certs/ca-certificates.crt")
        run([*native, "/sbin/apk", "add", "--no-cache", "make", "python3", "lscpu", "clang22-extra-tools"], out / "project-dependencies.log", env=env)
        support = root / "opt/tabstudio/microchip"
        support.mkdir(parents=True, exist_ok=True)
        for name in ("install.py", "project.py", "packs.lock.json"):
            shutil.copy2(REPO / "src/TabStudio.Toolchains/Microchip" / name, support / name)
        shutil.copy2(REPO / "build/pic/assets.lock.json", support / "assets.lock.json")
        if translate:
            shutil.copy2(root / "usr/bin/box64", support / "box64")
        packs = json.loads((support / "packs.lock.json").read_text())["packs"]
        selected = next(p for p in packs if p["sha256"] == LOCK[family + "-dfp"]["sha256"])
        installed_pack = support / "packs" / selected["vendor"] / selected["name"] / selected["version"]
        shutil.copytree(pack_dir, installed_pack, dirs_exist_ok=True)
        (installed_pack / ".tabstudio-sha256").write_text(selected["sha256"])
        project_path = root / "work/project"
        shutil.copytree(template, project_path, ignore=shutil.ignore_patterns("dist", "build", ".tabstudio"), dirs_exist_ok=True)
        manifest = {"compilerRoot": dest, "version": LOCK[args.compiler]["version"]}
        (support / (args.compiler + ".json")).write_text(json.dumps(manifest))
        setup = "import sys;sys.path.insert(0,'/opt/tabstudio/microchip');from install import write_wrappers;write_wrappers(" + repr(args.compiler) + ",__import__('pathlib').Path(" + repr(dest) + "))"
        run([*native, "python3", "-c", setup], out / "project-wrappers.log", env=env)
        wrapper_path = root / "usr/local/bin" / ("xc8-cc" if args.compiler == "xc8" else "xc32-gcc")
        (out / "compiler-wrapper.txt").write_text(wrapper_path.read_text())
        project_code, _, _ = run([*native, "python3", "/opt/tabstudio/microchip/project.py", "build", "/work/project"], out / "project-build.log", env=env, timeout=300, check=False)
        if project_code and args.compiler == "xc32" and translate:
            # Keep failures actionable: distinguish compiler arguments, shell
            # wrappers and spawning from native musl Python. None replaces the
            # mandatory production-driver result below.
            exact = [processor, "-mdfp=" + str(installed_pack).removeprefix(str(root)), "-O0", "-g", "-Wl,--defsym=_min_heap_size=0,--report-mem", "-o", "/results/diagnostic.elf", "/work/project/main.c"]
            run([*prefix, *translate, program, *exact], out / "project-direct-control.log", env=env, timeout=180, check=False)
            invocation = ["/opt/tabstudio/microchip/box64", program, *exact]
            script = "import subprocess;raise SystemExit(subprocess.call(" + repr(invocation) + "))"
            run([*native, "python3", "-c", script], out / "project-native-python-control.log", env=env, timeout=180, check=False)
            run([*native, "/usr/local/bin/xc32-gcc", *exact], out / "project-wrapper-control.log", env=env, timeout=180, check=False)
            for label, arguments, capture in [
                ("cwd-absolute", exact, False),
                ("cwd-relative", [processor, "-mdfp=" + str(installed_pack).removeprefix(str(root)), "-O0", "-g", "-Wl,--defsym=_min_heap_size=0,--report-mem", "-o", "dist/default/production/firmware.elf", "main.c"], False),
                ("pipe", exact, True),
            ]:
                invocation = ["/usr/local/bin/xc32-gcc", *arguments]
                script = "import subprocess;subprocess._USE_VFORK=False;subprocess._USE_POSIX_SPAWN=False;r=subprocess.run(" + repr(invocation) + ",cwd='/work/project'" + (",input='',text=True,capture_output=True);print(r.stdout);print(r.stderr)" if capture else ")") + ";raise SystemExit(r.returncode)"
                run([*native, "python3", "-c", script], out / ("project-" + label + "-control.log"), env=env, timeout=180, check=False)
            run([*native, "BOX64_STRACE=1", "BOX64_LOG=2", "python3", "/opt/tabstudio/microchip/project.py", "build", "/work/project"],
                out / "project-syscalls.log", env=env, timeout=180, check=False)
        if project_code:
            raise RuntimeError("The shipped MPLAB project driver failed; see project-build.log and controls")
        project_hex = project_path / "dist/default/production/firmware.hex"
        if not project_hex.is_file() or not (project_path / ".tabstudio/pic/xc-predefined-macros.txt").is_file():
            raise RuntimeError("The shipped project driver did not produce firmware and compiler builtin metadata")
        shutil.copy2(project_hex, out / "template.hex")
        result["mplabProjectBuild"] = True
        shutil.copy2(project_path / ".tabstudio/pic/xc-predefined-macros.txt", out / "xc-predefined-macros.txt")
        if args.compiler == "xc8":
            # Compile a small source-line/stepping fixture independently from
            # the blink template, using its actual supported PIC18F14K22 model.
            shutil.copy2(REPO / "build/pic/simulation-smoke.c", root / "work/simulation.c")
            shutil.copy2(REPO / "build/pic/simulation-smoke.c", out / "simulation.c")
            run([*prefix, *translate, program, "-mcpu=18F14K22", f"-mdfp={pack}", "-O0", "-o", "/results/simulation.elf", "simulation.c"], out / "simulation-compile.log", env=env, timeout=180)
        # The editor must parse the real device/compiler headers, not merely
        # produce a compilation-database JSON file with plausible arguments.
        # Limit optional refactoring probes to the opening comment. Diagnostics
        # still parse the entire translation unit and all device headers; this
        # avoids clangd's unrelated SwapBinaryOperands macro-rewrite failure.
        run([*native, "clangd", "--check=/work/project/main.c", "--compile-commands-dir=/work/project/.tabstudio/pic", "--check-lines=1-1"],
            out / "clangd-device-headers.log", env=env, timeout=180)
        result["intelliSenseCheck"] = True
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
