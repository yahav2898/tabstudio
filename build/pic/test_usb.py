"""Protocol recordings and serial framing; no physical programmer is implied."""
import importlib.util
import json
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "src/TabStudio.Toolchains/Microchip"))
from bootloaders import An1388, An1388Decoder, MccUart, an1388_frame, mcc_request, hex_records
from usb_serial import UsbSerial, ch340_baud, ftdi_baud


class An1388Recording(An1388):
    """Protocol peer with the reference CRC for bytes 00..0f, independent of our encoder."""
    def __init__(self, crc=b"\x3d\x51"):
        super().__init__(None)
        self.calls = []
        self.crc = crc

    def command(self, command, data=b"", reply=True):
        self.calls.append((command, data, reply))
        return b"\x01\x00" if command == 1 else self.crc if command == 4 else b""


AN1388_FIRMWARE = ":020000041D00DD\n:10100000000102030405060708090A0B0C0D0E0F68\n:00000001FF\n"


class UsbProtocolTests(unittest.TestCase):
    def test_recorded_reference_encoder_frames(self):
        fixtures = json.loads((ROOT / "build/pic/fixtures/bootloader-frames.json").read_text())
        self.assertFalse(fixtures["provenance"]["hardwareCapture"])
        for frame in fixtures["frames"]:
            with self.subTest(frame=frame["name"]):
                payload = bytes.fromhex(frame["payload"])
                actual = an1388_frame(frame["command"], payload) if frame["protocol"] == "an1388" else mcc_request(frame["command"], frame["address"], frame["length"], payload)
                self.assertEqual(bytes.fromhex(frame["request"]), actual)

    def test_an1388_decoder_handles_every_split_and_escaped_control_byte(self):
        expected = b"\x03\x01\x04\x10\xff"
        recording = bytes.fromhex("0103100110041010ff255904")
        for split in range(len(recording) + 1):
            decoder = An1388Decoder()
            self.assertEqual([expected], decoder.feed(recording[:split]) + decoder.feed(recording[split:]))

    def test_an1388_rejects_bad_crc(self):
        with self.assertRaisesRegex(ValueError, "CRC"):
            An1388Decoder().feed(bytes.fromhex("01100122101004"))

    def test_an1388_writes_physical_hex_and_verifies_the_reference_virtual_address(self):
        fixtures = json.loads((ROOT / "build/pic/fixtures/bootloader-frames.json").read_text())
        reference = next(f for f in fixtures["frames"] if f["name"] == "read-crc-virtual-address")
        bootloader = An1388Recording()
        with tempfile.TemporaryDirectory() as folder:
            firmware = Path(folder) / "application.hex"
            firmware.write_text(AN1388_FIRMWARE)
            bootloader.program(firmware, 0x1d001000, 0x1d002000)
        self.assertIn((3, bytes.fromhex("020000041d00dd"), True), bootloader.calls)
        self.assertIn((4, bytes.fromhex(reference["payload"]), True), bootloader.calls)
        self.assertEqual((5, b"", False), bootloader.calls[-1])

    def test_an1388_does_not_start_the_application_after_failed_verification(self):
        bootloader = An1388Recording(crc=b"\x00\x00")
        with tempfile.TemporaryDirectory() as folder:
            firmware = Path(folder) / "application.hex"
            firmware.write_text(AN1388_FIRMWARE)
            with self.assertRaisesRegex(ValueError, "verification failed"):
                bootloader.program(firmware, 0x1d001000, 0x1d002000)
        self.assertFalse(any(command == 5 for command, _, _ in bootloader.calls))

    def test_mcc_response_uses_stx_and_echoes_the_nine_byte_header(self):
        class Recording:
            def __init__(self): self.writes = []
            def write(self, data): self.written = data; self.writes.append(data)
            def read(self, timeout):
                data, self.data = self.data[:2], self.data[2:]
                return data
        transport = Recording()
        transport.data = bytes.fromhex("5502040055aa0030000001")
        request = bytes.fromhex("02040055aa00300000010410ff")
        self.assertEqual(b"\x01", MccUart(transport).exchange(request))
        self.assertEqual(request, transport.written)
        self.assertEqual([b"\x55", request], transport.writes)

    def test_fixed_baud_mcc_variant_does_not_receive_an_autobaud_character(self):
        class Recording:
            def __init__(self): self.writes = []
            def write(self, data): self.writes.append(data)
        transport = Recording()
        request = mcc_request(9)
        MccUart(transport, auto_baud=False).send(request)
        self.assertEqual([request], transport.writes)

    def test_mcc_write_length_is_checked(self):
        with self.assertRaisesRegex(ValueError, "length"):
            mcc_request(2, 0x3000, 2, b"one")

    def test_out_of_application_firmware_is_rejected_before_any_erase_or_usb_write(self):
        class NoWrite:
            def write(self, data): raise AssertionError("USB must not be touched")
        with tempfile.TemporaryDirectory() as folder:
            firmware = Path(folder) / "bad.hex"
            firmware.write_text(":0400000001020304F2\n:00000001FF\n")
            with self.assertRaisesRegex(ValueError, "outside"):
                An1388(NoWrite()).program(firmware, 0x1000, 0x2000)

    def test_hex_parser_rejects_conflicts_and_truncated_or_bad_checksums(self):
        with tempfile.TemporaryDirectory() as folder:
            firmware = Path(folder) / "bad.hex"
            for text in (":0400000001020304F1\n:00000001FF\n", ":0400000001020304F2\n", ":0400000001020304F2\n:0100000005FA\n:00000001FF\n"):
                firmware.write_text(text)
                with self.assertRaises(ValueError): hex_records(firmware)

    def test_serial_baud_control_values_match_reference_drivers(self):
        self.assertEqual((0x4138, 0), ftdi_baud(9600))
        self.assertEqual((26, 0), ftdi_baud(115200))
        self.assertEqual((0xb282, 0x0c), ch340_baud(9600))
        self.assertEqual((0xcc83, 0x08), ch340_baud(115200))

    def test_ftdi_removes_status_bytes_from_each_packet_including_short_packets(self):
        class FakeUsb:
            device = {"vendorId": 0x0403, "productId": 0x6001}
            def endpoints(self, *args): return {"number": 0}, {"packetSize": 8}, {}
            def control(self, *args, **kwargs): return 0
            def transfer(self, *args, **kwargs): return b"\x01\x60abcdef\x01\x60XYZ"
        self.assertEqual(b"abcdefXYZ", UsbSerial(FakeUsb()).read())

    def test_second_cdc_port_controls_its_own_communication_interface(self):
        class CompositeUsb:
            device = {"vendorId": 0x1234, "productId": 0x5678, "interfaces": [
                {"number": 0, "class": 2, "subclass": 2}, {"number": 2, "class": 2, "subclass": 2}]}
            def __init__(self): self.claimed = []; self.control_interfaces = []
            def endpoints(self, kind, interface): return {"number": 3, "class": 10}, {}, {}
            def claim(self, number): self.claimed.append(number)
            def control(self, kind, request, value=0, index=0, **kwargs): self.control_interfaces.append(index)
        usb = CompositeUsb()
        UsbSerial(usb, interface=3)
        self.assertEqual([2], usb.claimed)
        self.assertEqual([2, 2], usb.control_interfaces)

    def test_emulator_executes_an_apk_library_path_containing_equals(self):
        spec = importlib.util.spec_from_file_location("android_pic_test", ROOT / "build/pic/android_spike.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            native = Path(folder) / "APK with spaces==/lib"
            native.mkdir(parents=True)
            program = native / "libproot.so"
            program.write_text('#!/bin/sh\nprintf \'%s\\n\' "$PROOT_LOADER" "$1"\n')
            program.chmod(0o755)
            command = module.guest_command(str(native), folder, [str(program), "--kill-on-exit"])
            result = subprocess.check_output(["/bin/sh", "-c", command], text=True)
            self.assertEqual([str(native / "libproot-loader.so"), "--kill-on-exit"], result.splitlines())

    def test_emulator_finds_native_libraries_across_android_package_dump_formats(self):
        spec = importlib.util.spec_from_file_location("android_pic_test", ROOT / "build/pic/android_spike.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        self.assertEqual("/data/app/pkg/lib/x86_64", module.native_library_directory("legacyNativeLibraryDir=/data/app/pkg/lib/x86_64", "", "x86_64"))
        self.assertEqual("/data/app/pkg/lib/x86_64", module.native_library_directory("legacyNativeLibraryDir=/data/app/pkg/lib", "", "x86_64"))
        self.assertEqual("/data/app/pkg/lib/x86_64", module.native_library_directory("", "package:/data/app/pkg/base.apk", "x86_64"))
        with self.assertRaises(RuntimeError): module.native_library_directory("", "", "unknown")


if __name__ == "__main__":
    unittest.main()
