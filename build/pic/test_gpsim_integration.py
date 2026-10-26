"""Run explicitly against native gpsim and XC8's supported-model firmware in CI."""
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

origin = Path(__file__).resolve()
if len(origin.parents) > 2:
    source = origin.parents[2] / "src/TabStudio.Toolchains/Microchip"
    if source.is_dir(): sys.path.insert(0, str(source))
from gpsim_dap import Adapter, Gpsim, SourceMap, read_message


class DapFramingTests(unittest.TestCase):
    def test_binary_length_handles_unicode_and_consecutive_messages(self):
        output = io.BytesIO()
        adapter = Adapter(output)
        adapter.event("output", {"output": "PIC שלום\n"})
        adapter.event("initialized")
        output.seek(0)
        self.assertEqual("PIC שלום\n", read_message(output)["body"]["output"])
        self.assertEqual("initialized", read_message(output)["event"])
        self.assertIsNone(read_message(output))

    def test_oversize_and_truncated_frames_are_rejected(self):
        for data in (b"Content-Length: -1\r\n\r\n", b"Content-Length: 4000001\r\n\r\n", b"Content-Length: 2\r\n\r\n{"):
            with self.assertRaises((ValueError, EOFError)): read_message(io.BytesIO(data))


@unittest.skipUnless(os.environ.get("PIC_TEST_GPSIM") and os.environ.get("PIC_TEST_ELF"), "Native integration runs explicitly in the ARM64 Alpine/PRoot job")
class NativeGpsimTests(unittest.TestCase):
    def setUp(self):
        self.output = io.BytesIO()
        self.adapter = Adapter(self.output)
        self.elf = Path(os.environ["PIC_TEST_ELF"])
        self.adapter.launch({"device": "PIC18F14K22", "program": str(self.elf), "cwd": str(self.elf.parent), "gpsim": os.environ["PIC_TEST_GPSIM"]})

    def tearDown(self):
        self.adapter.close()

    def test_xc8_dwarf_breakpoint_and_native_cpu_step(self):
        source = self.elf.with_suffix(".c")
        found = self.adapter.sources.breakpoint(source, 8)
        self.assertIsNotNone(found, "XC8's real ELF must map the counter increment to source")
        self.adapter.handle({"seq": 1, "command": "setBreakpoints", "arguments": {"source": {"path": str(source)}, "breakpoints": [{"line": 8}]}})
        self.assertTrue(self.adapter.break_ids)
        self.adapter.continue_run()
        self.adapter.worker.join(timeout=10)
        self.assertFalse(self.adapter.worker.is_alive(), "Execution must stop at the XC8 DWARF source breakpoint")
        self.assertEqual(found[0], self.adapter.pc())
        before = self.adapter.pc()
        self.adapter.handle({"seq": 2, "command": "next", "arguments": {"threadId": 1, "granularity": "instruction"}})
        self.assertNotEqual(before, self.adapter.pc())

    def test_registers_sfrs_and_dap_evaluation_come_from_gpsim(self):
        self.adapter.handle({"seq": 1, "command": "variables", "arguments": {"variablesReference": 1}})
        self.adapter.handle({"seq": 2, "command": "variables", "arguments": {"variablesReference": 2, "count": 8}})
        self.adapter.handle({"seq": 3, "command": "evaluate", "arguments": {"expression": "pc"}})
        self.output.seek(0)
        messages = []
        while message := read_message(self.output): messages.append(message)
        variables = messages[0]["body"]["variables"]
        self.assertTrue(any(v["name"] == "pc" and v["value"].startswith("0x") for v in variables))
        self.assertGreater(len(messages[1]["body"]["variables"]), 0)
        self.assertTrue(messages[2]["body"]["result"].startswith("0x"))
        with self.assertRaisesRegex(ValueError, "arbitrary"):
            self.adapter.handle({"seq": 4, "command": "evaluate", "arguments": {"expression": "quit\n"}})

    def test_queries_while_running_leave_the_pause_request_responsive(self):
        self.adapter.continue_run()
        with self.assertRaisesRegex(ValueError, "Pause gpsim"):
            self.adapter.handle({"seq": 1, "command": "setBreakpoints", "arguments": {"source": {"path": str(self.elf.with_suffix('.c'))}, "breakpoints": [{"line": 8}]}})
        with self.assertRaisesRegex(ValueError, "Pause gpsim"):
            self.adapter.handle({"seq": 2, "command": "variables", "arguments": {"variablesReference": 1}})
        self.adapter.handle({"seq": 3, "command": "pause"})
        self.adapter.worker.join(timeout=10)
        self.assertFalse(self.adapter.worker.is_alive())
        self.assertFalse(self.adapter.running)

    def test_step_out_uses_the_actual_pic18_hardware_return_stack(self):
        # PIC18 CALL 0x8; BRA .; NOP; MOVLW 1; RETURN. Byte addresses,
        # deliberately independent of compiler source-line placement.
        payload = bytes.fromhex("04ec00f0ffd70000010e1200")
        record = bytes([len(payload), 0, 0, 0]) + payload
        with tempfile.TemporaryDirectory() as folder:
            firmware = Path(folder) / "call.hex"
            firmware.write_text(":" + record.hex() + f"{-sum(record) & 255:02x}" + "\n:00000001FF\n")
            self.adapter.close()
            self.adapter.launch({"device": "PIC18F14K22", "program": str(firmware), "gpsim": os.environ["PIC_TEST_GPSIM"]})
            self.adapter.instruction_breakpoints = [8]
            self.adapter.install_breakpoints()
            self.adapter.continue_run()
            self.adapter.worker.join(timeout=10)
            self.assertEqual(8, self.adapter.pc())
            self.assertEqual(1, int(self.adapter.value("stkptr"), 0) & 31)
            self.adapter.handle({"seq": 1, "command": "stepOut", "arguments": {"threadId": 1}})
            self.adapter.worker.join(timeout=10)
            self.assertEqual(4, self.adapter.pc())
            self.assertEqual(0, int(self.adapter.value("stkptr"), 0) & 31)


if __name__ == "__main__":
    unittest.main(verbosity=2)
