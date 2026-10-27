"""Synthetic UART streams through the actual PTY/control worker and receive-only client.

No stream is labelled a physical board capture. Hardware boot/reset behavior is unverified.
"""
import importlib.util
import json
import os
from pathlib import Path
import select
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "src/TabStudio.Toolchains/Microchip"))


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bridge_module = load("serial_unity_production_bridge", ROOT / "native/embedded/serial_bridge.py")
usb_module = load("serial_unity_production_driver", ROOT / "src/TabStudio.Toolchains/Microchip/usb_serial.py")


@unittest.skipUnless(sys.platform == "linux", "The actual Linux PTY/native shim is required")
class SerialUnityContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="ts-unity-native-")
        cls.shim = Path(cls.scratch.name) / "serial-posix.so"
        subprocess.run(["cc", "-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-fPIC", "-shared",
                        str(ROOT / "native/embedded/serial_posix.c"), "-o", str(cls.shim), "-ldl", "-pthread"], check=True)

    @classmethod
    def tearDownClass(cls):
        cls.scratch.cleanup()

    def capture(self, payload, baud=230400):
        class Peer(bridge_module.VirtualSerial):
            def __init__(self):
                super().__init__(baud)
                self.transmitted = []
                self.line_changes = []
                self.break_changes = []

            def write(self, data):
                self.transmitted.append(bytes(data))
                raise OSError("The receive-only runner must not transmit")

            def set_lines(self, dtr, rts):
                self.line_changes.append((dtr, rts))

            def set_break(self, enabled):
                self.break_changes.append(enabled)

        peer = Peer()
        with tempfile.TemporaryDirectory(prefix="ts-unity-audit-") as scratch:
            audit = Path(scratch) / "controls.jsonl"
            bridge = bridge_module.SerialBridge(peer, str(audit), preserve_lines=True)
            bridge.start()
            for offset in range(0, len(payload), 31):
                peer.data.put(payload[offset:offset + 31])
            environment = dict(os.environ, LD_PRELOAD=str(self.shim),
                               TABSTUDIO_SERIAL_DEVICE=bridge.slave_name,
                               TABSTUDIO_SERIAL_CONTROL_DIR=bridge.directory.name)
            try:
                result = subprocess.run([sys.executable, str(ROOT / "native/embedded/serial_unity_reader.py"),
                                         "--port", bridge.slave_name, "--baud", str(baud), "--timeout", "1"],
                                        env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=5)
                self.assertIsNone(bridge.error)
                events = [json.loads(line) for line in audit.read_text().splitlines()]
            finally:
                bridge.close()
            self.assertEqual([], peer.transmitted)
            self.assertEqual([], peer.line_changes)
            self.assertEqual([], peer.break_changes)
            self.assertFalse(any(event["operation"] in (bridge_module.LINES, bridge_module.BREAK) for event in events))
            self.assertTrue(any(event["operation"] == bridge_module.CONFIG and event["values"][0] == baud for event in events))
            return result

    def test_full_stream_and_split_utf8_are_received_without_any_transmit_reset_or_cleanup_pulse(self):
        data = ("\n".join("test_sensör.c:%d:test_%d:PASS" % (index + 1, index) for index in range(1200))
                + "\n-----------------------\n1200 Tests 0 Failures 0 Ignored\nOK\n").encode()
        result = self.capture(data)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(data, result.stdout)

    def test_real_assertion_failure_is_a_completed_capture_and_remains_a_failure_line(self):
        data = b"test.c:9:test_fault:FAIL:Expected 42 Was 0\n1 Tests 1 Failures 0 Ignored\nFAILED\n"
        result = self.capture(data)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(data, result.stdout)

    def test_arbitrary_firmware_baud_survives_hupcl_cleanup_configuration_without_named_rate_rounding(self):
        data = b"test.c:1:custom_baud:PASS\n1 Tests 0 Failures 0 Ignored\nOK\n"
        result = self.capture(data, baud=123456)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(data, result.stdout)

    def test_truncated_missing_contradictory_and_invalid_utf8_runs_never_complete_successfully(self):
        for payload in (b"test.c:1:test_a:PASS\n", b"test.c:1:test_a:PASS\n1 Tests 0 Failures 0 Ignored\nOK",
                        b"test.c:1:test_a:FAIL:x\n1 Tests 1 Failures 0 Ignored\nOK\n",
                        b"test.c:1:test_a:PASS\xff\n", b"0 Tests 0 Failures 0 Ignored\nOK\n"):
            with self.subTest(payload=payload):
                result = self.capture(payload)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(b"capture failed", result.stderr)

    def test_actual_worker_disconnect_stops_the_reader_and_removes_its_private_alias(self):
        code = """
import importlib.util,sys,time
spec=importlib.util.spec_from_file_location('actual_serial_worker',sys.argv[1])
worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
class Detached(worker.VirtualSerial):
    def __init__(self,baud):
        super().__init__(baud);self.first=True
    def read(self,length=4096,timeout=200):
        time.sleep(0.2)
        if self.first:
            self.first=False;return b'test.c:1:unfinished:PASS\\n'
        raise OSError('synthetic USB device detached')
worker.VirtualSerial=Detached
sys.argv=sys.argv[1:]
raise SystemExit(worker.main())
"""
        with tempfile.TemporaryDirectory(prefix="ts-unity-disconnect-") as scratch:
            alias = Path(scratch) / "board-port"
            result = subprocess.run([sys.executable, "-c", code, str(ROOT / "native/embedded/serial_bridge.py"),
                                     "--virtual", "--preserve-lines", "--shim", str(self.shim), "--port", str(alias),
                                     "--", sys.executable, str(ROOT / "native/embedded/serial_unity_reader.py"),
                                     "--port", "{port}", "--baud", "115200", "--timeout", "5"],
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
            self.assertNotEqual(0, result.returncode)
            self.assertIn(b"synthetic USB device detached", result.stderr)
            self.assertFalse(os.path.lexists(alias))

    def test_stop_of_the_actual_worker_reaps_capture_child_and_removes_the_alias(self):
        with tempfile.TemporaryDirectory(prefix="ts-unity-stop-") as scratch:
            alias = Path(scratch) / "board-port"
            process = subprocess.Popen([sys.executable, str(ROOT / "native/embedded/serial_bridge.py"),
                                        "--virtual", "--preserve-lines", "--shim", str(self.shim), "--port", str(alias),
                                        "--", sys.executable, str(ROOT / "native/embedded/serial_unity_reader.py"),
                                        "--port", "{port}", "--baud", "115200", "--timeout", "30"],
                                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                self.assertTrue(select.select([process.stderr], [], [], 3)[0], "The actual capture client never became ready")
                self.assertIn(b"Serial Unity capture ready", process.stderr.readline())
                self.assertTrue(alias.is_symlink())
                process.terminate()
                process.communicate(timeout=5)
                self.assertNotEqual(0, process.returncode)
                self.assertFalse(os.path.lexists(alias))
            finally:
                if process.poll() is None:
                    process.kill()
                    process.communicate()


class PreserveLinesDriverContracts(unittest.TestCase):
    def test_all_existing_drivers_skip_explicit_modem_reset_and_handshake_requests_in_capture_mode(self):
        class Usb:
            def __init__(self, vid, pid):
                self.device = {"vendorId": vid, "productId": pid, "interfaces": [{"number": 0, "class": 2, "subclass": 2}]}
                self.requests = []

            def endpoints(self, kind, interface):
                return {"number": 1, "class": 10}, {"packetSize": 64}, {"packetSize": 64}

            def claim(self, number):
                pass

            def control(self, kind, request, value=0, index=0, data=None, length=0):
                self.requests.append((kind, request))
                return bytes(length)

        for vid, pid, forbidden in ((0x2341, 0x0043, {(0x21, 0x22)}),
                                    (0x1a86, 0x7523, {(0x40, 0xa4)}),
                                    (0x10c4, 0xea60, {(0x41, 7), (0x41, 0x13)}),
                                    (0x0403, 0x6001, {(0x40, 0), (0x40, 1), (0x40, 2)})):
            with self.subTest(vid=vid):
                usb = Usb(vid, pid)
                serial = usb_module.UsbSerial(usb, 115200, preserve_lines=True)
                self.assertEqual(115200, serial.baud)
                self.assertFalse(forbidden.intersection(usb.requests), usb.requests)


if __name__ == "__main__":
    unittest.main()
