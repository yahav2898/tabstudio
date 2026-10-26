"""USB serial with runtime line coding using only a permissioned libusb handle.

Control sequences follow usb-serial-for-android v3.9.0 (MIT). No driver is
installed and no tty device node is needed. Standard CDC-ACM, CH340/341,
CP210x and FTDI FT232/2232/4232 interfaces are handled here.
"""
import struct
from usb_device import UsbError


def ftdi_baud(baud):
    if not 183 <= baud <= 3_500_000:
        raise ValueError("FTDI baud must be from 183 to 3500000")
    if baud >= 2_500_000:
        divisor, fraction, effective = 0, 0, 3_000_000
    elif baud >= 1_750_000:
        divisor, fraction, effective = 1, 0, 2_000_000
    else:
        eighths = ((48_000_000 // baud) + 1) >> 1
        divisor, fraction = eighths >> 3, eighths & 7
        effective = ((48_000_000 // eighths) + 1) >> 1
    if abs(1 - effective / baud) >= 0.031:
        raise ValueError("FTDI baud deviation exceeds 3 percent")
    encoded = (0, 3, 2, 4, 1, 5, 6, 7)[fraction]
    return divisor | ((encoded & 3) << 14), encoded >> 2


def ch340_baud(baud):
    if baud == 921600:
        factor, divisor = 0xf300, 7
    else:
        factor, divisor = 1532620800 // baud, 3
        while factor > 0xfff0 and divisor > 0:
            factor >>= 3
            divisor -= 1
        if factor > 0xfff0 or factor < 1:
            raise ValueError("Unsupported CH340 baud rate")
        factor = 0x10000 - factor
    return (factor & 0xff00) | divisor | 0x80, factor & 0xff


class UsbSerial:
    def __init__(self, usb, baud=115200, interface=None, dtr=False, rts=False):
        if not 1 <= baud <= 3_500_000:
            raise ValueError("Invalid serial baud rate")
        self.usb = usb
        self.item, self.rx, self.tx = usb.endpoints(2, interface)
        self.port = self.item["number"]
        self.ftdi = False
        self.kind = "cdc"
        self.baud, self.data_bits, self.stop_bits, self.parity = baud, 8, 1, 0
        self.dtr, self.rts, self.break_enabled = bool(dtr), bool(rts), False
        vid, pid = usb.device["vendorId"], usb.device["productId"]
        control = usb.control
        if vid == 0x1a86 and pid in (0x7523, 0x5523):
            self.kind = "ch340"
            version = control(0xc0, 0x5f, length=2)
            if len(version) != 2:
                raise UsbError("CH340 version response was truncated")
            control(0x40, 0xa1)
            self._ch_baud(9600)
            control(0xc0, 0x95, 0x2518, length=2)
            control(0x40, 0x9a, 0x2518, 0xc3)
            control(0xc0, 0x95, 0x0706, length=2)
            control(0x40, 0xa1, 0x501f, 0xd90a)
            self._ch_baud(baud)
            control(0x40, 0xa4, ~((0x20 if dtr else 0) | (0x40 if rts else 0)))
        elif vid == 0x10c4 and pid in (0xea60, 0xea70, 0xea71):
            self.kind = "cp210x"
            control(0x41, 0, 1, self.port)
            control(0x41, 0x1e, index=self.port, data=struct.pack("<I", baud))
            control(0x41, 3, 0x0800, self.port)
            control(0x41, 7, 0x0300 | int(dtr) | (int(rts) << 1), self.port)
            # No hardware handshake: DTR/RTS stay under explicit user control.
            control(0x41, 0x13, index=self.port, data=struct.pack("<IIII", 1 if dtr else 0, 0x40 if rts else 0, 0, 0))
        elif vid == 0x0403 and pid in (0x6001, 0x6010, 0x6011, 0x6014, 0x6015):
            self.kind = "ftdi"
            self.ftdi = True
            control(0x40, 0, index=self.port + 1)
            value, index = ftdi_baud(baud)
            # Multi-interface and FT232H/X encode channel in the low index byte.
            if pid != 0x6001:
                index = (index << 8) | (self.port + 1)
            control(0x40, 3, value, index)
            control(0x40, 4, 8, self.port + 1)
            control(0x40, 2, 0, self.port + 1)
            control(0x40, 1, 0x0300 | int(dtr) | (int(rts) << 1), self.port + 1)
        else:
            communication = max((i for i in usb.device["interfaces"] if i["class"] == 2 and i["subclass"] == 2 and i["number"] <= self.port), key=lambda i: i["number"], default=None)
            if communication is None or self.item["class"] not in (2, 10):
                raise UsbError("Unsupported USB serial adapter; use CDC-ACM, CH340/341, CP210x or FTDI")
            usb.claim(communication["number"])
            self.control_port = communication["number"]
            control(0x21, 0x20, index=communication["number"], data=struct.pack("<IBBB", baud, 0, 0, 8))
            control(0x21, 0x22, int(dtr) | (int(rts) << 1), communication["number"])

    def configure(self, baud, data_bits=8, stop_bits=1, parity=0):
        """Change line coding without reopening the permissioned USB connection.

        Parity uses the USB CDC encoding: none=0, odd=1, even=2, mark=3,
        space=4. Stop bits are 1 or 2; flow control stays under host control.
        """
        if not 1 <= baud <= 3_500_000 or data_bits not in (5, 6, 7, 8) or stop_bits not in (1, 2) or parity not in range(5):
            raise ValueError("Unsupported USB serial line coding")
        control = self.usb.control
        if self.kind == "ch340":
            self._ch_baud(baud)
            line = 0xc0 | (data_bits - 5) | (4 if stop_bits == 2 else 0) | (0, 8, 0x18, 0x28, 0x38)[parity]
            control(0x40, 0x9a, 0x2518, line)
        elif self.kind == "cp210x":
            control(0x41, 0x1e, index=self.port, data=struct.pack("<I", baud))
            control(0x41, 3, (data_bits << 8) | (parity << 4) | (2 if stop_bits == 2 else 0), self.port)
        elif self.kind == "ftdi":
            value, index = ftdi_baud(baud)
            if self.usb.device["productId"] != 0x6001:
                index = (index << 8) | (self.port + 1)
            control(0x40, 3, value, index)
            control(0x40, 4, data_bits | (parity << 8) | ((2 if stop_bits == 2 else 0) << 11) | (0x4000 if self.break_enabled else 0), self.port + 1)
        else:
            control(0x21, 0x20, index=self.control_port, data=struct.pack("<IBBB", baud, 2 if stop_bits == 2 else 0, parity, data_bits))
        self.baud, self.data_bits, self.stop_bits, self.parity = baud, data_bits, stop_bits, parity

    def set_lines(self, dtr, rts):
        """Set both modem outputs in one USB request, preserving reset timing."""
        dtr, rts = bool(dtr), bool(rts)
        control = self.usb.control
        if self.kind == "ch340":
            control(0x40, 0xa4, ~((0x20 if dtr else 0) | (0x40 if rts else 0)))
        elif self.kind == "cp210x":
            control(0x41, 7, 0x0300 | int(dtr) | (int(rts) << 1), self.port)
        elif self.kind == "ftdi":
            # FTDI's RTS value is bit 1, with independent write-enable bits.
            control(0x40, 1, 0x0300 | int(dtr) | (int(rts) << 1), self.port + 1)
        else:
            control(0x21, 0x22, int(dtr) | (int(rts) << 1), self.control_port)
        self.dtr, self.rts = dtr, rts

    def set_break(self, enabled):
        """Assert/deassert BREAK. The caller owns its duration."""
        enabled = bool(enabled)
        control = self.usb.control
        if self.kind == "ch340":
            state = bytearray(control(0xc0, 0x95, 0x1805, length=2))
            if len(state) != 2:
                raise UsbError("Truncated CH340 BREAK response")
            state[0] = (state[0] & ~1) if enabled else (state[0] | 1)
            state[1] = (state[1] & ~0x40) if enabled else (state[1] | 0x40)
            control(0x40, 0x9a, 0x1805, state[0] | (state[1] << 8))
        elif self.kind == "cp210x":
            control(0x41, 5, int(enabled), self.port)
        elif self.kind == "ftdi":
            control(0x40, 4, self.data_bits | (self.parity << 8) | ((2 if self.stop_bits == 2 else 0) << 11) | (0x4000 if enabled else 0), self.port + 1)
        else:
            control(0x21, 0x23, 0xffff if enabled else 0, self.control_port)
        self.break_enabled = enabled

    def modem_bits(self):
        """Linux TIOCM bits; CDC has no synchronous input-status request."""
        result = (2 if self.dtr else 0) | (4 if self.rts else 0)
        if self.kind == "cdc":
            return result
        if self.kind == "ch340":
            data = self.usb.control(0xc0, 0x95, 0x0706, length=2)
            if len(data) != 2:
                raise UsbError("Truncated CH340 modem status")
            flags = ~data[0]
            source = (1, 2, 4, 8)
        elif self.kind == "cp210x":
            data = self.usb.control(0xc1, 8, index=self.port, length=1)
            if len(data) != 1:
                raise UsbError("Truncated CP210x modem status")
            flags, source = data[0], (0x10, 0x20, 0x40, 0x80)
        else:
            data = self.usb.control(0xc0, 5, index=self.port + 1, length=2)
            if len(data) != 2:
                raise UsbError("Truncated FTDI modem status")
            flags, source = data[0], (0x10, 0x20, 0x40, 0x80)
        for bit, target in zip(source, (0x20, 0x100, 0x80, 0x40)):
            if flags & bit:
                result |= target
        return result

    def _ch_baud(self, baud):
        first, second = ch340_baud(baud)
        self.usb.control(0x40, 0x9a, 0x1312, first)
        self.usb.control(0x40, 0x9a, 0x0f2c, second)

    def read(self, length=4096, timeout=200):
        packet = self.rx["packetSize"]
        # FTDI prepends two status bytes to EACH USB packet, including short ones.
        size = max(packet, ((length + packet - 1) // packet) * packet)
        data = self.usb.transfer(self.rx, length=size, timeout=timeout)
        if self.ftdi:
            data = b"".join(data[i + 2:min(i + packet, len(data))] for i in range(0, len(data), packet))
        return data

    def write(self, data):
        return self.usb.transfer(self.tx, data=data, timeout=3000)
