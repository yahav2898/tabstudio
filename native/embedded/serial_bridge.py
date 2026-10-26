#!/usr/bin/env python3
"""Permissioned USB serial -> raw PTY plus synchronous POSIX-control RPC.

The child is unmodified. Only its dynamic POSIX calls load serial_posix.so.
No root, kernel serial driver, unrestricted USB enumeration or TCP listener.
Virtual mode is a synthetic byte-loopback device for CI, not a hardware proof.
"""
import argparse
import array
import contextlib
import errno
import fcntl
import json
import os
from pathlib import Path
import pty
import queue
import select
import signal
import struct
import subprocess
import sys
import tempfile
import termios
import threading
import time
import tty

MAGIC = 0x54534231
REQUEST = struct.Struct("!7I")
RESPONSE = struct.Struct("!6I")
CONFIG, LINES, MODEM, BREAK, FLUSH, DRAIN, GET_CONFIG = range(1, 8)


def receive_exact(descriptor, size):
    data = bytearray()
    deadline = time.monotonic() + 5
    while len(data) < size:
        if time.monotonic() >= deadline:
            raise OSError(errno.ETIMEDOUT, "Truncated serial-control request")
        if not select.select([descriptor], [], [], 0.1)[0]:
            continue
        chunk = os.read(descriptor, size - len(data))
        if not chunk:
            raise OSError(errno.EPIPE, "Truncated serial-control request")
        data.extend(chunk)
    return bytes(data)


class VirtualSerial:
    """Deliberately synthetic USB-serial substitute; never used automatically."""
    def __init__(self, baud=115200):
        self.baud, self.data_bits, self.stop_bits, self.parity = baud, 8, 1, 0
        self.dtr = self.rts = self.break_enabled = False
        self.data = queue.Queue()

    def configure(self, baud, data_bits, stop_bits, parity):
        if not 1 <= baud <= 3500000 or data_bits not in (5, 6, 7, 8) or stop_bits not in (1, 2) or parity not in range(5):
            raise ValueError("Unsupported line coding")
        self.baud, self.data_bits, self.stop_bits, self.parity = baud, data_bits, stop_bits, parity

    def set_lines(self, dtr, rts):
        self.dtr, self.rts = bool(dtr), bool(rts)

    def modem_bits(self):
        return (2 if self.dtr else 0) | (4 if self.rts else 0)

    def set_break(self, enabled):
        self.break_enabled = bool(enabled)

    def read(self, length=4096, timeout=200):
        try:
            return self.data.get(timeout=timeout / 1000)
        except queue.Empty:
            return b""

    def write(self, data):
        self.data.put(bytes(data))
        return len(data)


class SerialBridge:
    def __init__(self, serial, audit=None):
        self.serial, self.audit = serial, audit
        self.master, self.slave = pty.openpty()
        tty.setraw(self.slave)
        self.slave_name = os.ttyname(self.slave)
        os.set_blocking(self.master, False)
        self.stop = threading.Event()
        self.control_lock = threading.Lock()
        self.tx_lock = threading.Lock()
        self.error = None
        self.directory = tempfile.TemporaryDirectory(prefix="ts-serial-")
        # FIFOs work in Android app storage and need no socket permission. The
        # native caller uses a separate flock per request to serialize processes.
        for name in ("request", "response"):
            os.mkfifo(str(Path(self.directory.name) / name), 0o600)
        Path(self.directory.name, "lock").touch(mode=0o600)
        self.request = os.open(str(Path(self.directory.name) / "request"), os.O_RDWR | os.O_NONBLOCK)
        self.response = os.open(str(Path(self.directory.name) / "response"), os.O_RDWR | os.O_NONBLOCK)
        self.threads = []

    def record(self, kind, values):
        if self.audit:
            with open(self.audit, "a", encoding="utf-8") as log:
                log.write(json.dumps({"operation": kind, "values": values}) + "\n")

    def fail(self, error):
        self.error = error
        self.stop.set()

    def incoming(self):
        try:
            while not self.stop.is_set():
                data = memoryview(self.serial.read(timeout=100))
                while data and not self.stop.is_set():
                    if not select.select([], [self.master], [], 0.1)[1]:
                        continue
                    try:
                        count = os.write(self.master, data)
                        data = data[count:]
                    except BlockingIOError:
                        pass
        except OSError as error:
            if not self.stop.is_set():
                self.fail(error)

    def outgoing(self):
        try:
            while not self.stop.is_set():
                if not select.select([self.master], [], [], 0.1)[0]:
                    continue
                with self.tx_lock:
                    try:
                        data = os.read(self.master, 4096)
                    except BlockingIOError:
                        continue
                    if data:
                        count = self.serial.write(data)
                        if count != len(data):
                            raise OSError(errno.EIO, "Short USB serial write")
        except OSError as error:
            if not self.stop.is_set():
                self.fail(error)

    def drain(self):
        deadline = time.monotonic() + 5
        while not self.stop.is_set():
            with self.tx_lock:
                pending = array.array("i", [0])
                fcntl.ioctl(self.master, termios.FIONREAD, pending, True)
                if not pending[0]:
                    return
            if time.monotonic() >= deadline:
                raise OSError(errno.ETIMEDOUT, "Serial output did not drain")
            self.stop.wait(0.001)
        raise OSError(errno.ENODEV, "Serial device disconnected")

    def dispatch(self, request):
        magic, _token, operation, a, b, c, d = request
        if magic != MAGIC:
            raise OSError(errno.EPROTO, "Unknown serial RPC version")
        with self.control_lock:
            if operation == CONFIG:
                self.serial.configure(a, b, c, d)
            elif operation == LINES:
                mask = b & 6
                bits = (((2 if self.serial.dtr else 0) | (4 if self.serial.rts else 0)) & ~mask) | (a & mask)
                self.serial.set_lines(bool(bits & 2), bool(bits & 4))
            elif operation == MODEM:
                return [self.serial.modem_bits(), 0, 0, 0]
            elif operation == GET_CONFIG:
                return [self.serial.baud, self.serial.data_bits, self.serial.stop_bits, self.serial.parity]
            elif operation == BREAK:
                if a not in (0, 1) or b > 60000:
                    raise ValueError("Invalid BREAK duration")
                self.serial.set_break(bool(a))
                if a and b:
                    try:
                        self.stop.wait(b / 1000)
                    finally:
                        self.serial.set_break(False)
            elif operation == FLUSH:
                if a not in (0, 1, 2):
                    raise ValueError("Invalid flush direction")
                # Discard PTY queues. CDC has no portable USB endpoint purge.
                termios.tcflush(self.slave, a)
            elif operation == DRAIN:
                self.drain()
            else:
                raise OSError(errno.ENOTSUP, "Unsupported serial control operation")
            self.record(operation, [a, b, c, d])
        return [0, 0, 0, 0]

    def controls(self):
        while not self.stop.is_set():
            if not select.select([self.request], [], [], 0.1)[0]:
                continue
            token = 0
            try:
                request = REQUEST.unpack(receive_exact(self.request, REQUEST.size))
                token = request[1]
                values = self.dispatch(request)
                status = 0
            except (OSError, ValueError, struct.error) as error:
                status, values = getattr(error, "errno", None) or errno.EINVAL, [0, 0, 0, 0]
            with contextlib.suppress(OSError):
                os.write(self.response, RESPONSE.pack(status, token, *values))

    def start(self):
        for operation in (self.incoming, self.outgoing, self.controls):
            thread = threading.Thread(target=operation, daemon=True)
            thread.start()
            self.threads.append(thread)

    def close(self):
        self.stop.set()
        for thread in self.threads:
            thread.join(timeout=4)
        # Reset outputs even on cancellation or a failed child. Never reset the MCU
        # after a failed verify; these are serial host outputs, not a jump command.
        with contextlib.suppress(OSError):
            self.serial.set_break(False)
            self.serial.set_lines(False, False)
        os.close(self.request)
        os.close(self.response)
        os.close(self.master)
        os.close(self.slave)
        self.directory.cleanup()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shim", required=True)
    parser.add_argument("--port", required=True, help="Private absolute PTY alias, e.g. .tabstudio/dev/ttyUSB0")
    parser.add_argument("--baud", type=int, default=115200)
    parser.add_argument("--interface", type=int)
    parser.add_argument("--virtual", action="store_true", help="Explicit synthetic loopback device for CI")
    parser.add_argument("--audit", help="Control-event JSONL for tests; contains no firmware or serial bytes")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("Expected a tool command after --")
    alias = Path(args.port)
    if not alias.is_absolute() or not Path(args.shim).is_file():
        parser.error("The PTY alias and compiled shim must be absolute paths")
    if os.path.lexists(alias):
        parser.error("Refusing to overwrite an existing serial port alias")
    alias.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    usb = None
    bridge = None
    child = None
    try:
        if args.virtual:
            serial = VirtualSerial(args.baud)
        else:
            # These are the existing permissioned PIC USB drivers, installed
            # beside this worker, not another USB/serial implementation.
            from usb_device import WrappedUsb
            from usb_serial import UsbSerial
            usb = WrappedUsb()
            serial = UsbSerial(usb, args.baud, args.interface)
        bridge = SerialBridge(serial, args.audit)
        os.symlink(bridge.slave_name, alias)
        bridge.start()
        environment = dict(os.environ)
        environment.update(TABSTUDIO_SERIAL_DEVICE=bridge.slave_name, TABSTUDIO_SERIAL_CONTROL_DIR=bridge.directory.name)
        environment["LD_PRELOAD"] = args.shim + (":" + environment["LD_PRELOAD"] if environment.get("LD_PRELOAD") else "")
        command = [argument.replace("{port}", str(alias)) for argument in command]
        child = subprocess.Popen(command, env=environment, close_fds=True)
        def stop_child(_signal, _frame):
            bridge.stop.set()
            if child.poll() is None:
                child.terminate()
        signal.signal(signal.SIGTERM, stop_child)
        signal.signal(signal.SIGINT, stop_child)
        while child.poll() is None:
            if bridge.stop.wait(0.1):
                child.terminate()
                try:
                    child.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    child.kill()
                break
        result = child.wait()
        if bridge.error:
            print("Serial bridge disconnected: " + str(bridge.error), file=sys.stderr)
            return 1
        return result if result >= 0 else 128 - result
    finally:
        if child is not None and child.poll() is None:
            child.kill()
            child.wait()
        if bridge:
            bridge.close()
            if alias.is_symlink() and os.readlink(alias) == bridge.slave_name:
                alias.unlink()
        if usb:
            usb.close()


if __name__ == "__main__":
    sys.exit(main())
