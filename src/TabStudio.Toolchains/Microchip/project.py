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

# Do not share a musl parent's stack with the glibc translator across PRoot's
# execve loader. The firmware compiler still controls its own child processes.
subprocess._USE_VFORK = False
subprocess._USE_POSIX_SPAWN = False


def descriptor(path, configuration=None):
    if configuration is None and (path / ".tabstudio/pic-build.json").is_file():
        selection = path / ".tabstudio/pic-build.json"
        if selection.stat().st_size > 65536:
            raise ValueError("PIC configuration selection is too large")
        configuration = json.loads(selection.read_text())["configuration"]
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


def make_tools(path, selected, compiler):
    """Relocate MPLAB's tool names without replacing its selected drivers."""
    names = {"MP_CC": compiler + ("-cc" if compiler == "xc8" else "-gcc"),
             "MP_AS": "xc8-cc" if compiler == "xc8" else "xc32-as",
             "MP_LD": "xc8-cc" if compiler == "xc8" else "xc32-ld", "MP_AR": compiler + "-ar"}
    if compiler == "xc32": names["MP_CPPC"] = "xc32-g++"
    local = path / "nbproject" / ("Makefile-local-" + selected + ".mk")
    if local.is_file():
        if local.stat().st_size > 1_000_000: raise ValueError("Oversized MPLAB local makefile")
        for line in local.read_text().splitlines():
            match = re.match(r"\s*(MP_(?:CC|CPPC|AS|LD|AR))\s*[:?]?=\s*(.*?)\s*$", line)
            if not match: continue
            name = match[2].strip('"\'').replace("\\", "/").rsplit("/", 1)[-1].removesuffix(".exe")
            if not re.fullmatch(r"[A-Za-z0-9_+.-]+", name) or not (name.startswith(compiler + "-") or compiler == "xc8" and name == "pic-as"):
                raise ValueError("Unsupported MPLAB tool assignment: " + line + "; use the Windows Bridge")
            names[match[1]] = name
    return [argument for key, name in names.items() for argument in
            (key + "=/usr/local/bin/" + name, key + "_DIR=/usr/local/bin")]


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
    command = ["make", "-f", str(makefile.relative_to(path)), "SUBPROJECTS=", ".build-conf", "CND_CONF=" + selected,
               "TYPE_IMAGE=PRODUCTION", *make_tools(path, selected, compiler), "DFP_DIR=" + str(pack_path)]
    print(f"MPLAB configuration: {selected}; {device}; {compiler.upper()} {manifest['version']}; {pack['name']} {pack['version']}", flush=True)
    print(" ".join(command), flush=True)
    try:
        write_intellisense(path, conf, manifest, compiler, device, dfp)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print("PIC IntelliSense: warning: " + str(error), file=sys.stderr, flush=True)
    code = subprocess.call(command, cwd=path)
    if code == 0:
        output = path / "dist" / selected / "production"
        images = {suffix: list(output.glob("*." + suffix)) for suffix in ("elf", "hex")}
        receipt = {"configuration": selected, "device": device, "compiler": compiler,
                   **{suffix: str(files[0]) for suffix, files in images.items() if len(files) == 1}}
        (path / ".tabstudio/pic/build.json").write_text(json.dumps(receipt, indent=2) + "\n")
    return code


def write_intellisense(path, conf, manifest, compiler, device, dfp):
    """clangd parses the device headers with explicit XC language extensions."""
    support = path / ".tabstudio/pic"
    support.mkdir(parents=True, exist_ok=True)
    compatibility = support / "xc-builtins.h"
    cc = "/usr/local/bin/xc8-cc" if compiler == "xc8" else "/usr/local/bin/xc32-gcc"
    processor = "-mcpu=" + device.removeprefix("PIC") if compiler == "xc8" else "-mprocessor=" + device.removeprefix("PIC")
    builtins = subprocess.run([cc, processor, "-mdfp=" + str(dfp), "-E", "-dM", "-x", "c", "-"],
                              input="", text=True, capture_output=True, timeout=60)
    if builtins.returncode or not any("__XC" in line for line in builtins.stdout.splitlines()):
        raise ValueError("Could not query the selected XC compiler's predefined macros: " + builtins.stderr[-2000:])
    # Preserve the real compiler/device macros. clangd's own type and ABI macros
    # stay tied to its target; it is an editor, never the firmware compiler.
    macros = []
    for line in builtins.stdout.splitlines():
        match = re.match(r"#define\s+([A-Za-z_][A-Za-z_0-9]*)", line)
        if match and ("XC" in match[1] or device.removeprefix("PIC").upper() in match[1].upper()
                      or match[1].startswith(("_PIC", "__PIC", "__18", "__16", "__12", "__10", "__32", "_18", "_16", "_12", "_10", "_32"))):
            macros.append(line)
    (support / "xc-predefined-macros.txt").write_text(builtins.stdout)
    compatibility.write_text("/* Editor-only XC/device macros and declarations; never used for firmware. */\n" + "\n".join(macros) + "\n" + """
#define __at(address)
#define __section(name)
#define __persistent
#define __bank(number)
#define __eeprom
#define __rom
#define __ram
#define __far
#define __near
#define __bit unsigned char
#define __pack
#define __interrupt(...)
#define __nonreentrant
#define __reentrant
#define __delay_ms(ms) ((void)(ms))
#define __delay_us(us) ((void)(us))
void __builtin_software_breakpoint(void);
unsigned int __builtin_disable_interrupts(void);
unsigned int __builtin_enable_interrupts(void);
unsigned int __builtin_mfc0(int, int);
void __builtin_mtc0(int, int, unsigned int);
""")
    compiler_root = Path(manifest["compilerRoot"])
    includes = ([dfp / "pic/include", dfp / "pic/include/proc", compiler_root / "pic/include/c99", compiler_root / "pic/include"]
                if compiler == "xc8" else [dfp / "include", dfp / "include/proc", *[compiler_root / f / "include" for f in ("pic32m", "pic32c", "pic32a")]])
    flags = ["-xc", "-std=gnu99", "-fsyntax-only", "-Wno-unknown-pragmas", "-Wno-ignored-attributes", "-include", str(compatibility)]
    for folder in includes:
        flags += ["-isystem", str(folder)]
    if compiler == "xc32":
        if "#define __mips__" in builtins.stdout or "#define __mips " in builtins.stdout:
            flags += ["--target=mipsel-unknown-none"]
        elif "#define __arm__" in builtins.stdout:
            flags += ["--target=arm-none-eabi"]
    # Include/define settings are kept per translation unit, including overrides.
    root = ET.parse(path / "nbproject/configurations.xml").getroot()
    for element in root.iter():
        element.tag = element.tag.rsplit("}", 1)[-1]
    files = root.findall(".//itemPath")
    commands = []
    for item in files:
        source = (item.text or "").replace("\\", "/")
        if Path(source).suffix.lower() not in (".c", ".cpp", ".cc", ".cxx"):
            continue
        local = list(flags)
        if Path(source).suffix.lower() in (".cpp", ".cc", ".cxx"):
            local = ["-xc++" if f == "-xc" else "-std=gnu++17" if f == "-std=gnu99" else f for f in local]
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
