"""Synthetic BOT/SCSI contracts, not USB captures or physical RP flashing evidence."""
import base64
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import unittest

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "src/TabStudio.Toolchains/Microchip"))
SPEC = importlib.util.spec_from_file_location("msc_worker", ROOT / "native/embedded/msc_worker.py")
msc = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(msc)


class Usb:
    def __init__(self):
        self.device = {"vendorId": 0x2e8a, "productId": 3, "interfaces": [
            {"number": 0, "class": 8, "subclass": 6, "protocol": 0x50,
             "endpoints": [{"address": 0x81, "type": 2}, {"address": 2, "type": 2}]}]}
        self.claimed = []
        self.commands = []
        self.sectors = {}
        self.pending = None
        self.short_data = False
        self.bad_signature = False
        self.bad_tag = False
        self.residue = 0
        self.status = 0
        self.disconnect_status = False

    def claim(self, number):
        self.claimed.append(number)

    def transfer(self, endpoint, data=None, length=4096, timeout=500):
        if not endpoint["address"] & 0x80:
            if self.pending is None:
                assert len(data) == 31
                sig, tag, size, flags, lun, cdb_length, cdb = struct.unpack("<IIIBBB16s", data)
                assert sig == 0x43425355 and lun == 0 and cdb_length in (6, 10)
                assert all(x == 0 for x in cdb[cdb_length:])
                self.commands.append((tag, size, flags, bytes(cdb[:cdb_length])))
                self.pending = {"tag": tag, "length": size, "cdb": cdb, "data_sent": False}
                if cdb[0] == 0x12:
                    assert cdb[:6] == bytes([0x12, 0, 0, 0, 36, 0]) and flags == 0x80 and size == 36
                    self.pending["response"] = bytes([0, 0, 2, 2, 31, 0, 0, 0]) + b"RPI     RP2             1.00"
                elif cdb[0] == 0x25:
                    assert flags == 0x80 and size == 8 and cdb[1:10] == bytes(9)
                    self.pending["response"] = struct.pack(">II", 99, 512)
                elif cdb[0] in (0x28, 0x2a):
                    opcode, command_flags, sector, group, count, control = struct.unpack(">BBIBHB", cdb[:10])
                    assert command_flags == group == control == 0 and size == count * 512
                    assert flags == (0x80 if opcode == 0x28 else 0)
                    self.pending.update(sector=sector, count=count)
                    if opcode == 0x28:
                        self.pending["response"] = b"".join(self.sectors.get(i, bytes(512)) for i in range(sector, sector + count))
                else:
                    raise AssertionError("Unexpected arbitrary SCSI opcode")
            else:
                assert self.pending["cdb"][0] == 0x2a and len(data) == self.pending["length"]
                for i in range(self.pending["count"]):
                    self.sectors[self.pending["sector"] + i] = bytes(data[i * 512:(i + 1) * 512])
                self.pending["data_sent"] = True
            return len(data)
        pending = self.pending
        if "response" in pending and not pending["data_sent"]:
            pending["data_sent"] = True
            assert length == len(pending["response"])
            return pending["response"][:-1] if self.short_data else pending["response"]
        assert length == 13
        if self.disconnect_status:
            raise msc.UsbError("Cannot transfer USB data: libusb error -4. Reconnect the device.")
        self.pending = None
        return struct.pack("<IIIB", 0 if self.bad_signature else 0x53425355,
                           pending["tag"] + (1 if self.bad_tag else 0), self.residue, self.status)


class BotTests(unittest.TestCase):
    def test_inquiry_capacity_read_and_write_use_exact_cbw_csw_and_scsi_byte_order(self):
        usb = Usb()
        transport = msc.BulkOnlyScsi(usb)
        count, inquiry = transport.initialize()
        self.assertEqual(100, count)
        self.assertIn("RPI", inquiry)
        payload = bytes(range(256)) * 2
        transport.write(7, payload)
        self.assertEqual(payload, transport.read(7, 1))
        self.assertEqual([1, 2, 3, 4], [command[0] for command in usb.commands])
        self.assertEqual(bytes([0x2a, 0, 0, 0, 0, 7, 0, 0, 1, 0]), usb.commands[2][3])
        self.assertEqual([0], usb.claimed)

    def test_non_rom_identity_wrong_interface_and_unbounded_transfers_are_refused(self):
        usb = Usb(); usb.device["vendorId"] = 0x1234
        with self.assertRaises(msc.BotError): msc.BulkOnlyScsi(usb)
        self.assertEqual([], usb.claimed)
        usb = Usb(); usb.device["interfaces"][0]["protocol"] = 0x62
        with self.assertRaises(msc.BotError): msc.BulkOnlyScsi(usb)
        usb = Usb(); transport = msc.BulkOnlyScsi(usb); transport.initialize()
        for sector, count in [(-1, 1), (99, 2), (0, 65), (True, 1), (0, 0)]:
            with self.subTest(sector=sector, count=count), self.assertRaises(ValueError):
                transport.read(sector, count)
        self.assertEqual(2, len(usb.commands))

    def test_short_data_bad_csw_tag_signature_residue_and_phase_fail_closed(self):
        for field, value in [("short_data", True), ("bad_tag", True), ("bad_signature", True), ("residue", 1), ("status", 2)]:
            with self.subTest(field=field):
                usb = Usb(); transport = msc.BulkOnlyScsi(usb); transport.initialize()
                setattr(usb, field, value)
                with self.assertRaises(msc.BotError) as caught: transport.read(1, 1)
                self.assertTrue(caught.exception.fatal)

    def test_final_status_disconnect_records_data_submission_without_claiming_success(self):
        usb = Usb(); transport = msc.BulkOnlyScsi(usb); transport.initialize()
        usb.disconnect_status = True
        with self.assertRaises(msc.BotError) as caught: transport.write(4, bytes(512))
        self.assertEqual("status", caught.exception.phase)
        self.assertTrue(caught.exception.data_transferred)
        self.assertTrue(caught.exception.disconnected)

    def test_rpc_rejects_unvalidated_write_without_sending_write_command(self):
        usb = Usb()
        command = {"id": 1, "op": "write", "sector": 10, "data": base64.b64encode(bytes(512)).decode()}
        output = io.StringIO()
        with contextlib.redirect_stdout(output): msc.serve(usb, io.BytesIO((json.dumps(command) + "\n").encode()))
        replies = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertTrue(replies[0]["ok"])
        self.assertFalse(replies[1]["ok"])
        self.assertEqual([0x12, 0x25], [command[3][0] for command in usb.commands])

    def test_rpc_enables_only_matching_family_and_never_exposes_format_or_arbitrary_cdb(self):
        for command in [{"op": "enable", "guard": "rp-rom-uf2-validated", "family": 0xe48bff59},
                        {"op": "format"}, {"op": "scsi", "cdb": [4]}]:
            usb = Usb(); command["id"] = 1; output = io.StringIO()
            with contextlib.redirect_stdout(output): msc.serve(usb, io.BytesIO((json.dumps(command) + "\n").encode()))
            self.assertFalse(json.loads(output.getvalue().splitlines()[-1])["ok"])
            self.assertEqual(2, len(usb.commands))

    def test_rpc_readback_is_actual_storage_bytes_after_explicit_guard(self):
        usb = Usb(); payload = bytes([0xA5]) * 512
        commands = [{"id": 1, "op": "enable", "guard": "rp-rom-uf2-validated", "family": 0xe48bff56},
                    {"id": 2, "op": "write", "sector": 7, "data": base64.b64encode(payload).decode()},
                    {"id": 3, "op": "read", "sector": 7, "count": 1}]
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            msc.serve(usb, io.BytesIO("".join(json.dumps(c) + "\n" for c in commands).encode()))
        replies = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertTrue(all(reply["ok"] for reply in replies))
        self.assertEqual(payload, base64.b64decode(replies[-1]["data"]))


if __name__ == "__main__":
    unittest.main()
