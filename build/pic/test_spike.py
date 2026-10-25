"""Tests for the compiler spike harness; no proprietary downloads needed."""
import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("spike", Path(__file__).with_name("spike.py"))
spike = importlib.util.module_from_spec(spec)
spec.loader.exec_module(spike)


class SpikeTests(unittest.TestCase):
    def test_cached_download_is_verified_again_before_use(self):
        with tempfile.TemporaryDirectory() as folder:
            cache = Path(folder)
            (cache / "asset").write_bytes(b"modified")
            asset = {"sha256": hashlib.sha256(b"original").hexdigest()}
            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                spike.fetch(asset, cache, "asset")

    def test_extracts_debian_xz_and_gzip_members_from_a_pipe(self):
        for extension, mode in [("xz", "w:xz"), ("gz", "w:gz")]:
            with self.subTest(extension=extension), tempfile.TemporaryDirectory() as folder:
                work = Path(folder)
                archive = work / f"data.tar.{extension}"
                with tarfile.open(archive, mode) as tar:
                    entry = tarfile.TarInfo("usr/share/example.txt")
                    entry.size = 5
                    tar.addfile(entry, io.BytesIO(b"hello"))
                package = work / "example.deb"
                subprocess.run(["ar", "r", str(package), str(archive)], check=True, capture_output=True)
                dest = work / "root"
                dest.mkdir()
                spike.unpack_deb(package, dest)
                self.assertEqual(b"hello", (dest / "usr/share/example.txt").read_bytes())

    def test_no_acceptance_means_no_download_or_execution(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(spike, "prepare") as prepare:
            args = argparse.Namespace(compiler="xc8", work=Path(folder) / "work",
                                      output=Path(folder) / "out", accept_microchip_license=False)
            with self.assertRaisesRegex(ValueError, "Read the Microchip license"):
                spike.spike(args)
            prepare.assert_not_called()
            report = json.loads((args.output / "result.json").read_text())
            self.assertFalse(report["success"])
            self.assertEqual([], report["compileSeconds"])

    def test_all_non_alpine_downloads_have_sha256_pins(self):
        for name, asset in spike.LOCK.items():
            with self.subTest(asset=name):
                self.assertRegex(asset["sha256"], r"^[0-9a-f]{64}$")
                self.assertTrue(asset["url"].startswith("https://"))
                self.assertTrue(asset["version"])


if __name__ == "__main__":
    unittest.main()
