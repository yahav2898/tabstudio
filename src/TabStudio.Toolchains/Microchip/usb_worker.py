"""Android permissioned USB worker: serial monitor and PIC bootloaders."""
import argparse
import json
from pathlib import Path
import select
import sys
import subprocess

from bootloaders import An1388, MccUart
from usb_device import WrappedUsb
from usb_serial import UsbSerial


class HidTransport:
    def __init__(self, usb, interface=None):
        self.usb = usb
        self.item, self.rx, self.tx = usb.endpoints(3, interface)
        if self.item["class"] != 3 or self.tx["packetSize"] != 64:
            raise ValueError("AN1388 requires a 64-byte HID bootloader interface")

    def write(self, data):
        if len(data) > 64:
            raise ValueError("AN1388 HID request exceeds one report")
        return self.usb.transfer(self.tx, data=data.ljust(64, b"\x04"), timeout=3000)

    def read(self, timeout=500):
        return self.usb.transfer(self.rx, length=64, timeout=timeout)


def monitor(serial):
    print("USB serial connected (8-N-1). Disconnect to release the device.", file=sys.stderr, flush=True)
    while True:
        if select.select([sys.stdin.buffer], [], [], 0)[0]:
            data = sys.stdin.buffer.read1(4096)
            if not data:
                return
            serial.write(data)
        data = serial.read(timeout=100)
        if data:
            sys.stdout.buffer.write(data)
            sys.stdout.buffer.flush()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["serial", "flash"])
    parser.add_argument("--baud", type=int, default=115200)
    parser.add_argument("--interface", type=int)
    parser.add_argument("--dtr", action="store_true")
    parser.add_argument("--rts", action="store_true")
    parser.add_argument("--profile", type=Path)
    args = parser.parse_args()
    if args.command == "serial":
        with WrappedUsb() as usb:
            monitor(UsbSerial(usb, args.baud, args.interface, args.dtr, args.rts))
        return
    if args.profile is None or args.profile.stat().st_size > 65536:
        raise ValueError("Select a project PIC flash profile")
    profile = json.loads(args.profile.read_text())
    protocol = profile["protocol"]
    firmware = (args.profile.parent.parent / profile["firmware"]).resolve()
    if protocol in ("an1388-hid", "an1388-uart", "mcc-uart"):
        start, end = int(profile["applicationStart"], 0), int(profile["applicationEnd"], 0)
        # Validate all firmware bytes before opening USB or sending an erase.
        from bootloaders import bounded_image
        bounded_image(firmware, start, end)
        with WrappedUsb() as usb:
            interface = profile.get("interface")
            transport = HidTransport(usb, interface) if protocol == "an1388-hid" else UsbSerial(usb, profile.get("baud", 115200), interface)
            if protocol == "mcc-uart":
                MccUart(transport).program(firmware, start, end, profile["pageSizeBytes"], profile.get("addressUnitBytes", 1))
            else:
                An1388(transport).program(firmware, start, end)
    else:
        from programmers import program
        program(profile, firmware)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, ImportError, subprocess.SubprocessError) as error:
        print("PIC USB: error: " + str(error), file=sys.stderr, flush=True)
        sys.exit(1)
