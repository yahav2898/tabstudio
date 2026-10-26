"""Small hidapi-compatible transport for pyedbglib's permissioned device only.

This module does not enumerate host USB devices or access hidraw. Android has
already selected and permitted the fd supplied to WrappedUsb.
"""
import json
import os
from usb_device import WrappedUsb


def _strings(usb):
    descriptor = usb.control(0x80, 6, 0x0100, length=18)
    if len(descriptor) != 18:
        raise OSError("Truncated USB device descriptor")
    languages = usb.control(0x80, 6, 0x0300, length=255)
    language = int.from_bytes(languages[2:4], "little") if len(languages) >= 4 else 0x0409
    values = []
    for index in descriptor[14:17]:
        data = usb.control(0x80, 6, 0x0300 | index, language, length=255) if index else b""
        values.append(data[2:].decode("utf-16le", errors="replace"))
    return values


def enumerate(vendor_id=0, product_id=0):  # pylint: disable=redefined-builtin
    selected = json.loads(os.environ["TABSTUDIO_USB_DEVICE"])
    if vendor_id not in (0, selected["vendorId"]) or product_id not in (0, selected["productId"]):
        return []
    with WrappedUsb() as usb:
        manufacturer, product, serial = _strings(usb)
    return [{"path": b"tabstudio:3", "vendor_id": selected["vendorId"], "product_id": selected["productId"],
             "manufacturer_string": manufacturer, "product_string": product, "serial_number": serial,
             "interface_number": next(i["number"] for i in selected["interfaces"] if i["class"] == 3),
             "usage_page": 0xff00, "usage": 1, "release_number": 0}]


class device:  # hidapi's public name
    def __init__(self):
        self.usb = None
        self.nonblocking = False

    def open(self, vendor_id, product_id, serial_number=None):
        selected = json.loads(os.environ["TABSTUDIO_USB_DEVICE"])
        if (vendor_id, product_id) != (selected["vendorId"], selected["productId"]):
            raise OSError("Only the Android-permitted USB device can be opened")
        self.usb = WrappedUsb()
        self.strings = _strings(self.usb)
        if serial_number and self.strings[2] != serial_number:
            self.close()
            raise OSError("USB serial number does not match the selected device")
        self.item, self.rx, self.tx = self.usb.endpoints(3)

    def open_path(self, path):
        if path != b"tabstudio:3":
            raise OSError("Unpermitted HID path")
        selected = json.loads(os.environ["TABSTUDIO_USB_DEVICE"])
        self.open(selected["vendorId"], selected["productId"])

    def write(self, data):
        payload = bytes(data)
        if not payload:
            return 0
        count = self.usb.transfer(self.tx, data=payload[1:] if payload[0] == 0 else payload, timeout=5000)
        return count + (1 if payload[0] == 0 else 0)

    def read(self, length, timeout_ms=0):
        return list(self.usb.transfer(self.rx, length=length, timeout=timeout_ms or (1 if self.nonblocking else 5000)))

    def set_nonblocking(self, value):
        self.nonblocking = bool(value)
        return 0

    def get_manufacturer_string(self):
        return self.strings[0]

    def get_product_string(self):
        return self.strings[1]

    def get_serial_number_string(self):
        return self.strings[2]

    def close(self):
        if self.usb is not None:
            self.usb.close()
            self.usb = None
