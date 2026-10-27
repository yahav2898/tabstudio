#!/usr/bin/env python3
"""Build the canonical tablet CH32V003 template with its pinned upstream MCU support.

Host-only cross-compilation proof: this never opens a USB probe or programs hardware.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("flash_clients_smoke", ROOT / "build/embedded/flash-clients-smoke.py")
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


def main():
    catalog = json.loads((ROOT / "src/TabStudio.Toolchains/Catalog/toolchains.json").read_text())
    toolchain = next(item for item in catalog["toolchains"] if item["id"] == "embedded-ch32fun")
    archive = toolchain["setup"][0]["archive"]
    cache = Path(tempfile.gettempdir()) / "tabstudio-flash-client-cache"
    cache.mkdir(exist_ok=True)
    package = helpers.download(archive, cache)
    with tempfile.TemporaryDirectory(prefix="tabstudio-ch32fun-") as temporary:
        temporary = Path(temporary)
        with tarfile.open(package) as source:
            source.extractall(temporary / "sdk", filter="data")
        sdk = temporary / "sdk" / ("ch32fun-" + toolchain["pinnedVersion"])
        assert (sdk / "LICENSE").is_file()
        assert (sdk / "misc/LIBGCC_LICENSE").is_file()
        project = temporary / "firmware"
        project.mkdir()
        for file in (ROOT / "native/embedded/templates/ch32v003").iterdir():
            (project / file.name).write_text(file.read_text().replace("@CH32FUN@", str(sdk / "ch32fun")), encoding="utf-8")
        prefix = os.environ.get("TABSTUDIO_CH32_PREFIX", "riscv-none-elf")
        arguments = ["make", "-j2", "build", "PREFIX=" + prefix, "GENERATED_LD_FILE=" + str(project / "ch32v003.ld")]
        if os.environ.get("TABSTUDIO_CH32_NEWLIB"):
            arguments.append("NEWLIB=" + os.environ["TABSTUDIO_CH32_NEWLIB"])
        subprocess.run(arguments, cwd=project, check=True, timeout=180)
        image = (project / "firmware.bin").read_bytes()
        assert 0 < len(image) <= 16384
        elf = (project / "firmware.elf").read_bytes()
        assert elf[:6] == b"\x7fELF\x01\x01"  # Little-endian ELF32, from the actual upstream linker script.
        assert struct.unpack_from("<H", elf, 18)[0] == 243
        assert struct.unpack_from("<I", elf, 24)[0] == 0
        assert struct.unpack_from("<I", elf, 36)[0] & 8  # RV32E ABI; this is not generic RV32I firmware.
        table, size, count = struct.unpack_from("<I", elf, 28)[0], struct.unpack_from("<H", elf, 42)[0], struct.unpack_from("<H", elf, 44)[0]
        loaded = []
        for index in range(count):
            entry = table + index * size
            if struct.unpack_from("<I", elf, entry)[0] == 1:
                physical, length = struct.unpack_from("<II", elf, entry + 12)
                assert physical + length <= 16384
                loaded.append([physical, length])
        assert loaded
        print(json.dumps({"revision": toolchain["pinnedVersion"], "source_sha256": archive["sha256"], "flash_bytes": len(image),
                          "firmware_sha256": hashlib.sha256(image).hexdigest(), "load_ranges": loaded}, indent=2), flush=True)
        print("TABSTUDIO_CH32FUN_TEMPLATE_BUILT", flush=True)


if __name__ == "__main__":
    main()
