#!/usr/bin/env python3
"""Evaluate XC32's published GCC and SDCC as native aarch64-musl tools.

This is a compiler/backend feasibility check, not a claim that a complete XC
distribution was rebuilt. The result distinguishes object generation from a
linked firmware image. It runs inside the same verified Alpine/PRoot rootfs.
"""
import argparse
import json
from pathlib import Path
import platform
import shutil
import subprocess
import zipfile

import spike


def evaluate(args):
    work, out = args.work.resolve(), args.output.resolve()
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    result = {"compiler": args.tool + "-native", "hostArchitecture": platform.machine(),
              "cpu": subprocess.check_output(["lscpu"], text=True), "success": False,
              "linkedFirmware": False, "compileSeconds": [], "componentSeconds": {}}
    try:
        if platform.machine() != "aarch64":
            raise RuntimeError("This evaluation requires a real aarch64 host")
        root, cache, prefix, _, env = spike.prepare(work, out, args.tool)
        # Alpine build tools are musl programs. Never inject Debian's glibc libs into them.
        # These source releases predate GCC 15's C23 default. In particular,
        # GMP's configure probes intentionally use pre-C23 function declarations.
        native = [*prefix, "/usr/bin/env", "-u", "LD_LIBRARY_PATH", "CC=gcc -std=gnu17"]
        shutil.copyfile("/etc/resolv.conf", root / "etc/resolv.conf")
        (root / "etc/ssl/certs").mkdir(parents=True, exist_ok=True)
        shutil.copyfile("/etc/ssl/certs/ca-certificates.crt", root / "etc/ssl/certs/ca-certificates.crt")
        catalog = json.loads((spike.REPO / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())["linux"]
        (root / "etc/apk/repositories").write_text("\n".join(f'{catalog["mirror"]}/{catalog["branch"]}/{repo}' for repo in catalog["repositories"]) + "\n")
        packages = ["build-base", "bash", "bison", "flex", "texinfo", "zlib-dev", "zstd-dev", "python3", "gawk"]
        if args.tool == "sdcc":
            packages += ["boost-dev"]
        spike.run([*native, "/sbin/apk", "add", "--no-cache", *packages], out / "alpine-build-tools.log", env=env, timeout=600)
        _, _, host = spike.run([*native, "gcc", "-dumpmachine"], out / "host-toolchain.log", env=env)
        result["nativeHost"] = host.strip()
        if args.tool == "xc32":
            archive = spike.fetch(spike.LOCK["xc32-source"], cache, "xc32-source.zip")
            # The official ZIP contains a nested source tarball. Stream it to tar.
            with zipfile.ZipFile(archive) as package, package.open("xc32-sources.tar.bz2") as source:
                with subprocess.Popen(["tar", "--no-same-owner", "-xjf", "-", "-C", str(root / "work")], stdin=subprocess.PIPE) as tar:
                    shutil.copyfileobj(source, tar.stdin)
                    tar.stdin.close()
                    if tar.wait():
                        raise RuntimeError("Cannot extract the official XC32 source")
            # These are Microchip's own top-level Makefile targets, ordered by
            # their actual dependencies (the published host-libs target has none).
            components = ["gmp", "mpfr", "mpc", "expat", "libmchp", "pic32m-binutils", "pic32m-gcc"]
            for component in components:
                _, seconds, _ = spike.run([*native, "sh", "-c", 'cd /work/xc32-source && exec make "${@}"',
                    "native-xc32", "VERSION=6.00", "make-args=-j4", "build-" + component],
                    out / (component + ".log"), env=env, timeout=2400)
                result["componentSeconds"][component] = seconds
            compiler = "/work/xc32-source/build/opt/bin/pic32m-gcc"
            source = "void blink_step(void) { *(volatile unsigned int *)0xbf88603cUL = 1; }\n"
            (root / "work/blink.c").write_text(source)
            pack = root / "opt/tabstudio/packs/pic32"
            pack.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(spike.fetch(spike.LOCK["pic32-dfp"], cache, "pic32.atpack")) as package:
                package.extractall(pack)
            options = ["-mprocessor=32MX250F128B", "-mdfp=/opt/tabstudio/packs/pic32", "-ffreestanding", "-O0", "-c", "blink.c", "-o", "/results/blink.o"]
        else:
            # Alpine 3.24 has no gputils package. Build the verified upstream
            # release rather than silently using an assembler on the host.
            gputils = spike.fetch(spike.LOCK["gputils-source"], cache, "gputils.tar.gz")
            spike.run(["tar", "--no-same-owner", "-xzf", str(gputils), "-C", str(root / "work")], out / "gputils-extract.log")
            spike.run([*native, "sh", "-c", "cd /work/gputils-1.5.2 && ./configure --prefix=/usr/local"], out / "gputils-configure.log", env=env, timeout=300)
            _, seconds, _ = spike.run([*native, "sh", "-c", "cd /work/gputils-1.5.2 && make -j4 && make install"], out / "gputils-build.log", env=env, timeout=900)
            result["componentSeconds"]["gputils"] = seconds
            archive = spike.fetch(spike.LOCK["sdcc-source"], cache, "sdcc.tar.bz2")
            spike.run(["tar", "--no-same-owner", "-xjf", str(archive), "-C", str(root / "work")], out / "extract.log")
            # Build only the PIC18 backend. Device libraries and other backends
            # are separate work; do not spend CI compiling thousands of devices.
            ports = ["mcs51", "z80", "z180", "r2k", "r2ka", "r3ka", "sm83", "tlcs90", "ez80_z80", "z80n", "r800", "ds390", "ds400", "pic14", "hc08", "s08", "stm8", "pdk13", "pdk14", "pdk15", "mos6502", "mos65c02", "f8"]
            configure = 'cd /work/sdcc-4.5.0 && exec ./configure --prefix=/opt/sdcc --enable-pic16-port --disable-device-lib --disable-ucsim --disable-sdcdb ' + " ".join("--disable-" + port + "-port" for port in ports)
            spike.run([*native, "sh", "-c", configure], out / "configure.log", env=env, timeout=300)
            _, seconds, _ = spike.run([*native, "sh", "-c", "cd /work/sdcc-4.5.0 && make -j4 && make install"], out / "build.log", env=env, timeout=2400)
            result["componentSeconds"]["sdcc"] = seconds
            compiler = "/opt/sdcc/bin/sdcc"
            # Only the NOP() compatibility subset is evaluated here. XC8 config
            # pragmas, interrupts and calibrated delays are not silently accepted.
            (root / "work/xc.h").write_text("#define __XC8_COMPAT_SDCC__ 1\n#define NOP() do { __asm nop __endasm; } while (0)\n")
            (root / "work/blink.c").write_text('#include "xc.h"\n__sfr __at(0xf8c) LATD;\nvoid main(void) { volatile unsigned int delay; for (;;) { LATD ^= 1; for (delay=0; delay<10000; ++delay) { NOP(); } } }\n')
            options = ["-mpic16", "-p18f45k22", "--use-non-free", "-c", "blink.c", "-o", "/results/blink.o"]
        _, _, result["version"] = spike.run([*native, compiler, "--version"], out / "version.log", env=env)
        for attempt in range(3):
            _, seconds, _ = spike.run([*native, compiler, *options], out / f"compile-{attempt}.log", env=env, timeout=180)
            result["compileSeconds"].append(seconds)
        result["success"] = (out / "blink.o").exists()
        result["scope"] = "Native compiler emits a target object; full startup/library/HEX linking is not yet validated."
        if not result["success"]:
            raise RuntimeError("No target object was emitted")
    except Exception as error:
        result["error"] = str(error)
        raise
    finally:
        if "root" in locals():
            for file in root.glob("work/**/config.log"):
                destination = out / "configure-details" / file.relative_to(root / "work")
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(file, destination)
        (out / "result.json").write_text(json.dumps(result, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tool", choices=["xc32", "sdcc"], required=True)
    parser.add_argument("--work", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    evaluate(parser.parse_args())
