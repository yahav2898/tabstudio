"""Resolved SCons metadata fixtures, not hardware/protocol recordings."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("esp_export", ROOT / "native/embedded/export_platformio_esp.py")
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


class Environment(dict):
    def __init__(self, directory, chip, boot_offset):
        super().__init__(PIOFRAMEWORK=["arduino"])
        self.directory = directory
        self.chip = chip
        self.values = {"$UPLOAD_PROTOCOL": "esptool", "$ESP32_APP_OFFSET": "0x10000",
                       "$BUILD_DIR": str(directory), "$BUILD_DIR/${PROGNAME}.bin": str(directory / "firmware.bin")}
        (directory / "bootloader.bin").write_bytes(b"\xe9\x00bootloader")
        # A real ESP-IDF partition record: bytes AA 50, type app, subtype factory, offset 0x10000, size 1 MiB, label.
        record = b"\xaa\x50\x00\x00" + (0x10000).to_bytes(4, "little") + (0x100000).to_bytes(4, "little") + b"factory".ljust(16, b"\0") + bytes(4)
        (directory / "partitions.bin").write_bytes(record + b"\xff" * (0xC00 - len(record)))
        (directory / "firmware.bin").write_bytes(b"\xe9\x00application")
        self.flags = ["--chip", chip, "--port", "", "--baud", "115200", "--before", "default_reset", "--after", "hard_reset", "write_flash", "-z",
                      "--flash_mode", "dio", "--flash_freq", "40m", "--flash_size", "4MB",
                      boot_offset, str(directory / "bootloader.bin"), "0x8000", str(directory / "partitions.bin")]

    def subst(self, value):
        return self.values[value]

    def subst_list(self, value):
        assert value == "$UPLOADERFLAGS"
        return [self.flags]


class ExportTests(unittest.TestCase):
    def test_resolved_esp32_c3_s3_offsets_and_chip_are_exported_without_defaults_or_padding(self):
        for chip, boot_offset in (("esp32", "0x1000"), ("esp32c3", "0x0"), ("esp32s3", "0x0")):
            with self.subTest(chip=chip), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                env = Environment(directory, chip, boot_offset)
                exporter.export_manifest([], [], env)
                manifest = json.loads((directory / "tabstudio-flasher-args.json").read_text())
                self.assertEqual(chip, manifest["extra_esptool_args"]["chip"])
                self.assertEqual({boot_offset, "0x8000", "0x10000"}, set(manifest["flash_files"]))
                self.assertEqual(3, len(list((directory / "tabstudio-flash-images").iterdir())))
                self.assertEqual((directory / "firmware.bin").read_bytes(), (directory / manifest["app"]["file"]).read_bytes())
                self.assertFalse(any("merge" in file.name for file in directory.iterdir()))

    def test_encryption_force_and_custom_upload_operations_cannot_be_exported(self):
        for unsafe in ("--encrypt", "--force", "--erase-all", "--encrypt-files"):
            with self.subTest(unsafe=unsafe), tempfile.TemporaryDirectory() as temporary:
                env = Environment(Path(temporary), "esp32c3", "0x0")
                env.flags.insert(0, unsafe)
                with self.assertRaises(ValueError):
                    exporter.export_manifest([], [], env)
                self.assertFalse((Path(temporary) / "tabstudio-flasher-args.json").exists())

    def test_custom_transport_missing_mode_and_ambiguous_images_fail_visibly(self):
        for fault in ("transport", "mode", "image"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                env = Environment(Path(temporary), "esp32c3", "0x0")
                if fault == "transport":
                    env.values["$UPLOAD_PROTOCOL"] = "custom"
                elif fault == "mode":
                    at = env.flags.index("--flash_mode")
                    del env.flags[at:at + 2]
                else:
                    (Path(temporary) / "partitions.bin").write_bytes(b"\xe9\x00another bootloader")
                with self.assertRaises((KeyError, ValueError)):
                    exporter.export_manifest([], [], env)
                self.assertFalse((Path(temporary) / "tabstudio-flasher-args.json").exists())


if __name__ == "__main__":
    unittest.main()
