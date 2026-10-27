#!/usr/bin/env python3
"""Build the production unchanged WCH-Link source on native Alpine ARM64/x64.

This exercises the same source/dependency pins and installer as the Store. It
opens no USB device and does not substitute a firmware target for the host ABI.
"""
import hashlib
import importlib.util
import os
from pathlib import Path
import platform
import re
import subprocess
import tarfile
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]


def main():
    definition = (ROOT / "src/TabStudio.Toolchains/Embedded/Installation/EmbeddedWlinkInstallation.cs").read_text()
    def constant(name):
        return re.search(r'const string ' + re.escape(name) + r' = "([a-f0-9.]+)";', definition).group(1)
    revision, digest = constant("Revision"), constant("Sha256")
    size = int(re.search(r"const long ArchiveBytes = ([0-9_]+);", definition).group(1).replace("_", ""))
    spec = importlib.util.spec_from_file_location("tabstudio_wlink_installer", ROOT / "native/embedded/install_wlink_client.py")
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    assert helper.REVISION == revision and helper.LOCK_SHA256 == constant("CargoLockSha256")
    with tempfile.TemporaryDirectory(prefix="tabstudio-wlink-native-") as temporary:
        temporary = Path(temporary)
        archive = temporary / "source.tar.gz"
        request = urllib.request.Request("https://github.com/ch32-rs/wlink/archive/" + revision + ".tar.gz", headers={"User-Agent": "TabStudio-native-WCH-Link-CI"})
        with urllib.request.urlopen(request, timeout=120) as response:
            content = response.read(size + 1)
        if len(content) != size or hashlib.sha256(content).hexdigest() != digest:
            raise ValueError("WCH-Link source archive differs from the Store's exact source pin")
        archive.write_bytes(content)
        upstream = temporary / "upstream"
        upstream.mkdir()
        with tarfile.open(archive, "r:gz") as package:
            package.extractall(upstream, filter="data")
        # A firmware workspace can configure these globally. The product
        # installer must explicitly choose rustc's actual native host instead.
        os.environ["CARGO_BUILD_TARGET"] = "thumbv6m-none-eabi"
        external = temporary / "not-the-firmware-output"
        os.environ["CARGO_TARGET_DIR"] = str(external)
        helper.install(ROOT / "native/embedded/install_source_client.py", upstream, temporary / "bin")
        executable = temporary / "bin/wlink"
        for arguments in (["--version"], ["flash", "--help"], ["dump", "--help"], ["reset", "--help"]):
            subprocess.run([str(executable), *arguments], check=True, timeout=60)
        source = upstream / ("wlink-" + revision)
        assert hashlib.sha256((source / "Cargo.lock").read_bytes()).hexdigest() == constant("CargoLockSha256")
        assert (source / "LICENSE-MIT").is_file() and (source / "LICENSE-APACHE").is_file()
        assert not (source / "target").exists() and not external.exists()
        print("TABSTUDIO_NATIVE_WLINK_INSTALL_PASSED architecture=" + platform.machine() + " source=" + revision + " usbOpened=false", flush=True)


if __name__ == "__main__":
    main()
