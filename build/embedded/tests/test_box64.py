"""Host ELF/package-overlay contracts; these do not claim ARM64 translation proof."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import struct
import sys
import tempfile
import unittest

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

    def test_launcher_preserves_usb_fd_and_original_helper_search_path(self):
        text = boxed.launcher(Path("/original/unchanged/bin/arm-none-eabi-gcc"))
        self.assertIn("pass_fds=fds", text)
        self.assertIn("os.fstat(3)", text)
        self.assertIn("'/original/unchanged/bin:'", text)
        self.assertIn("subprocess._USE_POSIX_SPAWN = False", text)


if __name__ == "__main__":
    unittest.main()
