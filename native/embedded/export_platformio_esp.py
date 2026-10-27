"""Template post-build action: export resolved esptool inputs without guessing offsets.

Uses PlatformIO's final SCons UPLOADERFLAGS, FLASH_EXTRA_IMAGES and ESP32_APP_OFFSET.
The original project/core upload command is not executed. Files are copied into
one bounded build directory so the tablet can inspect each separately.
"""
import json
import os
from pathlib import Path
import shutil


def export_manifest(source, target, env):
    if list(env.get("PIOFRAMEWORK", [])) != ["arduino"] or env.subst("$UPLOAD_PROTOCOL") != "esptool":
        raise ValueError("TabStudio ESP export requires the resolved Arduino/esptool upload protocol")
    tokens = [str(value) for value in env.subst_list("$UPLOADERFLAGS")[0]]
    settings = {}
    flags = {}
    pairs = []
    operation = False
    at = 0
    while at < len(tokens):
        flag = tokens[at].replace("_", "-")
        if flag in ("--chip", "--port", "--baud", "--before", "--after", "--flash-mode", "--flash-freq", "--flash-size"):
            if at + 1 >= len(tokens) or flag in flags:
                raise ValueError("Missing/duplicate resolved esptool flag")
            flags[flag] = tokens[at + 1]
            at += 2
        elif flag == "write-flash" and not operation:
            operation = True
            at += 1
        elif flag in ("-z", "--compress"):
            at += 1
        else:
            if not operation or at + 1 >= len(tokens):
                raise ValueError("Unknown esptool flag: encryption/force/protection/custom commands cannot be exported")
            address = int(tokens[at], 0)
            pairs.append((address, tokens[at + 1].strip('"')))
            at += 2
    for name in ("flash-mode", "flash-freq", "flash-size"):
        settings[name.replace("-", "_")] = flags["--" + name]
    chip = flags["--chip"]
    if chip not in ("esp32", "esp32s2", "esp32s3", "esp32c3", "esp32c6", "esp32h2"):
        raise ValueError("Unsupported resolved ESP chip")
    if not operation or not pairs:
        raise ValueError("Resolved upload metadata has no bootloader/partition images")
    application_offset = int(env.subst("$ESP32_APP_OFFSET"), 0)
    application = Path(env.subst("$BUILD_DIR/${PROGNAME}.bin")).resolve(strict=True)
    pairs.append((application_offset, str(application)))
    build = Path(env.subst("$BUILD_DIR")).resolve(strict=True)
    image_directory = build / "tabstudio-flash-images"
    image_directory.mkdir(exist_ok=True)
    manifest = {
        "write_flash_args": ["--flash-mode", settings["flash_mode"], "--flash-freq", settings["flash_freq"], "--flash-size", settings["flash_size"]],
        "flash_settings": settings, "flash_files": {},
        "extra_esptool_args": {"chip": chip, "before": flags["--before"], "after": flags["--after"], "stub": True},
        "tabstudio_source": "platformio-resolved-upload",
    }
    for index, (address, original) in enumerate(pairs):
        original = Path(original).resolve(strict=True)
        with original.open("rb") as image:
            magic = image.read(2)
        if original == application:
            role = "app"
        elif magic == b"\xaa\x50":  # ESP-IDF partition records start with bytes AA 50 (0x50AA little-endian)
            role = "partition-table"
        elif magic[:1] == b"\xe9":
            role = "bootloader"
        else:
            role = "extra-" + str(index)
        if role in manifest:
            raise ValueError("Ambiguous resolved ESP image role: " + role)
        relative = "tabstudio-flash-images/image-" + str(index).zfill(2) + ".bin"
        destination = build / relative
        pending = destination.with_suffix(".partial")
        try:
            shutil.copyfile(original, pending)
            os.replace(pending, destination)
        finally:
            pending.unlink(missing_ok=True)
        offset = hex(address)
        if offset in manifest["flash_files"]:
            raise ValueError("Resolved ESP images overlap at the same offset")
        manifest["flash_files"][offset] = relative
        manifest[role] = {"offset": offset, "file": relative, "encrypted": "false"}
    if not all(role in manifest for role in ("app", "bootloader", "partition-table")):
        raise ValueError("Resolved upload did not identify bootloader, partition table and application; no offsets were guessed")
    destination = build / "tabstudio-flasher-args.json"
    pending = destination.with_suffix(".partial")
    try:
        pending.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        os.replace(pending, destination)
    finally:
        pending.unlink(missing_ok=True)
    print("TabStudio: exported " + str(len(pairs)) + " distinct ESP flash images; no gaps merged or padded")


if "Import" in globals():
    Import("env")
    env.AddPostAction("$BUILD_DIR/${PROGNAME}.bin", export_manifest)
