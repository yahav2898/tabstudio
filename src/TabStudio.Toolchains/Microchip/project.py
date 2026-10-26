#!/usr/bin/env python3
"""Build the selected MPLAB configuration using its generated makefiles.

No option-name approximation is used: MPLAB's makefile retains per-file flags,
libraries, ordering, custom steps and image settings. A missing generated makefile
is an error, not permission to silently build a different program.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

from install import ASSETS, TOOLS, ensure_pack


def descriptor(path, configuration=None):
    file = path / "nbproject/configurations.xml"
    data = file.read_bytes()
    if len(data) > 16_000_000 or b"<!DOCTYPE" in data.upper():
        raise ValueError("Invalid or oversized MPLAB configuration descriptor")
    root = ET.fromstring(data)
    for element in root.iter():
        element.tag = element.tag.rsplit("}", 1)[-1]
    configurations = root.findall("./confs/conf")
    if not configurations:
        raise ValueError("The MPLAB project has no configurations")
    selected = configuration or ("default" if any(c.get("name") == "default" for c in configurations) else configurations[0].get("name"))
    matches = [c for c in configurations if c.get("name") == selected]
    if len(matches) != 1:
        raise ValueError("Unknown or duplicate MPLAB configuration: " + str(selected))
    # A configuration is used in a generated filename and make variable. It is
    # never interpolated into a shell command; prohibit a path outside nbproject.
    if not selected or not re.fullmatch(r"[A-Za-z0-9_. -]+", selected) or selected in (".", ".."):
        raise ValueError("The MPLAB configuration name cannot be used as a makefile name")
    conf = matches[0]
    device = conf.findtext("./toolsSet/targetDevice", "").strip()
    compiler = conf.findtext("./toolsSet/languageToolchain", "").lower().strip()
    version = conf.findtext("./toolsSet/languageToolchainVersion", "").strip()
    if compiler not in ("xc8", "xc32") or not device:
        raise ValueError("This MPLAB configuration does not select XC8 or XC32 and a device")
    return selected, device, compiler, version, conf


def build(path, configuration=None):
    path = path.resolve()
    if path.is_file():
        path = path.parent
    while not (path / "nbproject/configurations.xml").is_file():
        if path == path.parent:
            raise ValueError("Open an MPLAB X project containing nbproject/configurations.xml")
        path = path.parent
    selected, device, compiler, version, conf = descriptor(path, configuration)
    manifest = json.loads((TOOLS / (compiler + ".json")).read_text())
    if version and tuple(map(int, version.split("."))) != tuple(map(int, manifest["version"].split("."))):
        raise ValueError(f"Project requests {compiler.upper()} {version}; installed version is {manifest['version']}. Select that installed compiler in the project; no compiler version was silently substituted.")
    requested = None
    for p in conf.findall("./packs/pack"):
        candidate = (p.get("vendor", ""), p.get("name", ""), p.get("version", ""))
        try:
            pack, pack_path = ensure_pack(device, candidate)
            requested = candidate
            break
        except LookupError:
            continue
    if requested is None:
        if conf.findall("./packs/pack"):
            raise ValueError("The selected configuration's exact device pack is not in the verified pack catalog")
        pack, pack_path = ensure_pack(device)
    dfp = pack_path / "xc8" if compiler == "xc8" else pack_path if int(manifest["version"].split(".")[0]) >= 6 else pack_path / "xc32"
    makefile = path / "nbproject" / ("Makefile-" + selected + ".mk")
    if not makefile.is_file():
        raise ValueError("MPLAB's generated makefile is missing: " + str(makefile) + ". Generate it with MPLAB X's prjMakefilesGenerator, or use the Windows Bridge; options have not been guessed.")
    cc = "/usr/local/bin/xc8-cc" if compiler == "xc8" else "/usr/local/bin/xc32-gcc"
    command = ["make", "-f", str(makefile.relative_to(path)), "SUBPROJECTS=", ".build-conf", "CND_CONF=" + selected,
               "TYPE_IMAGE=PRODUCTION", "MP_CC=" + cc, "MP_LD=" + cc, "MP_CC_DIR=/usr/local/bin",
               "MP_AS=" + ("/usr/local/bin/xc8-cc" if compiler == "xc8" else "/usr/local/bin/xc32-as"),
               "MP_AR=/usr/local/bin/" + compiler + "-ar", "DFP_DIR=" + str(dfp)]
    print(f"MPLAB configuration: {selected}; {device}; {compiler.upper()} {manifest['version']}; {pack['name']} {pack['version']}", flush=True)
    print(" ".join(command), flush=True)
    write_intellisense(path, conf, manifest, compiler, device, dfp)
    return subprocess.call(command, cwd=path)


def write_intellisense(path, conf, manifest, compiler, device, dfp):
    """clangd parses the device headers with explicit XC language extensions."""
    support = path / ".tabstudio/pic"
    support.mkdir(parents=True, exist_ok=True)
    compatibility = support / "xc-builtins.h"
    compatibility.write_text("""/* Editor-only declarations; never passed to the XC firmware build. */
#define __at(address)
#define __section(name)
#define __persistent
#define __bank(number)
#define __eeprom
#define __rom
#define __ram
#define __bit unsigned char
#define __pack
#define __interrupt(...)
#define __nonreentrant
#define __reentrant
#define __delay_ms(ms) ((void)(ms))
#define __delay_us(us) ((void)(us))
void __builtin_software_breakpoint(void);
""")
    compiler_root = Path(manifest["compilerRoot"])
    includes = ([dfp / "pic/include", dfp / "pic/include/proc", compiler_root / "pic/include/c99", compiler_root / "pic/include"]
                if compiler == "xc8" else [dfp / "include", dfp / "include/proc", compiler_root / "pic32m/include"])
    flags = ["-xc", "-std=c99", "-fsyntax-only", "-Wno-unknown-pragmas", "-Wno-ignored-attributes", "-include", str(compatibility)]
    for folder in includes:
        flags += ["-isystem", str(folder)]
    flags += (["-D__XC8", "-D__XC8__", "-D__XC8_VERSION=40000", "-D__" + device.removeprefix("PIC"), "-D_" + device.removeprefix("PIC"), "-D_PIC18"]
              if compiler == "xc8" else ["--target=mipsel-unknown-none", "-D__XC32", "-D__XC32__", "-D__" + device.removeprefix("PIC") + "__"])
    # Include/define settings are kept per translation unit, including overrides.
    files = ET.parse(path / "nbproject/configurations.xml").findall(".//itemPath")
    commands = []
    for item in files:
        source = item.text or ""
        if Path(source).suffix.lower() not in (".c", ".cpp", ".cc", ".cxx"):
            continue
        local = list(flags)
        groups = [g for g in conf if g.tag != "item"]
        override = next((e for e in conf.findall("item") if e.get("path", "").replace("\\", "/") == source.replace("\\", "/")), None)
        if override is not None and override.get("ex") in ("true", "1"):
            continue
        properties = {}
        for group in groups + (list(override) if override is not None else []):
            for prop in group.findall("property"):
                properties[prop.get("key")] = prop.get("value", "")
        for key, value in properties.items():
            if key in ("extra-include-directories", "include-directories"):
                local.extend("-I" + p for p in value.split(";") if p)
            if key in ("define-macros", "preprocessor-macros"):
                local.extend("-D" + p for p in value.split(";") if p)
        commands.append({"directory": str(path), "file": str(path / source), "arguments": ["clang", *local, str(path / source)]})
    (support / "compile_commands.json").write_text(json.dumps(commands, indent=2) + "\n")
    config = path / ".clangd"
    marker = "# TabStudio PIC compilation database\n"
    if not config.exists() or config.read_text().startswith(marker):
        config.write_text(marker + "CompileFlags:\n  CompilationDatabase: .tabstudio/pic\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build"])
    parser.add_argument("path", nargs="?", default=".", type=Path)
    parser.add_argument("--configuration")
    args = parser.parse_args()
    try:
        sys.exit(build(args.path, args.configuration))
    except (OSError, ValueError, ET.ParseError, LookupError) as error:
        print("PIC build: error: " + str(error), file=sys.stderr)
        sys.exit(1)
