"""Host ELF/package-overlay contracts; these do not claim ARM64 translation proof."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import struct
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "native/embedded"))
import box64_runtime
import platformio_box64 as boxed


class BoxedPackageTests(unittest.TestCase):
    def test_original_gcc_driver_and_helpers_keep_paths_and_bytes(self):
        if os.uname().machine != "x86_64":
            self.skipTest("This contract checks unchanged native x86 binaries; ARM64 has a separate translator proof")
        with tempfile.TemporaryDirectory() as temporary:
            package = Path(temporary) / "toolchain"
            (package / "bin").mkdir(parents=True)
            source = package / "helper.c"
            source.write_text('#include <stdio.h>\nint main(void){puts("ORIGINAL_X86_EXECUTED");return 0;}\n')
            subprocess.run(["cc", str(source), "-o", str(package / "bin/tool")], check=True)
            subprocess.run(["cc", "-shared", "-fPIC", str(source), "-o", str(package / "libexample.so")], check=True)
            library = package / "libexample.so"
            library.chmod(0o755)
            digest = hashlib.sha256((package / "bin/tool").read_bytes()).hexdigest()
            self.assertTrue(boxed.x86_executable(package / "bin/tool"))
            self.assertFalse(boxed.x86_executable(library))
            boxed.overlay(package)
            manifest = json.loads((package / ".tabstudio-box64-overlay.json").read_text())
            original = Path(manifest["original"])
            self.assertEqual(digest, hashlib.sha256((original / "bin/tool").read_bytes()).hexdigest())
            self.assertTrue((package / "libexample.so").is_symlink())
            environment = {**os.environ, "PYTHONPATH": str(ROOT / "native/embedded")}
            result = subprocess.run([str(package / "bin/tool")], env=environment, text=True, capture_output=True, check=True)
            self.assertIn("ORIGINAL_X86_EXECUTED", result.stdout)
            boxed.overlay(package)
            self.assertEqual(digest, hashlib.sha256((original / "bin/tool").read_bytes()).hexdigest())

    def test_private_arm_loader_matches_runtime_and_never_replaces_system_loader(self):
        arguments, environment = box64_runtime.command("/original/bin/gcc", ["-v"], "aarch64", Path("/private"))
        self.assertEqual(["/private/box64", "/original/bin/gcc", "-v"], arguments)
        self.assertEqual("/private/runtime/amd64/usr/lib/x86_64-linux-gnu", environment["BOX64_LD_LIBRARY_PATH"])
        self.assertEqual("0", environment["BOX64_DYNAREC"])

    def test_only_translator_interpreter_bytes_change_and_long_replacements_are_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            binary = Path(temporary) / "box64"
            header = bytearray(64)
            header[:7] = b"\x7fELF\x02\x01\x01"
            struct.pack_into("<H", header, 18, 183)
            struct.pack_into("<Q", header, 32, 64)
            struct.pack_into("<HH", header, 54, 56, 1)
            entry = struct.pack("<IIQQQQQQ", 3, 4, 120, 0, 0, 27, 27, 1)
            original = bytes(header) + entry + b"/lib/ld-linux-aarch64.so.1\0" + b"UNCHANGED_CODE"
            binary.write_bytes(original)
            box64_runtime.set_translator_loader(binary, Path("/opt/tabstudio/box64/ld.so"))
            changed = binary.read_bytes()
            self.assertEqual(original[:120], changed[:120])
            self.assertEqual(original[147:], changed[147:])
            self.assertEqual(b"/opt/tabstudio/box64/ld.so\0", changed[120:147])
            with self.assertRaises(ValueError):
                box64_runtime.set_translator_loader(binary, Path("/a/" + "x" * 100))

    def test_native_x64_command_has_no_translation(self):
        arguments, _ = box64_runtime.command("/original/gcc", ["-c", "a file.c"], "x86_64")
        self.assertEqual(["/original/gcc", "-c", "a file.c"], arguments)

    def test_boxed_serial_child_has_only_guest_preload_and_parent_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temporary:
            shim = Path(temporary) / "serial-x64.so"
            header = bytearray(64)
            header[:7] = b"\x7fELF\x02\x01\x01"
            header[16:20] = b"\x03\x00\x3e\x00"
            shim.write_bytes(header)
            with patch.dict(os.environ, {"LD_PRELOAD": "/native-arm.so", "BOX64_LD_PRELOAD": "/old-x64.so"}):
                _, environment = box64_runtime.command("/original/client", [], "aarch64", Path("/private"), serial_shim=shim)
                self.assertNotIn("LD_PRELOAD", environment)
                self.assertEqual(str(shim), environment["BOX64_LD_PRELOAD"])
                self.assertEqual("/native-arm.so", os.environ["LD_PRELOAD"])
                _, environment = box64_runtime.command("/original/client", [], "x86_64", serial_shim=shim)
                self.assertEqual(str(shim), environment["LD_PRELOAD"])
                self.assertNotIn("BOX64_LD_PRELOAD", environment)
            header[18:20] = b"\xb7\x00"
            shim.write_bytes(header)
            with self.assertRaises(ValueError):
                box64_runtime.command("/client", serial_shim=shim)

    def test_launcher_preserves_usb_fd_and_original_helper_search_path(self):
        text = boxed.launcher(Path("/original/unchanged/bin/arm-none-eabi-gcc"))
        self.assertIn("pass_fds=fds", text)
        self.assertIn("os.fstat(3)", text)
        self.assertIn("'/original/unchanged/bin:'", text)
        self.assertIn("subprocess._USE_POSIX_SPAWN = False", text)

    def test_package_update_keeps_previous_original_and_never_reuses_its_binary(self):
        if os.uname().machine != "x86_64":
            self.skipTest("Native x86 executable fixture")
        with tempfile.TemporaryDirectory() as temporary:
            package = Path(temporary) / "toolchain"
            package.mkdir()
            shutil.copy2("/bin/true", package / "tool")
            boxed.overlay(package)
            first = Path(json.loads((package / ".tabstudio-box64-overlay.json").read_text())["original"])
            previous = (first / "tool").read_bytes()
            shutil.rmtree(package)
            package.mkdir()
            shutil.copy2("/bin/echo", package / "tool")
            boxed.overlay(package)
            second = Path(json.loads((package / ".tabstudio-box64-overlay.json").read_text())["original"])
            self.assertNotEqual(first, second)
            self.assertEqual(previous, (first / "tool").read_bytes())
            self.assertEqual(Path("/bin/echo").read_bytes(), (second / "tool").read_bytes())


if __name__ == "__main__":
    unittest.main()
