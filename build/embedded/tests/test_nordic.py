"""Synthetic installer/hash/identity guards; real original CLI execution has a separate proof fixture."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "native/embedded"))
import nordic_runtime
import install_nordic_client as installer


class NordicGuards(unittest.TestCase):
    def source(self, folder):
        folder.mkdir()
        (folder / "manifest.json").write_text(json.dumps({"name": "nrfutil-nrf5sdk-tools", "version": "1.1.0", "platform": "x86_64-unknown-linux-gnu"}))
        data = folder / "data"
        binary = data / nordic_runtime.BINARY_RELATIVE
        binary.parent.mkdir(parents=True)
        binary.write_bytes(b"explicitly synthetic fixture; never executed")
        license_file = data / "share/nrfutil-nrf5sdk-tools/LICENSE"
        license_file.parent.mkdir(parents=True)
        license_file.write_text("LicenseRef-Nordic-1-Clause\nSynthetic marker, not vendor license content")
        return binary

    def test_manifest_version_platform_and_name_must_match_before_install(self):
        with tempfile.TemporaryDirectory() as scratch:
            folder = Path(scratch) / "vendor"
            self.source(folder)
            for key, value in [("name", "unrelated"), ("version", "latest"), ("platform", "aarch64-unknown-linux-gnu")]:
                manifest = {"name": "nrfutil-nrf5sdk-tools", "version": "1.1.0", "platform": "x86_64-unknown-linux-gnu", key: value}
                (folder / "manifest.json").write_text(json.dumps(manifest))
                with self.subTest(key=key), self.assertRaisesRegex(ValueError, "identity/version/platform"):
                    installer.checked_source(folder)

    def test_correct_identity_never_bypasses_real_original_binary_hash(self):
        with tempfile.TemporaryDirectory() as scratch:
            folder = Path(scratch) / "vendor"
            self.source(folder)
            with self.assertRaisesRegex(ValueError, "reviewed SHA-256"):
                installer.checked_source(folder)

    def test_links_are_refused_instead_of_reading_outside_verified_data(self):
        with tempfile.TemporaryDirectory() as scratch:
            folder = Path(scratch) / "vendor"
            self.source(folder)
            (folder / "data/escape").symlink_to(Path(scratch))
            with self.assertRaisesRegex(ValueError, "regular files"):
                installer.checked_source(folder)

    def test_license_and_second_digest_are_required_even_after_archive_inspection(self):
        with tempfile.TemporaryDirectory() as scratch:
            folder = Path(scratch) / "vendor"
            binary = self.source(folder)
            fixture_hash = hashlib.sha256(binary.read_bytes()).hexdigest()
            with patch.object(nordic_runtime, "BINARY_SHA256", fixture_hash):
                self.assertEqual(folder / "data", installer.checked_source(folder))
                (folder / "data/share/nrfutil-nrf5sdk-tools/LICENSE").write_text("wrong license")
                with self.assertRaisesRegex(ValueError, "vendor license"):
                    installer.checked_source(folder)
            with self.assertRaisesRegex(ValueError, "changed after"):
                nordic_runtime.command(["version"], prefix=folder / "data", architecture="x86_64")

    def test_arm_never_silently_executes_x64_without_the_specific_box64_route(self):
        with tempfile.TemporaryDirectory() as scratch:
            folder = Path(scratch) / "vendor"
            binary = self.source(folder)
            with patch.object(nordic_runtime, "BINARY_SHA256", hashlib.sha256(binary.read_bytes()).hexdigest()), patch.dict("os.environ", {}, clear=True):
                with self.assertRaisesRegex(ValueError, "explicit ARM64 Box64 route"):
                    nordic_runtime.command(["version"], prefix=folder / "data", architecture="aarch64")
                with self.assertRaisesRegex(ValueError, "requires x64"):
                    nordic_runtime.command(["version"], prefix=folder / "data", architecture="armv7l")


if __name__ == "__main__":
    unittest.main()
