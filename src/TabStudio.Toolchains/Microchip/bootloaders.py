"""AN1388 and MCC 8-bit UART framing and bounded firmware operations.

AN1388: Microchip DS01388B; command captures are cross-checked against
sergev/pic32prog 4b4c8e5f. MCC: Microchip's PIC18F57Q43 Melody bootloader
ca2bfbbf (9-byte requests, 0x55 response synchronization).
"""
import binascii
from pathlib import Path
import struct
import time


def an1388_frame(command, payload=b""):
    body = bytes([command]) + payload
    body += struct.pack("<H", binascii.crc_hqx(body, 0))
    escaped = bytearray([1])
    for value in body:
        if value in (1, 4, 16):
            escaped.append(16)
        escaped.append(value)
    escaped.append(4)
    return bytes(escaped)


class An1388Decoder:
    def __init__(self):
        self.body = None
        self.escaped = False

    def feed(self, data):
        replies = []
        for value in data:
            if self.body is None:
                if value == 1:
                    self.body = bytearray()
                continue
            if self.escaped:
                self.body.append(value)
                self.escaped = False
            elif value == 16:
                self.escaped = True
            elif value == 1:
                self.body.clear()
            elif value == 4:
                body, self.body = bytes(self.body), None
                if len(body) < 3 or binascii.crc_hqx(body[:-2], 0) != int.from_bytes(body[-2:], "little"):
                    raise ValueError("AN1388 response CRC mismatch")
                replies.append(body[:-2])
            else:
                self.body.append(value)
            if self.body is not None and len(self.body) > 4096:
                raise ValueError("Oversized AN1388 frame")
        return replies


def mcc_request(command, address=0, length=0, data=b""):
    if not 0 <= length <= 65535 or not 0 <= address <= 0xffffffff:
        raise ValueError("Invalid MCC address or length")
    if command in (2, 5, 7) and length != len(data):
        raise ValueError("MCC write length does not match its payload")
    if command not in (2, 5, 7) and data:
        raise ValueError("This MCC request has no data payload")
    return struct.pack("<BHBBI", command, length, 0x55 if command in (2, 3, 5, 7) else 0,
                       0xaa if command in (2, 3, 5, 7) else 0, address) + data


def hex_records(path):
    records, image, base, eof = [], {}, 0, False
    if Path(path).stat().st_size > 32_000_000:
        raise ValueError("Firmware HEX file is too large")
    for number, line in enumerate(Path(path).read_text().splitlines(), 1):
        if not line.strip():
            continue
        if eof or not line.startswith(":"):
            raise ValueError(f"Invalid HEX record at line {number}")
        try:
            data = bytes.fromhex(line[1:])
        except ValueError as error:
            raise ValueError(f"Invalid HEX data at line {number}") from error
        if len(data) < 5 or len(data) != data[0] + 5 or sum(data) & 255:
            raise ValueError(f"Invalid HEX length/checksum at line {number}")
        count, kind = data[0], data[3]
        address = int.from_bytes(data[1:3], "big")
        payload = data[4:-1]
        if kind == 0:
            if address + count > 65536:
                raise ValueError("HEX data crosses a 64 KiB segment")
            for offset, value in enumerate(payload):
                absolute = base + address + offset
                if absolute in image and image[absolute] != value:
                    raise ValueError("Conflicting HEX records")
                image[absolute] = value
        elif kind == 1 and count == 0:
            eof = True
        elif kind in (2, 4) and count == 2 and address == 0:
            base = int.from_bytes(payload, "big") << (4 if kind == 2 else 16)
        elif kind in (3, 5) and count == 4:
            continue  # Execution metadata, never a flash write.
        else:
            raise ValueError("Unsupported or malformed HEX record")
        records.append(data)
    if not eof or not image:
        raise ValueError("Firmware HEX needs data and an end-of-file record")
    return records, image


def bounded_image(path, start, end):
    records, image = hex_records(path)
    if not 0 <= start < end <= 0x100000000:
        raise ValueError("Configure the bootloader's application start and exclusive end addresses, in HEX byte addresses")
    if min(image) < start or max(image) >= end:
        raise ValueError("Firmware writes outside the configured application area. Select the bootloader linker script/offset and exclude boot/config words; nothing was erased.")
    return records, image


class An1388:
    def __init__(self, transport):
        self.transport = transport
        self.decoder = An1388Decoder()

    def command(self, command, data=b"", reply=True):
        self.transport.write(an1388_frame(command, data))
        if not reply:
            return b""
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            for response in self.decoder.feed(self.transport.read(timeout=500)):
                if response[0] != command:
                    raise ValueError("Unexpected AN1388 command response")
                return response[1:]
        raise TimeoutError("AN1388 bootloader did not respond")

    def program(self, firmware, start, end):
        _, image = bounded_image(firmware, start, end)  # Validate BEFORE erase.
        version = self.command(1)
        if len(version) != 2:
            raise ValueError("Unexpected AN1388 version response")
        print(f"AN1388 {version[0]}.{version[1]}; {len(image)} application bytes", flush=True)
        if self.command(2):
            raise ValueError("Unexpected AN1388 erase response")
        upper = None
        addresses = sorted(image)
        index = 0
        while index < len(addresses):
            address = addresses[index]
            if address >> 16 != upper:
                upper = address >> 16
                self._record(bytes([2, 0, 0, 4]) + upper.to_bytes(2, "big"))
            data = bytearray()
            while index < len(addresses) and addresses[index] == address + len(data) and len(data) < 16 and addresses[index] >> 16 == upper:
                data.append(image[addresses[index]])
                index += 1
            self._record(bytes([len(data)]) + (address & 65535).to_bytes(2, "big") + b"\0" + data)
        # Verify every programmed span. Holes are included as erased 0xff bytes.
        first, last = min(image), max(image) + 1
        for address in range(first, last, 4096):
            count = min(4096, last - address)
            data = bytes(image.get(a, 255) for a in range(address, address + count))
            response = self.command(4, struct.pack("<II", address, count))
            if len(response) != 2 or int.from_bytes(response, "little") != binascii.crc_hqx(data, 0):
                raise ValueError(f"AN1388 verification failed at 0x{address:x}; application was not started")
        self.command(5, reply=False)
        print("Flash verified; application started.", flush=True)

    def _record(self, record):
        if self.command(3, record + bytes([-sum(record) & 255])):
            raise ValueError("Unexpected AN1388 programming response")


class MccUart:
    def __init__(self, transport):
        self.transport = transport
        self.pending = bytearray()

    def exchange(self, request, payload_length=1):
        self.transport.write(request)
        deadline = time.monotonic() + 10
        expected = 1 + 9 + payload_length
        while len(self.pending) < expected and time.monotonic() < deadline:
            self.pending.extend(self.transport.read(timeout=200))
            if self.pending and self.pending[0] != 0x55:
                raise ValueError("MCC response is missing its 0x55 synchronization byte; check the bootloader protocol/baud")
        if len(self.pending) < expected:
            raise TimeoutError("MCC bootloader response was truncated")
        response = bytes(self.pending[1:expected])
        del self.pending[:expected]
        if response[:9] != request[:9]:
            raise ValueError("MCC bootloader response header does not match the request")
        return response[9:]

    def program(self, firmware, start, end, page_size, unit=1):
        _, image = bounded_image(firmware, start, end)
        if unit not in (1, 2) or page_size < 2 or page_size > 4096 or page_size % unit or start % page_size or end % page_size:
            raise ValueError("Set the bootloader page size in bytes and addressUnitBytes (PIC18=1; PIC16=2); application limits must align to pages")
        version = self.exchange(mcc_request(0), 16)
        reported_page = int.from_bytes(version[10:12], "little") * unit
        if reported_page != page_size:
            raise ValueError(f"MCC bootloader reports page size {reported_page}; configured {page_size}; nothing was erased")
        print(f"MCC bootloader {version[1]}.{version[0]}; {len(image)} application bytes", flush=True)
        # One page write avoids erasing earlier rows while updating another row.
        for address in range(start, end, page_size):
            if not any(a in image for a in range(address, address + page_size)):
                continue
            data = bytes(image.get(a, 0xff if unit == 1 or a % 2 == 0 else 0x3f) for a in range(address, address + page_size))
            result = self.exchange(mcc_request(2, address // unit, len(data), data))
            if result != b"\x01":
                raise ValueError(f"MCC write failed at 0x{address:x}: status {result.hex()}")
            actual = self.exchange(mcc_request(1, address // unit, len(data)), 1 + len(data))
            if actual[:1] != b"\x01" or actual[1:] != data:
                raise ValueError(f"MCC readback verification failed at 0x{address:x}; application was not started")
        # Reset has no response in Melody; do not wait for one.
        self.transport.write(mcc_request(9))
        print("Flash verified; reset requested.", flush=True)
