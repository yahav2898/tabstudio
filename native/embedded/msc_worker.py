"""Permissioned RP ROM USB Mass Storage BOT/SCSI worker; never opens a kernel disk.

USB MSC Bulk-Only Transport rev 1.0 (usb.org usbmassbulk_10.pdf). Only LUN 0,
INQUIRY, READ CAPACITY(10), READ(10), WRITE(10). No format, reset, mode-select,
arbitrary CDB or retry of a possibly transmitted write is exposed by the RPC.
"""
import base64
import json
import struct
import sys

from usb_device import WrappedUsb, UsbError


class BotError(UsbError):
    def __init__(self, message, phase="command", data_transferred=False,
                 disconnected=False, fatal=True):
        super().__init__(message)
        self.phase = phase
        self.data_transferred = data_transferred
        self.disconnected = disconnected
        self.fatal = fatal


class BulkOnlyScsi:
    """One serial BOT command at a time on one explicitly selected mass-storage interface."""
    def __init__(self, usb):
        self.usb = usb
        self.tag = 0
        self.capacity = None
        descriptor = usb.device
        if descriptor.get("vendorId") != 0x2e8a or descriptor.get("productId") not in (3, 15):
            raise BotError("Only standard RP2040/RP2350 ROM BOOTSEL USB identities are permitted")
        candidates = [i for i in descriptor.get("interfaces", [])
                      if (i.get("class"), i.get("subclass"), i.get("protocol")) == (8, 6, 0x50)]
        if len(candidates) != 1:
            raise BotError("Exactly one SCSI Bulk-Only mass-storage interface is required")
        self.interface = candidates[0]
        endpoints = [e for e in self.interface["endpoints"] if e["type"] == 2]
        incoming = [e for e in endpoints if e["address"] & 0x80]
        outgoing = [e for e in endpoints if not e["address"] & 0x80]
        if len(incoming) != 1 or len(outgoing) != 1:
            raise BotError("The BOT interface must have one bulk endpoint in each direction")
        self.input, self.output = incoming[0], outgoing[0]
        usb.claim(self.interface["number"])

    def command(self, cdb, read_length=0, payload=None):
        if not 1 <= len(cdb) <= 16 or read_length < 0 or read_length > 32768:
            raise ValueError("Invalid bounded SCSI command")
        if payload is not None and (read_length or len(payload) > 32768):
            raise ValueError("SCSI command has conflicting data directions")
        length = read_length if payload is None else len(payload)
        self.tag = self.tag % 0xffffffff + 1
        cbw = struct.pack("<IIIBBB16s", 0x43425355, self.tag, length,
                          0x80 if read_length else 0, 0, len(cdb), bytes(cdb))
        phase, transferred = "command", False
        try:
            self.usb.transfer(self.output, data=cbw, timeout=3000)
            data = b""
            phase = "data"
            if read_length:
                data = self.usb.transfer(self.input, length=read_length, timeout=3000)
                if len(data) != read_length:
                    raise BotError("Short SCSI data phase", phase=phase)
            elif payload is not None and length:
                self.usb.transfer(self.output, data=payload, timeout=3000)
                transferred = True
            phase = "status"
            csw = self.usb.transfer(self.input, length=13, timeout=3000)
            if len(csw) != 13:
                raise BotError("Missing complete SCSI command status", phase=phase,
                               data_transferred=transferred)
            signature, tag, residue, status = struct.unpack("<IIIB", csw)
            if signature != 0x53425355 or tag != self.tag or residue > length or status > 2:
                raise BotError("Invalid CSW signature/tag/residue/status", phase=phase,
                               data_transferred=transferred)
            if status == 2:
                raise BotError("SCSI phase error; reconnect before retrying", phase=phase,
                               data_transferred=transferred)
            if status == 1:
                raise BotError("The target refused this SCSI command", phase=phase,
                               data_transferred=transferred, fatal=False)
            if residue:
                raise BotError("Incomplete SCSI command transfer", phase=phase,
                               data_transferred=transferred)
            return data
        except BotError:
            raise
        except UsbError as error:
            # libusb NO_DEVICE is the only disconnection classified here. Timeouts are not reboot proof.
            raise BotError(str(error), phase, transferred,
                           "libusb error -4." in str(error), True) from error

    def initialize(self):
        inquiry = self.command(bytes([0x12, 0, 0, 0, 36, 0]), read_length=36)
        if inquiry[0] != 0:
            raise BotError("The selected ROM LUN is not a direct-access block device")
        capacity = self.command(bytes([0x25]) + bytes(9), read_length=8)
        last, size = struct.unpack(">II", capacity)
        if last == 0xffffffff or size != 512 or last == 0:
            raise BotError("Only bounded READ(10) 512-byte-sector BOOTSEL capacity is supported")
        self.capacity = last + 1
        return self.capacity, inquiry[8:36].decode("ascii", errors="replace").strip()

    def read(self, sector, count):
        self.bounds(sector, count)
        cdb = struct.pack(">BBIBHB", 0x28, 0, sector, 0, count, 0)
        return self.command(cdb, read_length=count * 512)

    def write(self, sector, payload):
        if not payload or len(payload) % 512:
            raise ValueError("WRITE(10) requires complete nonempty sectors")
        self.bounds(sector, len(payload) // 512)
        cdb = struct.pack(">BBIBHB", 0x2a, 0, sector, 0, len(payload) // 512, 0)
        self.command(cdb, payload=payload)

    def bounds(self, sector, count):
        if (type(sector) is not int or type(count) is not int or sector < 0 or
                not 1 <= count <= 64 or self.capacity is None or sector + count > self.capacity):
            raise ValueError("Block transfer exceeds the actual selected LUN capacity")


def response(value):
    print(json.dumps(value, separators=(",", ":")), flush=True)


def serve(usb, input_stream=None):
    input_stream = input_stream or sys.stdin.buffer
    scsi = BulkOnlyScsi(usb)
    count, inquiry = scsi.initialize()
    response({"ok": True, "sectorSize": 512, "sectorCount": count,
              "vendorId": usb.device["vendorId"], "productId": usb.device["productId"],
              "inquiry": inquiry})
    writable = False
    while True:
        line = input_stream.readline(65537)
        if not line:
            return
        request_id = None
        try:
            if len(line) > 65536 or not line.endswith(b"\n"):
                raise BotError("Block request exceeds its complete-line bound")
            request = json.loads(line)
            if not isinstance(request, dict):
                raise BotError("A block request must be a JSON object")
            request_id = request.get("id")
            if type(request_id) is not int or not 1 <= request_id <= 0x7fffffff:
                raise BotError("Invalid block request ID")
            operation = request.get("op")
            extra = {}
            if operation == "enable":
                family = request.get("family")
                allowed = (0xe48bff56,) if usb.device["productId"] == 3 else (0xe48bff59, 0xe48bff5a, 0xe48bff5b)
                if request.get("guard") != "rp-rom-uf2-validated" or type(family) is not int or family not in allowed:
                    raise BotError("UF2/volume preflight authorization is missing")
                writable = True
            elif operation == "read":
                extra["data"] = base64.b64encode(scsi.read(request.get("sector"), request.get("count"))).decode("ascii")
            elif operation == "write":
                if not writable:
                    raise BotError("The block worker is read-only until FAT/INFO_UF2/application-range validation")
                payload = base64.b64decode(request.get("data", ""), validate=True)
                scsi.write(request.get("sector"), payload)
            else:
                raise BotError("Unknown block operation; arbitrary SCSI commands are not exposed")
            response({"ok": True, "id": request_id, **extra})
        except (UsbError, ValueError, KeyError, TypeError) as error:
            fatal = getattr(error, "fatal", True)
            response({"ok": False, "id": request_id, "error": str(error), "fatal": fatal,
                      "phase": getattr(error, "phase", "request"),
                      "dataTransferred": getattr(error, "data_transferred", False),
                      "disconnected": getattr(error, "disconnected", False)})
            if fatal:
                return


def main():
    try:
        with WrappedUsb() as usb:
            serve(usb)
    except (UsbError, ValueError, KeyError, TypeError) as error:
        response({"ok": False, "error": str(error), "fatal": True})
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
