"""Installer behavior with explicit fake native processes; real builds run separately in CI."""
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]


def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "native/embedded" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class NativeInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.module = load("install_native_client")

    def test_native_package_layout_is_discovered_without_guessing_subfolder(self):
        binary = self.root / "arbitrary-official-layout" / "picotool"
        binary.parent.mkdir()
        binary.write_bytes(b"\x7fELFtest")
        calls = []

        def run(command, **arguments):
            calls.append((command, arguments))
            return subprocess.CompletedProcess(command, 0, "picotool v2.3.1\n")

        with patch.dict(os.environ, {"LD_PRELOAD": "/bad.so", "TABSTUDIO_USB_FD": "3"}):
            self.module.install(self.root, "picotool", "2.3.1", self.root / "bin", run)
        self.assertEqual([str(binary), "version"], calls[0][0])
        self.assertNotIn("LD_PRELOAD", calls[0][1]["env"])
        self.assertNotIn("TABSTUDIO_USB_FD", calls[0][1]["env"])
        wrapper = (self.root / "bin/picotool").read_text()
        self.assertIn(repr(str(binary)), wrapper)
        self.assertIn("os.execv", wrapper)
        self.assertNotIn("shell", wrapper)

    def test_wrong_version_creates_no_path_alias(self):
        (self.root / "esptool").write_bytes(b"\x7fELFfake")
        run = lambda command, **arguments: subprocess.CompletedProcess(command, 0, "esptool v5.6.01")
        with self.assertRaises(ValueError):
            self.module.install(self.root, "esptool", "5.6.0", self.root / "bin", run)
        self.assertFalse((self.root / "bin/esptool").exists())

    def test_two_executables_or_outside_symlink_are_not_arbitrarily_selected(self):
        for folder in ("one", "two"):
            directory = self.root / folder
            directory.mkdir()
            (directory / "picotool").write_bytes(b"\x7fELFfake")
        with self.assertRaises(ValueError):
            self.module.locate(self.root, "picotool")
        with tempfile.TemporaryDirectory() as outside:
            external = Path(outside) / "esptool"
            external.write_bytes(b"\x7fELFfake")
            (self.root / "esptool").symlink_to(external)
            with self.assertRaises(ValueError):
                self.module.locate(self.root, "esptool")

    def test_both_esptool_aliases_point_at_same_verified_executable(self):
        binary = self.root / "esptool"
        binary.write_bytes(b"\x7fELFfake")
        run = lambda command, **arguments: subprocess.CompletedProcess(command, 0, "esptool v5.6.0")
        self.module.install(self.root, "esptool", "5.6.0", self.root / "bin", run)
        self.assertEqual((self.root / "bin/esptool").read_bytes(), (self.root / "bin/esptool.py").read_bytes())


class SourceInstallerTests(unittest.TestCase):
    def test_recipes_use_locked_native_dependencies_and_disable_vendored_usb(self):
        module = load("install_source_client")
        self.assertIn("--locked", module.RECIPES["wchisp"]["command"])
        self.assertIn("--no-default-features", module.RECIPES["wchisp"]["command"])
        self.assertIn("VERSION=1.9.1", module.RECIPES["bossac"]["command"])
        self.assertIn("WITHOUT_READLINE=1", module.RECIPES["mspdebug"]["command"])
        self.assertEqual(("--list-mcus",), module.RECIPES["teensy_loader_cli"]["check"])

    def test_source_build_preserves_sources_and_exposes_only_validated_binary(self):
        module = load("install_source_client")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "BOSSA-1.9.1"
            (source / "src").mkdir(parents=True)
            (source / "src/bossac.cpp").write_text("// upstream source")
            (source / "bin").mkdir()
            (source / "bin/bossac").write_bytes(b"\x7fELFfake")
            calls = []

            def run(command, **arguments):
                calls.append((command, arguments))
                if command[0] == "make":
                    return subprocess.CompletedProcess(command, 0)
                return subprocess.CompletedProcess(command, 1, "Basic Open Source SAM-BA Application (BOSSA) Version 1.9.1")

            with patch.dict(os.environ, {"CXXFLAGS": "-DPACKAGING_FLAGS"}):
                module.install(root, "bossac", "1.9.1", root / "aliases", run)
            self.assertTrue((source / "src/bossac.cpp").exists())
            self.assertTrue((root / "bin/bossac").exists())
            self.assertTrue((root / "aliases/bossac").exists())
            self.assertEqual("make", calls[0][0][0])
            self.assertNotIn("LD_PRELOAD", calls[0][1]["env"])
            self.assertEqual("-DPACKAGING_FLAGS -include sys/select.h -include sys/time.h", calls[0][1]["env"]["CXXFLAGS"])


if __name__ == "__main__":
    unittest.main()
