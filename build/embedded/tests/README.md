# USB bridge verification

Run `python3 build/embedded/tests/test_bridges.py` on Linux with Python 3 and a C compiler. No root, physical device, USB kernel module, Unix socket or Python package download is required.

These are **synthetic tests, not recorded hardware transcripts**:

- USB UART tests replay the expected CDC-ACM, CH340, CP210x and FTDI control-request shapes against a fake transport. They test line coding, modem outputs and break; they do not establish that particular adapter firmware accepts those requests.
- A compiled, unchanged POSIX C client opens a real PTY through the worker and `LD_PRELOAD` shim. It exercises normal termios, Linux termios2 arbitrary baud rates, parity/stop bits, DTR/RTS, break, HUPCL, drain, flush and exact binary loopback. A separate client confirms unrelated terminal descriptors remain native.
- An independently implemented mock libusb ABI tests two interleaved libusb contexts, reference ownership, repeated enumeration, VID/PID filtering, borrowed-descriptor lifetime and denial without an explicitly inherited device FD.

`UsbBridgeProcess.CreateVirtualSelfTest()` runs a Python POSIX client through the same production worker and shim inside Android's PRoot environment. Success requires the literal `EMBEDDED_BRIDGE_PASSED` marker. Its backing serial adapter is explicitly virtual; no physical USB is claimed.

The libusb bridge supports **dynamically linked libusb clients only**, scoped to one permitted USB connection per process. nusb, native hidraw HIDAPI, statically linked libusb, direct usbfs calls and direct serial-control syscalls bypass these shims. A probe-rs executable using nusb needs a separate FD-aware backend or usbfs/sysfs integration; a successful libusb test does not prove probe-rs works.

The separate production `UsbfsDeviceInventory` and patched PRoot implement that usbfs/sysfs path. CI's
`build-proot-test.sh` tests selected-device raw syscall opens with a static tracee, with the seccomp accelerator
enabled and disabled. The locked `nusb-check` fixture compiles upstream nusb 0.2.9 unchanged with its normal
rustix raw-syscall backend. `nusb-inventory-check` mounts the actual C# inventory builder's synthetic descriptors
and runs that executable through patched PRoot. The [successful contracts run](https://github.com/yahav2898/tabstudio/actions/runs/40420024524/job/121911856957)
requires `RAW_USB_PASSED` and `NUSB_PERMISSIONED_ENUMERATION_PASSED 0483:3748`. This is CI verification of
enumeration and FD routing, not a hardware USB transfer, successful probe connection or target upload.

The composition fixture also inherits a real descriptor-file FD through the production Unix process launcher.
It requires nusb's selected-device open to read valid descriptors and reach the kernel's expected `EPERM` from
registering a regular file with epoll. Missing/denied opens or reading the empty usbfs placeholder fail earlier.
A separate read-only `USBDEVFS_GET_CAPABILITIES` ioctl must return kernel `ENOTTY` on that same synthetic file;
USB success is never simulated. Those added open/ioctl markers must pass their CI run before being described as
verified. An unbridged negative control runs the same stock nusb build against the same inventory and empty usbfs
placeholder with the FD-routing environment removed; it must fail descriptor validation. Physical USB ioctls,
hot-plug timing and native hidraw/CMSIS-DAP v1 remain unverified or unsupported.

The serial worker creates an explicit private PTY alias (usually `.tabstudio/dev/ttyUSB0`) and substitutes it into `{port}` tool arguments. It does not create kernel serial devices or install unrestricted `/dev` access. CDC input modem status has no synchronous generic request and reports only host-controlled DTR/RTS. Hardware RTS/CTS and distinct input/output baud rates are refused. Flush clears PTY queues; generic CDC has no portable hardware FIFO purge. Drain includes a conservative UART wire-time budget after USB OUT acceptance, bounded at 60 seconds.

Real captures should be contributed with device/probe firmware, VID/PID, target part, tool version, upstream source, permission to redistribute, operation, expected result and a SHA-256 of the firmware under test. Do not rename these synthetic fixtures into captures. Electrical reset timing, hot-plug/re-enumeration, USB host-controller quirks and actual programming/verification require physical hardware tests.

## BOOTSEL storage contracts

`python3 build/embedded/tests/test_msc.py` uses an independently scripted synthetic USB transport to check the
production worker's exact 31-byte CBW, 13-byte CSW, tag/residue/status validation, SCSI byte order, command
bounds, pre-write authorization and final-status disconnection reporting. These are not captured ROM traffic.
No FORMAT, reset or arbitrary SCSI operation is exposed. The worker borrows one already granted Android USB FD.

The managed `StorageTests` create FAT12, FAT16 and FAT32 volumes, both as superfloppies and inside MBR images.
They exercise existing-file preservation, mirrors and active FAT, a packed FAT12 entry crossing a sector,
FAT32 primary/backup FSInfo, application/family/identity refusal, cancellation, races, failure rollback and a
ROM-like final-payload disconnection. Export those regular scratch images and verify them independently:

```sh
TABSTUDIO_FAT_PROOF=/tmp/tabstudio-fat-proof \
  dotnet test tests/TabStudio.Toolchains.Tests --filter FullyQualifiedName~StorageTests
python3 build/embedded/verify-fat-images.py /tmp/tabstudio-fat-proof
```

The second command requires GNU mtools (`mtype`) and compares the actual copied file with its UF2 bytes.
It does not mount anything or open a hardware disk. Local mtools 4.0.43 read all six generated files correctly.
These checks establish FAT interoperability and protocol contracts; they do not establish physical RP ROM
programming or flash readback. RP BOOTSEL is a synthetic filesystem and may reset once the UF2 is consumed.

Primary specifications and implementation references:

- [USB Mass Storage Bulk-Only Transport 1.0](https://www.usb.org/sites/default/files/usbmassbulk_10.pdf).
- [Microsoft FAT specification](https://www.scs.stanford.edu/~zyedidia/docs/_other/fat.pdf).
- [RP2040 ROM virtual disk](https://github.com/raspberrypi/pico-bootrom-rp2040/blob/master/bootrom/virtual_disk.c)
  and [RP2350 ROM virtual disk](https://github.com/raspberrypi/pico-bootrom-rp2350/blob/master/src/nsboot/usb_virtual_disk.c).
- [UF2 family registry](https://github.com/microsoft/uf2/blob/master/utils/uf2families.json).


## PlatformIO package architecture proof

`platformio-policy-check.py` runs the pinned, unchanged Core 6.1.18 package manager,
HTTP mirror handling, archive extractor and SHA-256 downloader against a synthetic
loopback registry. An ARM64-labeled archive contains a deliberately generated ELF32
ARM executable. The policy must download it first, refuse its postinstall script,
retain that distribution unchanged and select only the same version's x64 artifact.
The replacement's actual postinstall runs, and its original executable bytes must
match. Separate processes prove native-only mode refuses x64 before postinstall and
foreign executables generated by postinstall are refused before a build.
Only the unrelated WAN reachability precheck is disabled for this loopback fixture.

The native glibc frontend job executes this proof on x64 and ARM64 alongside the
actual unchanged GCC/Box64 compilation and child-descriptor tests. The small local
Python unit suite additionally checks executable ELF32/ELF64 headers, corrupt-header
refusal and shared-library versus PIE classification. No registry label is treated
as evidence of the executable architecture. These are package/runtime tests, not
physical USB or board qualification.
