"""Actual Store installer/project code, exercised without downloads or execution."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]


class InstallationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.folder = Path(self.temporary.name)
        for name in ["install.py", "project.py", "packs.lock.json"]:
            shutil.copyfile(REPO / "src/TabStudio.Toolchains/Microchip" / name, self.folder / name)
        shutil.copyfile(REPO / "build/pic/assets.lock.json", self.folder / "assets.lock.json")
        spec = importlib.util.spec_from_file_location("pic_install_test", self.folder / "install.py")
        self.install = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.install)
        self.old_install = sys.modules.get("install")
        sys.modules["install"] = self.install
        spec = importlib.util.spec_from_file_location("pic_project_test", self.folder / "project.py")
        self.project = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.project)

    def tearDown(self):
        if self.old_install is None:
            sys.modules.pop("install", None)
        else:
            sys.modules["install"] = self.old_install
        self.temporary.cleanup()

    def test_rejected_license_prevents_downloads_and_execution(self):
        with patch.object(self.install, "download") as download, patch.object(self.install, "execute") as execute:
            with self.assertRaisesRegex(ValueError, "Accept Microchip"):
                self.install.install("xc8", False)
            download.assert_not_called()
            execute.assert_not_called()

    def test_modified_cached_asset_is_rejected_and_removed(self):
        self.install.CACHE.mkdir()
        file = self.install.CACHE / "compiler.run"
        file.write_bytes(b"modified")
        with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
            self.install.download({"url": "https://example.invalid/compiler.run", "sha256": hashlib.sha256(b"original").hexdigest()})
        self.assertFalse(file.exists())

    def test_store_debian_reader_extracts_the_xz_member_without_host_ar(self):
        data = io.BytesIO()
        with tarfile.open(fileobj=data, mode="w:xz") as archive:
            entry = tarfile.TarInfo("usr/lib/example.txt")
            entry.size = 5
            archive.addfile(entry, io.BytesIO(b"hello"))
        payload = data.getvalue()
        header = f'{"data.tar.xz/":<16}{0:<12}{0:<6}{0:<6}{"100644":<8}{len(payload):<10}`\n'.encode("ascii")
        package = self.folder / "runtime.deb"
        package.write_bytes(b"!<arch>\n" + header + payload + (b"\n" if len(payload) % 2 else b""))
        root = self.folder / "root"
        root.mkdir()
        self.install.extract_deb(package, root)
        self.assertEqual(b"hello", (root / "usr/lib/example.txt").read_bytes())

    def test_exact_pack_version_mismatch_never_downloads_another_pack(self):
        with patch.object(self.install, "download") as download:
            with self.assertRaises(LookupError):
                self.install.ensure_pack("PIC18F45K22", ("Microchip", "PIC18F-K_DFP", "0.0.1"))
            download.assert_not_called()

    def test_mplab_build_uses_its_generated_makefile_and_preserves_user_compilation_database(self):
        root = self.folder / "Blink.X"
        (root / "nbproject").mkdir(parents=True)
        (root / "nbproject/configurations.xml").write_text('''<configurationDescriptor>
<logicalFolder><itemPath>main.c</itemPath></logicalFolder><confs><conf name="production">
<toolsSet><targetDevice>PIC18F45K22</targetDevice><languageToolchain>XC8</languageToolchain><languageToolchainVersion>4.00</languageToolchainVersion></toolsSet>
<HI-TECH-COMP><property key="define-macros" value="BOARD=1;LED=0"/></HI-TECH-COMP>
</conf></confs></configurationDescriptor>''')
        makefile = root / "nbproject/Makefile-production.mk"
        makefile.write_text("# Custom options and per-file steps belong to MPLAB's generated makefile\nSPECIAL=-O2 -msmart-io=1\n")
        (root / "compile_commands.json").write_text("user-owned database")
        (self.folder / "xc8.json").write_text(json.dumps({"version": "4.00", "compilerRoot": "/opt/microchip/xc8/v4.00"}))
        with patch.object(self.project, "ensure_pack", return_value=({"name": "PIC18F-K_DFP", "version": "1.17.312"}, Path("/packs/device"))), patch.object(self.project.subprocess, "call", return_value=0) as call:
            self.assertEqual(0, self.project.build(root))
        command = call.call_args.args[0]
        self.assertEqual(["make", "-f", "nbproject/Makefile-production.mk"], command[:3])
        self.assertIn(".build-conf", command)
        self.assertIn("DFP_DIR=/packs/device/xc8", command)
        self.assertIn("SPECIAL=-O2 -msmart-io=1", makefile.read_text())
        self.assertEqual("user-owned database", (root / "compile_commands.json").read_text())
        database = json.loads((root / ".tabstudio/pic/compile_commands.json").read_text())
        self.assertIn("-DBOARD=1", database[0]["arguments"])

    def test_mplab_descriptor_refuses_external_entities(self):
        root = self.folder / "bad"
        (root / "nbproject").mkdir(parents=True)
        (root / "nbproject/configurations.xml").write_text('<!DOCTYPE config SYSTEM "file:///etc/passwd"><configurationDescriptor/>')
        with self.assertRaisesRegex(ValueError, "Invalid"):
            self.project.descriptor(root)


if __name__ == "__main__":
    unittest.main()
