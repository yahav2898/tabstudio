#!/usr/bin/env python3
"""Real pinned PlatformIO manager/downloader against a local synthetic registry.

The ELF32 ARM mislabeled-as-ARM64 case models an observed vendor distribution.
No synthetic archive or loopback interaction here is a hardware transcript.
"""
import argparse
import contextlib
import hashlib
import http.server
import io
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tarfile
import tempfile
import threading
from unittest.mock import patch

sys.path.insert(0, "/opt/tabstudio/embedded")
if not Path("/opt/tabstudio/embedded").is_dir():
    sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "native/embedded"))
import platformio_box64 as policy
from platformio.package.manager.tool import ToolPackageManager
from platformio.package.meta import PackageSpec


def archive(name, executable, marker, postinstall=None):
    result = io.BytesIO()
    with tarfile.open(fileobj=result, mode="w:gz") as package:
        for path, data, mode in [
            ("package.json", json.dumps({"name": name, "version": "1.2.3", "system": ["linux_aarch64"],
                "scripts": {"postinstall": ["postinstall.py"]}}).encode(), 0o644),
            ("bin/tool", executable, 0o755),
            ("postinstall.py", postinstall if postinstall is not None else ("from pathlib import Path\nPath(" + repr(str(marker)) + ").write_text('executed')\n").encode(), 0o644),
        ]:
            info = tarfile.TarInfo(path)
            info.size, info.mode = len(data), mode
            package.addfile(info, io.BytesIO(data))
    return result.getvalue()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--x64-client", required=True, type=Path)
    parser.add_argument("--native-only", action="store_true")
    parser.add_argument("--postinstall-foreign", action="store_true")
    args = parser.parse_args()
    client = args.x64_client.read_bytes()
    if client[:7] != b"\x7fELF\x02\x01\x01" or client[18:20] != b"\x3e\0":
        raise ValueError("Policy proof needs a real x64 executable")
    with tempfile.TemporaryDirectory(prefix="tabstudio-pio-policy-") as temporary:
        root = Path(temporary)
        os.environ["PLATFORMIO_CORE_DIR"] = str(root / "core")
        os.environ["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "no"
        os.environ.pop("PLATFORMIO_SYSTEM_TYPE", None)
        if args.postinstall_foreign:
            marker = root / "MUST_NOT_EXECUTE"
            candidate = root / "postinstall-package.tar.gz"
            # No foreign ELF exists until the legitimate Python script creates it.
            script = ("from pathlib import Path\nPath('bin/tool').write_bytes(" + repr(client) + ")\n").encode()
            candidate.write_bytes(archive("generated-fixture", b"#!/bin/sh\nexit 0\n", marker, script))
            policy.configure_policy("aarch64", allow_fallback=False)
            manager = ToolPackageManager(str(root / "packages"))
            try:
                manager.install(PackageSpec(uri=candidate.as_uri()), skip_dependencies=True)
            except ValueError as error:
                if "postinstall produced incompatible host executables" not in str(error):
                    raise
            else:
                raise AssertionError("A postinstall-generated foreign executable bypassed the audit")
            print("PLATFORMIO_POSTINSTALL_REFUSAL_PASSED hardware=false", flush=True)
            return
        if args.native_only:
            marker = root / "MUST_NOT_EXECUTE"
            candidate = root / "x64-package.tar.gz"
            candidate.write_bytes(archive("native-only-fixture", client, marker))
            policy.configure_policy("aarch64", allow_fallback=False)
            manager = ToolPackageManager(str(root / "packages"))
            try:
                manager.install(PackageSpec(uri=candidate.as_uri()), skip_dependencies=True)
            except ValueError as error:
                if "Refusing incompatible PlatformIO executable" not in str(error):
                    raise
            else:
                raise AssertionError("Native-only policy accepted an x64 host executable")
            if marker.exists():
                raise AssertionError("Native-only policy executed incompatible postinstall")
            print("PLATFORMIO_NATIVE_ONLY_REFUSAL_PASSED hardware=false", flush=True)
            return
        native = bytearray(52)
        native[:7] = b"\x7fELF\x01\x01\x01"
        struct.pack_into("<HH", native, 16, 2, 40)  # ELF32 ARM, never executable on ARM64/Box64.
        wrong_marker, correct_marker = root / "WRONG_NATIVE_POSTINSTALL", root / "correct-postinstall"
        archives = {"native": archive("synthetic-tool", native, wrong_marker),
                    "x64": archive("synthetic-tool", client, correct_marker)}
        requests = []

        class Handler(http.server.BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_HEAD(self):
                requests.append(("HEAD", self.path))
                name = self.path.rsplit("/", 1)[-1]
                data = archives[name]
                self.send_response(302)
                self.send_header("Location", address + "/archive/" + name)
                self.send_header("X-PIO-Mirror", address)
                self.send_header("X-PIO-Content-SHA256", hashlib.sha256(data).hexdigest())
                self.end_headers()

            def do_GET(self):
                requests.append(("GET", self.path))
                data = archives[self.path.rsplit("/", 1)[-1]]
                self.send_response(200)
                self.send_header("Content-Length", str(len(data)))
                self.send_header("Content-Type", "application/gzip")
                self.end_headers()
                self.wfile.write(data)

        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        address = "http://127.0.0.1:" + str(server.server_port)
        worker = threading.Thread(target=server.serve_forever, daemon=True)
        worker.start()
        try:
            files = [{"system": ["linux_aarch64" if name == "native" else "linux_x86_64"],
                "download_url": address + "/registry/" + name,
                "checksum": {"sha256": hashlib.sha256(data).hexdigest()}}
                for name, data in archives.items()]
            registry = {"id": 123, "owner": {"username": "synthetic"}, "name": "synthetic-tool",
                        "versions": [{"name": "1.2.3", "files": files}]}
            policy.configure_policy("aarch64")
            manager = ToolPackageManager(str(root / "packages"))
            with patch.object(manager, "fetch_registry_package", return_value=registry), patch("platformio.http.ensure_internet_on", return_value=True):
                # The registry is loopback-only. Skip the unrelated global WAN
                # reachability probe; HTTP, archives and checksum checks are real.
                package = manager.install(PackageSpec(owner="synthetic", name="synthetic-tool", requirements="1.2.3"), skip_dependencies=True)
            if wrong_marker.exists() or not correct_marker.is_file():
                raise AssertionError("An incompatible package script ran, or the correct replacement script did not run")
            record = json.loads((Path(package.path) / ".tabstudio-box64-overlay.json").read_text())
            if (Path(record["original"]) / "bin/tool").read_bytes() != client:
                raise AssertionError("The verified x64 package executable changed")
            rejected = list((Path(package.path).parent / ".tabstudio-rejected-native").glob("*/bin/tool"))
            if len(rejected) != 1 or rejected[0].read_bytes() != native:
                raise AssertionError("The rejected native distribution was not retained unchanged")
            if [path for method, path in requests if method == "GET"] != ["/archive/native", "/archive/x64"]:
                raise AssertionError("Expected native download before exact-version fallback: " + repr(requests))
            for flag, marker in [("--native-only", "PLATFORMIO_NATIVE_ONLY_REFUSAL_PASSED"),
                                 ("--postinstall-foreign", "PLATFORMIO_POSTINSTALL_REFUSAL_PASSED")]:
                result = subprocess.run([sys.executable, "-P", str(Path(__file__).resolve()),
                    "--x64-client", str(args.x64_client.resolve()), flag],
                    text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=30)
                print(result.stdout, end="", flush=True)
                if result.returncode or marker not in result.stdout:
                    raise AssertionError("Policy refusal failed: " + flag)
            print("PLATFORMIO_NATIVE_ELF_FALLBACK_PASSED native_then_exact_x64=true incompatible_postinstall=false original_bytes=true hardware=false", flush=True)
        finally:
            server.shutdown()
            server.server_close()
            worker.join(timeout=5)


if __name__ == "__main__":
    main()
