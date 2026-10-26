"""Use an Android USB permissioned fd. Never enumerate or open /dev/bus/usb.

libusb's wrap API borrows the fd: Android's UsbDeviceConnection stays alive in
the parent until this worker exits. See libusb_wrap_sys_device documentation.
"""
import ctypes as C
import json
import os


class UsbError(OSError):
    pass


class WrappedUsb:
    def __init__(self, fd=None, device=None, library=None):
        self.device = device or json.loads(os.environ["TABSTUDIO_USB_DEVICE"])
        self.fd = int(os.environ["TABSTUDIO_USB_FD"]) if fd is None else fd
        if self.fd < 3:
            raise UsbError("Expected an explicitly inherited Android USB descriptor")
        self.lib = library or C.CDLL("libusb-1.0.so.0")
        self.ctx = C.c_void_p()
        self.handle = C.c_void_p()
        self.claimed = []
        self.closed = False
        self.lib.libusb_init_context.argtypes = [C.POINTER(C.c_void_p), C.c_void_p, C.c_int]
        self.lib.libusb_wrap_sys_device.argtypes = [C.c_void_p, C.c_ssize_t, C.POINTER(C.c_void_p)]
        self.lib.libusb_close.argtypes = [C.c_void_p]
        self.lib.libusb_exit.argtypes = [C.c_void_p]
        self.lib.libusb_claim_interface.argtypes = [C.c_void_p, C.c_int]
        self.lib.libusb_release_interface.argtypes = [C.c_void_p, C.c_int]
        self.lib.libusb_set_auto_detach_kernel_driver.argtypes = [C.c_void_p, C.c_int]
        transfer = [C.c_void_p, C.c_ubyte, C.c_void_p, C.c_int, C.POINTER(C.c_int), C.c_uint]
        self.lib.libusb_bulk_transfer.argtypes = transfer
        self.lib.libusb_interrupt_transfer.argtypes = transfer
        self.lib.libusb_control_transfer.argtypes = [C.c_void_p, C.c_ubyte, C.c_ubyte, C.c_ushort, C.c_ushort, C.c_void_p, C.c_ushort, C.c_uint]
        class InitOption(C.Structure):
            _fields_ = [("option", C.c_int), ("value", C.c_ssize_t)]
        # LIBUSB_OPTION_NO_DEVICE_DISCOVERY. Applies before initialization.
        option = InitOption(2, 0)
        self.check(self.lib.libusb_init_context(C.byref(self.ctx), C.byref(option), 1), "initialize USB")
        try:
            self.check(self.lib.libusb_wrap_sys_device(self.ctx, self.fd, C.byref(self.handle)), "wrap Android USB descriptor")
            self.lib.libusb_set_auto_detach_kernel_driver(self.handle, 1)
        except BaseException:
            self.close()
            raise

    @staticmethod
    def check(code, operation):
        if code < 0:
            raise UsbError(f"Cannot {operation}: libusb error {code}. Reconnect the device if it was detached.")
        return code

    def claim(self, number):
        if number not in self.claimed:
            self.check(self.lib.libusb_claim_interface(self.handle, number), "claim interface " + str(number))
            self.claimed.append(number)

    def control(self, kind, request, value=0, index=0, data=b"", length=0, timeout=3000):
        incoming = bool(kind & 0x80)
        size = length if incoming else len(data)
        if not 0 <= size <= 65535:
            raise ValueError("Invalid USB control transfer length")
        buf = C.create_string_buffer(size)
        if not incoming and data:
            C.memmove(buf, bytes(data), size)
        count = self.check(self.lib.libusb_control_transfer(self.handle, kind, request, value & 0xffff, index & 0xffff, buf, size, timeout), "send control transfer")
        if not incoming and count != size:
            raise UsbError("Short USB control write")
        return buf.raw[:count] if incoming else count

    def transfer(self, endpoint, data=None, length=4096, timeout=500):
        incoming = bool(endpoint["address"] & 0x80)
        size = length if incoming else len(data)
        buf = C.create_string_buffer(size)
        if not incoming:
            C.memmove(buf, bytes(data), size)
        actual = C.c_int()
        method = self.lib.libusb_interrupt_transfer if endpoint["type"] == 3 else self.lib.libusb_bulk_transfer
        result = method(self.handle, endpoint["address"], buf, size, C.byref(actual), timeout)
        if result == -7 and incoming:  # timeout may still return partial data
            return buf.raw[:actual.value]
        self.check(result, "transfer USB data")
        if not incoming and actual.value != size:
            raise UsbError("Short USB write; the operation was not retried")
        return buf.raw[:actual.value] if incoming else actual.value

    def endpoints(self, kind, interface=None):
        for item in self.device["interfaces"]:
            if interface is not None and item["number"] != interface:
                continue
            endpoints = [e for e in item["endpoints"] if e["type"] == kind]
            read = next((e for e in endpoints if e["address"] & 0x80), None)
            write = next((e for e in endpoints if not e["address"] & 0x80), None)
            if read and write:
                self.claim(item["number"])
                return item, read, write
        raise UsbError("The selected USB interface has no matching input/output endpoints")

    def close(self):
        if self.closed:
            return
        self.closed = True
        if self.handle:
            for number in reversed(self.claimed):
                self.lib.libusb_release_interface(self.handle, number)
            self.lib.libusb_close(self.handle)
        if self.ctx:
            self.lib.libusb_exit(self.ctx)
        # Do not close the borrowed fd: libusb explicitly leaves ownership with Android.

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
