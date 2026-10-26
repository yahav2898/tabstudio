# USB bridge verification

Run `python3 build/embedded/tests/test_bridges.py` on Linux with Python 3 and a C compiler. No root, physical device, USB kernel module, Unix socket or Python package download is required.

These are **synthetic tests, not recorded hardware transcripts**:

- USB UART tests replay the expected CDC-ACM, CH340, CP210x and FTDI control-request shapes against a fake transport. They test line coding, modem outputs and break; they do not establish that particular adapter firmware accepts those requests.
- A compiled, unchanged POSIX C client opens a real PTY through the worker and `LD_PRELOAD` shim. It exercises normal termios, Linux termios2 arbitrary baud rates, parity/stop bits, DTR/RTS, break, HUPCL, drain, flush and exact binary loopback. A separate client confirms unrelated terminal descriptors remain native.
- An independently implemented mock libusb ABI tests two interleaved libusb contexts, reference ownership, repeated enumeration, VID/PID filtering, borrowed-descriptor lifetime and denial without an explicitly inherited device FD.

`UsbBridgeProcess.CreateVirtualSelfTest()` runs a Python POSIX client through the same production worker and shim inside Android's PRoot environment. Success requires the literal `EMBEDDED_BRIDGE_PASSED` marker. Its backing serial adapter is explicitly virtual; no physical USB is claimed.

The libusb bridge supports **dynamically linked libusb clients only**, scoped to one permitted USB connection per process. nusb, native hidraw HIDAPI, statically linked libusb, direct usbfs calls and direct serial-control syscalls bypass these shims. A probe-rs executable using nusb needs a separate FD-aware backend or usbfs/sysfs integration; a successful libusb test does not prove probe-rs works.

The serial worker creates an explicit private PTY alias (usually `.tabstudio/dev/ttyUSB0`) and substitutes it into `{port}` tool arguments. It does not create kernel serial devices or install unrestricted `/dev` access. CDC input modem status has no synchronous generic request and reports only host-controlled DTR/RTS. Hardware RTS/CTS and distinct input/output baud rates are refused. Flush clears PTY queues; generic CDC has no portable hardware FIFO purge. Drain includes a conservative UART wire-time budget after USB OUT acceptance, bounded at 60 seconds.

Real captures should be contributed with device/probe firmware, VID/PID, target part, tool version, upstream source, permission to redistribute, operation, expected result and a SHA-256 of the firmware under test. Do not rename these synthetic fixtures into captures. Electrical reset timing, hot-plug/re-enumeration, USB host-controller quirks and actual programming/verification require physical hardware tests.
