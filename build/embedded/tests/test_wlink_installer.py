"""Native host selection and pinned source checks, without contacting a device."""
import importlib.util
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[3] / "native/embedded/install_wlink_client.py"
SPEC = importlib.util.spec_from_file_location("tabstudio_wlink_installer", SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
SHARED = SOURCE.with_name("install_source_client.py")


class WlinkInstallerTests(unittest.TestCase):
    def fixture(self, root):
        source = root / ("wlink-" + MODULE.REVISION)
        source.mkdir()
        lock = source / "Cargo.lock"
        lock.write_text("test-only dependency lock\n")
        (source / "LICENSE-MIT").write_text("test-only retained notice\n")
        return source, lock, hashlib.sha256(lock.read_bytes()).hexdigest()

    @staticmethod
    def write_elf(binary, architecture=62):
        binary.parent.mkdir(parents=True, exist_ok=True)
        header = bytearray(20)
        header[:7] = b"\x7fELF\x02\x01\x01"
        header[18:20] = architecture.to_bytes(2, "little")
        binary.write_bytes(header)

    def test_only_native_linux_host_triples_are_accepted(self):
        for host in ("aarch64-alpine-linux-musl", "x86_64-alpine-linux-musl", "aarch64-unknown-linux-gnu", "x86_64-unknown-linux-gnu"):
            self.assertEqual(host, MODULE.native_host("rustc 1.91.1\nhost: " + host + "\nrelease: 1.91.1\n"))
        for output in ("host: thumbv6m-none-eabi\n", "host: riscv32imc-unknown-none-elf\n", "host: aarch64-apple-darwin\n", "host: x86_64-unknown-linux-gnu\nhost: aarch64-unknown-linux-gnu\n", "host: x86_64-unknown-linux-gnu;unprotect\n"):
            with self.assertRaises(ValueError):
                MODULE.native_host(output)

    def test_changed_or_missing_lock_is_rejected_before_running_the_compiler(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / ("wlink-" + MODULE.REVISION)
            source.mkdir()
            (source / "Cargo.lock").write_text("not the upstream lock")
            with self.assertRaisesRegex(ValueError, "pinned upstream"):
                MODULE.install(root / "missing-shared-builder.py", root)

    def test_a_cross_compiled_firmware_or_wrong_abi_cannot_be_published_as_the_client(self):
        with tempfile.TemporaryDirectory() as temporary:
            binary = Path(temporary) / "wlink"
            header = bytearray(20)
            header[:7] = b"\x7fELF\x02\x01\x01"
            header[18:20] = (62).to_bytes(2, "little")
            binary.write_bytes(header)
            MODULE.validate_elf_host(binary, "x86_64-unknown-linux-gnu")
            with self.assertRaises(ValueError):
                MODULE.validate_elf_host(binary, "aarch64-alpine-linux-musl")
            for architecture in (40, 243):
                header[18:20] = architecture.to_bytes(2, "little")
                binary.write_bytes(header)
                with self.assertRaises(ValueError):
                    MODULE.validate_elf_host(binary, "x86_64-alpine-linux-musl")
            header[18:20] = (62).to_bytes(2, "little")
            header[4] = 1
            binary.write_bytes(header)
            with self.assertRaises(ValueError):
                MODULE.validate_elf_host(binary, "x86_64-unknown-linux-gnu")

    def test_firmware_target_and_shared_target_directory_cannot_redirect_the_native_client(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, lock, digest = self.fixture(root)
            host = "x86_64-unknown-linux-gnu"
            outside = root / "firmware-output"
            outside.mkdir()
            marker = outside / "firmware.elf"
            marker.write_bytes(b"must be retained")
            calls = []

            def compiler_version(command, **arguments):
                self.assertEqual(["rustc", "-vV"], command)
                self.assertNotIn("TABSTUDIO_USB_FD", arguments["env"])
                self.assertNotIn("LD_PRELOAD", arguments["env"])
                return "rustc 1.91.1\nhost: " + host + "\nrelease: 1.91.1\n"

            def run(command, **arguments):
                calls.append(command)
                self.assertNotIn("CARGO_TARGET_DIR", arguments["env"])
                self.assertNotIn("TABSTUDIO_USB_FD", arguments["env"])
                self.assertNotIn("LD_PRELOAD", arguments["env"])
                if command[:2] == ["cargo", "build"]:
                    self.assertEqual(["cargo", "build", "--locked", "--release", "--target", host, "--jobs", "2"], command)
                    self.assertEqual(str(source), arguments["cwd"])
                    self.write_elf(source / "target" / host / "release/wlink")
                    return subprocess.CompletedProcess(command, 0)
                self.assertEqual([str(source / "target" / host / "release/wlink"), "--version"], command)
                return subprocess.CompletedProcess(command, 0, "wlink 0.1.3\n")

            inherited = {"CARGO_BUILD_TARGET": "thumbv6m-none-eabi", "CARGO_TARGET_DIR": str(outside),
                         "TABSTUDIO_USB_FD": "3", "LD_PRELOAD": "/must-not-inherit/usb.so"}
            with patch.object(MODULE, "LOCK_SHA256", digest), patch.dict(os.environ, inherited):
                MODULE.install(SHARED, root, root / "launchers", run=run, check_output=compiler_version)
            self.assertEqual(2, len(calls))
            self.assertEqual(b"must be retained", marker.read_bytes())
            self.assertTrue((root / "bin/wlink").is_file())
            self.assertIn(str(root / "bin/wlink"), (root / "launchers/wlink").read_text())
            self.assertEqual(digest, hashlib.sha256(lock.read_bytes()).hexdigest())
            self.assertTrue((source / "LICENSE-MIT").is_file())
            self.assertFalse((source / "target").exists())

    def test_a_build_that_mutates_the_dependency_lock_is_not_published(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, lock, digest = self.fixture(root)

            def run(command, **arguments):
                self.write_elf(source / "target/x86_64-unknown-linux-gnu/release/wlink")
                lock.write_text("changed by the build")
                return subprocess.CompletedProcess(command, 0)

            with patch.object(MODULE, "LOCK_SHA256", digest), self.assertRaisesRegex(ValueError, "changed the pinned"):
                MODULE.install(SHARED, root, root / "launchers", run=run,
                               check_output=lambda *a, **k: "host: x86_64-unknown-linux-gnu\n")
            self.assertFalse((root / "bin/wlink").exists())
            self.assertFalse((root / "launchers/wlink").exists())

    def test_a_wrong_abi_build_is_refused_before_version_check_or_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, _, digest = self.fixture(root)
            calls = []

            def run(command, **arguments):
                calls.append(command)
                self.write_elf(source / "target/aarch64-unknown-linux-gnu/release/wlink", architecture=62)
                return subprocess.CompletedProcess(command, 0)

            with patch.object(MODULE, "LOCK_SHA256", digest), self.assertRaisesRegex(ValueError, "native 64-bit ELF"):
                MODULE.install(SHARED, root, root / "launchers", run=run,
                               check_output=lambda *a, **k: "host: aarch64-unknown-linux-gnu\n")
            self.assertEqual(1, len(calls))
            self.assertFalse((root / "bin/wlink").exists())
            self.assertFalse((root / "launchers/wlink").exists())


if __name__ == "__main__":
    unittest.main()
