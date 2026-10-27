# Embedded firmware from the tablet

TabStudio can open native embedded projects, build with their own command-line tools, and route supported
flashing tools through Android's permissioned USB connection. **Debug > Embedded** contains device discovery,
build, upload, on-chip debug, backup, production, analysis, register and board-Python commands.

This is an experimental implementation, not a claim that every microcontroller works on Android. A parser,
template, command plan, virtual USB test or emulated CPU is not a physical-board verification. No physical board,
debug probe or USB adapter has been verified by this change. The tables below separate implemented routes from
missing integration. The PR's final commit and check results are the authority for CI verification.

## Start with an existing project

1. Open its folder. The detector recognizes the project systems below without executing project code. In a
   repository with several projects, the Embedded commands select the relevant project explicitly.
2. Choose **Debug > Embedded > Board and build configuration**. This opens `.tabstudio/embedded.json`.
   Select the actual board/environment/target; a USB-serial adapter's vendor ID never selects the MCU.
3. Install its available Toolchain Store entry. Board cores and SDK packages are separate dependencies.
   Choose **Build firmware**, or **Build > Embedded Firmware**. Native compiler output uses the existing
   Error List and Output paths. A successful build runs the ELF memory report when a unique ELF is available.
4. For upload, review `.tabstudio/embedded-flash.json` for the actual firmware, bootloader/probe and permitted
   application address ranges. Empty ranges refuse all programming before erase. Choose **Upload and verify**,
   select the USB device, and grant Android permission. Keep the target powered and wired as its vendor requires.
5. Open the existing **View > Serial Monitor** independently for application logs. The monitor releases its
   connection before another embedded operation takes that adapter.

`F5` uses an existing embedded debug profile to attach the on-chip debugger. With an explicit flash profile it
builds/uploads and opens the Serial Monitor; automatic connection is restricted to the same serial-capable USB
attachment. Without either profile it builds firmware. Starters include exact-board application bounds and
bootloader/probe profiles; target, clone and module variants still require review. Physical reset, re-enumeration
and monitor behavior are hardware-unverified.
The existing PIC F5 simulator and ordinary language F5 behavior remain documented separately.

### Project systems

| Project | Detection and native build | IntelliSense and limits |
| --- | --- | --- |
| PlatformIO | `platformio.ini`; `pio run`, preserving environments, inheritance, platform versions and native scripts | Existing `compile_commands.json` is preserved. Run `pio run -t compiledb` for exact clangd flags. Core is installed; every platform's ARM64/musl package availability is a separate question. |
| Arduino sketches and libraries | `.ino`, `sketch.yaml`, `library.properties`; `arduino-cli compile` with an explicit FQBN; a library requires an explicit `Sketch` example directory | The build first requests Arduino's compilation database. Install the selected board core/libraries separately. A library itself is not an executable sketch. |
| STM32CubeMX/CubeIDE | `.ioc` plus generated CMake or Make files; preserve their startup, CPU flags and linker script | CMake exports compilation commands. An Eclipse `.cproject` is not independently translated into a complete Cube build; generate/retain a portable build first. CubeMX's generator is not bundled. |
| ESP-IDF | IDF CMake markers, `sdkconfig`; `idf.py` with the selected target | The native glibc recipe pins ESP-IDF 5.4.1 and its submodules, vendor tool hashes and Python distributions. The selected target remains native project configuration; third-party components can have their own prerequisites. |
| Zephyr | west/Zephyr markers; `west build` with the exact board | The native glibc recipe pins Zephyr 4.1.0, west/Python wheels and its manifest modules, using signed ARM/RISC-V compilers. Out-of-tree west workspaces retain their own dependencies and board definitions. |
| Raspberry Pi Pico SDK | `pico_sdk_import.cmake` or Pico CMake usage; CMake with the selected `PICO_BOARD` | The native glibc recipe pins Pico SDK 2.1.1 and its submodules and exports its SDK path only for Pico projects. Pico/Pico W/Pico 2/Pico 2 W are distinct choices. |
| Rust embedded | Cargo with embedded target/linker markers, including `memory.x`; Cargo preserves its configuration and uses `--locked` when a lockfile exists | A separate embedded Rust SDK installs SHA-256-pinned official Rust 1.97.0 components, rust-analyzer, rust-src and ten ARM/RV32 target libraries in the glibc environment. Native project context selects its server/compiler; ordinary host Rust retains its existing toolchain. Official probe-rs uses the separate permissioned usbfs bridge. |
| MicroPython/CircuitPython | Board-Python markers and explicit `.py` file operations through mpremote | **MicroPython REPL** opens the existing Terminal; **Sync active Python file** copies that file and compares an independent readback. This does not install firmware, provide CircuitPython-specific REPL guarantees, or delete other board files. |
| Keil, IAR, Atmel/Microchip Studio | `.uvprojx`, `.ewp`, `.cproj`; explicit import creates a separate GCC/CMake project | Original files are untouched. Import notes describe unresolved linker/startup/ABI/language differences. Import success is not a linked or flashed firmware result. |

Build commands use argument arrays, not shell interpolation. SDK launchers must be visible to the app's toolchain
runtime. Exporting a variable in an unrelated Terminal session does not configure later IDE build processes.
PlatformIO's native upload commands and SDK runners are represented in build plans, but the safe Upload command
uses the reviewed flash profile; it does not execute arbitrary runner flags around the flash safety checks.

An example build configuration is:

```json
{
  "Environment": "uno",
  "Board": "arduino:avr:uno",
  "Jobs": 2
}
```

`Sketch`, `Target`, `Port`, `Chip`, `Firmware` and `Runner` are additional explicit choices used by the relevant
project system. They do not replace native project configuration or install missing SDKs automatically.

## USB bridges and their boundaries

Android grants access through the USB Host API. TabStudio keeps the selected `UsbDeviceConnection` alive and
duplicates only that granted descriptor into child fd 3. It does not grant root, enumerate arbitrary usbfs
devices or provide unrestricted `/dev/bus/usb` access.

The libusb bridge reuses the PIC support's `libusb_wrap_sys_device` implementation. Its enumeration/open
interposer presents the single granted device to **dynamically linked libusb clients**, with independent libusb
contexts and ownership checks. Production programming uses independent processes and connections per device.
The unmodified upstream executable still depends on a compatible dynamically linked backend.

The separate **usbfs/nusb bridge** covers clients that bypass libusb. The app reads raw descriptors and the
active configuration from the granted Android connection, then creates a private, single-device sysfs inventory
and usbfs placeholder mounted only into that worker. TabStudio's packaged PRoot rewrites an open/openat of
exactly the selected usbfs node into a duplication of inherited fd 3. This happens at the syscall boundary and
therefore also covers static executables and nusb's normal rustix raw-syscall backend. It does not reopen an
Android device node or synthesize successful USB transfers. A libc preload supplies a separately tested fallback
for dynamically linked clients. Each inventory and Android connection stays alive until its worker exits.

The [CI contracts run](https://github.com/yahav2898/tabstudio/actions/runs/40420024524/job/121911856957) verified
static raw-syscall FD duplication with PRoot's seccomp accelerator both enabled and disabled, and stock nusb
0.2.9 enumerating the actual `UsbfsDeviceInventory` output. These use a synthetic descriptor file, not a USB
board: enumeration and FD routing are **verified on CI**; physical USB ioctls, transfers, probe firmware and target
programming remain hardware-unverified. Native hidraw/udev HIDAPI, including probe-rs's CMSIS-DAP v1 backend,
is a separate path and is not made compatible by this bridge.

The serial bridge runs an unchanged dynamically linked POSIX client on a real raw PTY. A scoped native shim
forwards termios/ioctl changes to the existing CDC-ACM, CH340/341, CP210x and FTDI drivers: baud, data/parity/stop
format, DTR/RTS, break, hangup, drain and flush. Linux termios2 arbitrary baud rates are covered. The worker uses
an explicit private alias, normally `.tabstudio/dev/ttyUSB0`, rather than creating kernel serial devices.

| Transport | Implemented verification | Limit |
| --- | --- | --- |
| libusb | Synthetic ABI replay: enumeration, two contexts, references, VID/PID filtering, FD ownership and permission denial | No physical USB/probe test. Static libusb, direct usbfs, nusb and native hidraw bypass this shim. |
| usbfs / nusb | Verified on CI: static raw open/openat FD contracts with PRoot seccomp on/off, and stock nusb 0.2.9 enumeration of the production inventory builder's synthetic device | Hardware USB ioctls/transfers and actual flashing/debugging remain unverified. Native hidraw/HIDAPI v1 is not supplied by sysfs/usbfs synthesis. |
| CDC-ACM | Synthetic driver control requests and binary PTY loopback/control tests | No physical firmware/timing verification; generic CDC cannot synchronously report all input modem lines or purge every device FIFO. |
| CH340/CH341 | Synthetic line-format, baud, modem and break requests | Adapter variants and actual reset timing remain hardware-unverified. |
| CP210x | Synthetic line-format, baud, modem and break requests | Adapter firmware/USB controller combinations remain hardware-unverified. |
| FTDI | Synthetic line controls and per-packet status handling | Chip/board variants and physical flow control remain hardware-unverified. |
| Virtual Android device | Self-test invokes the production PTY worker/shim and requires `EMBEDDED_BRIDGE_PASSED` | An Android CI step is configured; its virtual serial backend is not physical USB. |

Hardware RTS/CTS, distinct input/output speeds, static serial executables and applications issuing direct control
syscalls are not transparently supported. Drain adds a conservative UART wire-time allowance, not an electrical
measurement. A 1200-baud touch closes the old connection and waits for a new bootloader USB identity; Android
permission is reacquired. Physical re-enumeration, detach and bootloader timing still require real-device tests.

The persistent device database stores successful explicit target/route selections after upload and can represent
VID/PID/optional-serial associations. Hot-plug callbacks refresh USB availability and end affected operations.
Discovery lists conservative transport suggestions. Generic adapters never identify a board, and a VID/PID match
is never labeled a probed chip identity. **Identify target** instead runs the pinned official probe-rs 0.32
`info --verbose` against the selected permissioned bulk-USB probe. Its Output summary retains only explicit
upstream IDCODE, ARM debug-port/CPUID and vendor DID records, with protocol context and evidence line numbers.
An IDCODE or core model does not uniquely identify an MCU or board, so flash/debug profiles still require the
actual chip and memory map. The command clears inherited erase-all, power-cycle, under-reset and custom target
overrides. Verbose info uses upstream's default debug sequences without erase/unlock permission; attaching can
affect live debug state. Native probe-rs HID-only CMSIS-DAP v1 is refused; its OpenOCD fallback uses the signed libusb HIDAPI backend. Physical target interrogation is hardware-unverified;
automatic interrogation on plug-in and a complete device-association editing UI are not implemented yet.

### Probes and target families

| Probe | Current route | Verification and limits |
| --- | --- | --- |
| ST-Link V2/V3 | probe-rs bulk-USB route; OpenOCD libusb fallback with installed scripts | Identity hints, safe plans and debug startup contracts tested locally; no hardware verification. Exact clone/firmware/target support belongs to the installed OpenOCD version. |
| CMSIS-DAP v2, DAPLink bulk interface | probe-rs bulk-USB route; OpenOCD libusb fallback | Backend integration is implemented; no physical DAPLink verification. |
| CMSIS-DAP v1 HID, DAPLink HID interface | OpenOCD with the signed HIDAPI-libusb backend selected in its private preload chain | An unchanged HIDAPI client is tested with the granted-FD libusb bridge. Native hidraw/udev and probe-rs's HID-only backend remain separate; physical DAPLink is hardware-unverified. |
| Raspberry Pi Debug Probe/picoprobe | probe-rs/CMSIS-DAP or OpenOCD where that probe firmware and target are supported | Descriptor hint and command contracts only; hardware-unverified. |
| J-Link | probe-rs or OpenOCD's applicable open backend | USB hint only is not SEGGER software installation or proof of target support. No hardware verification. |
| ESP USB-Serial-JTAG | Installed native esptool download; probe-rs for supported USB-JTAG targets or a matching Espressif OpenOCD build | The stock Alpine OpenOCD package does not establish vendor-fork coverage. No hardware verification. |
| WCH-Link | Pinned native wlink 0.1.3 plus an opt-in PRoot USB protocol guard, CH32V003 only | Four known 16 KiB parts; read-only chip identification, bounded code-flash loader, independent full readback, then separate reset. Option/protection/erase-all commands are denied. Actual stock-client replay and native installation are CI gates; the Store entry remains unlisted until they pass. Hardware-unverified; this does not establish other CH32 parts or debugging. |
| probe-rs probes | Pinned official 0.32 native ARM64/x64 glibc binaries; permissioned nusb download, DAP, identity and logging routes | Verified on CI: actual installer, official DAP initialization, stock nusb enumeration and raw FD routing. Physical probe/target I/O remains hardware-unverified; native hidraw CMSIS-DAP v1 uses the OpenOCD fallback. |

| Requested target families | Present foundation | Remaining qualification |
| --- | --- | --- |
| STM32, all families | Exact OpenOCD config plus ARM toolchain or native Cube/PlatformIO build | No blanket all-family validation; each part/config/protection state requires confirmation and hardware tests. |
| nRF51/52/53 | OpenOCD where supported; native Zephyr/PlatformIO/Rust project plans | Exact SDK/core selection, multicore behavior and each part's physical probe support require qualification. |
| RP2040/RP2350 | Pico SDK/PlatformIO plans; OpenOCD/picotool routes and permissioned BOOTSEL UF2 storage copy | Explicit board, application bounds and UF2 family; FAT/BOT synthetic tests and independent file readback, no physical-board qualification. |
| SAMD/SAME | ARM builds, OpenOCD or compatible bossac bootloader plan | Exact bootloader offset and installed tool are required; no hardware verification. |
| NXP LPC/Kinetis/i.MX RT | ARM/native ecosystem build and applicable OpenOCD config | No packaged vendor SDK or claim that every NXP target works. Teensy's bootloader route has separate limits below. |
| GD32, CH32V | Applicable upstream/vendor OpenOCD and GCC architecture | The Store supplies WCH ROM ISP; WCH-Link SWIO/vendor forks are separate. A generic RISC-V compiler alone does not establish a complete target startup/linker/debug workflow. |
| ESP32-C3/C6/H2 | ESP-IDF/native ecosystem build; exact esptool chip selection | The native ESP-IDF recipe supplies its selected target toolchain. Each chip/USB-JTAG backend, board layout and physical reset path still requires hardware qualification. |
| Other probe-rs/OpenOCD targets | Typed probe-rs chip selection or installed OpenOCD configuration can address upstream-supported targets | Upstream support lists are not TabStudio hardware verification or Android nusb compatibility. |

### Bootloaders and programming routes

| Board/protocol | Implemented operation | Verification and limits |
| --- | --- | --- |
| Arduino AVR Optiboot/STK500v1 | avrdude application write plus explicit verify | Safe image/range/argument plans and AVR profile tests; avrdude is in the Store. Hardware-unverified. |
| Arduino Mega STK500v2 | avrdude `wiring` application upload/verify | Explicit ATmega2560 profile; hardware-unverified. |
| Leonardo/Micro AVR109 | 1200-baud touch, re-permission, avrdude write/verify | Known template profiles and bridge contracts; physical bootloader transitions unverified. |
| SAMD SAM-BA | 1200-baud flow plus bossac `-o OFFSET -w -v -R`, without boot-configuration flags | The Store builds pinned BOSSA 1.9.1 source natively. BIN address becomes an explicit flash-relative offset, preserving the selected bootloader boundary. Exact bootloader/layout required; hardware-unverified. |
| ESP32/S2/S3/C3/C6/H2 ROM loader | Exact esptool chip, addressed BIN write then verify | The Store installs official esptool 5.6.0 ARM64/x64 binaries with pinned SHA-256 in its glibc environment. Single-image or checked build-metadata bundles; ESP32/S3/C3 starters export resolved SCons arguments, and ESP-IDF uses its actual flasher_args.json. Secure/encrypted images and data partitions are refused. Reset sequencing is hardware-unverified. |
| STM32 ROM UART | stm32flash explicit BIN range with verify | stm32flash is installed through the embedded USB Store entry from signed Alpine packages. BOOT pins/reset and exact target map are external prerequisites. Hardware-unverified. |
| STM32 USB DFU | dfu-util write, independent upload/readback and byte comparison | dfu-util is in the Store. Exact VID/PID, alternate and application range required. Read-protected/non-readback DFU cannot pass this route. Hardware-unverified. |
| RP2040/RP2350 BOOTSEL | picotool addressed ELF/UF2/BIN load with verify, or **Upload UF2 via BOOTSEL storage** through USB Host BOT/SCSI and user-space FAT | The Store installs official picotool 2.3.1 ARM64/x64 binaries pinned by SHA-256 in its glibc environment. Storage accepts standard RP ROM identities, actual INFO_UF2, a matching family and explicit XIP application ranges. USB/file acceptance is distinct from flash readback; ROM reset before final status is explicitly unconfirmed. FAT/BOT image tests and independent GNU mtools readback passed locally. Hardware-unverified. |
| nRF51/52 Nordic SDK Secure DFU | Application-only ZIP through legacy pc-nrfutil 6.x `dfu usb-serial` or `dfu serial` | ZIP/manifest/protobuf size/type/SHA-256 checks are unit-tested. Target bootloader validates compatibility/signature and object CRCs. The optional Store recipe retains the official Nordic bundle and runs its byte-identical legacy CLI natively on x64 or with explicit Box64 and a guest x64 serial shim on ARM64. Requires an existing Secure DFU bootloader; not modern nRFutil, BLE DFU or SoftDevice/bootloader updates. Hardware-unverified. |
| Teensy HalfKay | Exact-board `teensy_loader_cli --mcu=TEENSY40/TEENSY41 -v` HEX transfer | Transfer completion only: `-v` means verbose, not verify. UI says **Firmware transferred; readback unavailable** and production batches refuse this route. The Store builds pinned Teensy Loader 2.3 source with dynamic libusb-compat; press the board’s Program button before granting its HalfKay device. No first-device soft-reboot command is used. Hardware-unverified. |
| WCH ROM ISP | wchisp 0.3-compatible `info --chip` preflight, then default `flash` with ISP verification | Raw BIN must explicitly start at code-flash address zero. USB or explicit UART transport is supported; UART baud is 115200/1000000/2000000. The Store builds pinned wchisp 0.3.0 source with its checked Cargo.lock and dynamic libusb. No config-reset/unprotect flags. CH32V003 has UART ISP, no USB ISP, and requires application-controlled bootloader entry. WCH-Link/wlink SWIO is a separate route. Hardware-unverified. |
| MSP430 mspdebug | Addressed HEX/ELF `prog` then target-memory `verify`, with `-n` disabling startup scripts | Explicit rf2500, olimex variants, uif, goodfet or mehfet driver; serial/USB as supported by that driver. BSL drivers with automatic mass erase/unlock and vendor tilib are refused. The Store builds pinned MSPDebug 0.26 source; legacy libusb compatibility is a signed Alpine dependency. Hardware-unverified. |
| AVR UPDI | pymcuprog UART `write --verify` using an exact supported AVR and application-only HEX; installed avrdude `serialupdi` can also be selected | Android prepares the existing SHA-256-pinned MIT Python support wheels without downloading/licensing XC. Requires a compatible UPDI adapter/circuit and device module. No high-voltage activation, chip unlock, user-row, fuse or explicit chip-erase operation. Hardware-unverified. |
| PIC AN1388 HID/UART, MCC UART, PKOB nano, PICkit scripting, gpsim, Bridge | Existing PIC implementations are reused | See [PIC support](pic.md) for exact device/protocol/compiler evidence and vendor limits. Those historical tests do not verify newly added physical boards. |

### Automatic flashing-client installation

Opening a flash profile and choosing upload resolves its auxiliary Toolchain Store entry before requesting USB
permission. The following clients have declared, verified-download installation paths; physical flashing remains
hardware-unverified:

| Client | Installed source/distribution | Runtime and installation proof |
| --- | --- | --- |
| esptool | Official 5.6.0 ARM64/x64 standalone archives, SHA-256 pinned from release metadata | Shared Ubuntu/glibc, original bundled dependencies retained. Actual x64 download/CLI/PATH smoke passed locally; ARM64/native CI checks the same recipe. |
| picotool | Official Raspberry Pi 2.3.1 ARM64/x64 archives, SHA-256 pinned from release metadata | Shared Ubuntu/glibc. Uses the official binary rather than fetching a whole Pico SDK just to compile this host client. Actual x64 download/CLI/PATH smoke passed locally; ARM64 CI checks the same recipe. |
| bossac | Official BOSSA 1.9.1 source archive, SHA-256 pinned from downloaded bytes | Native Alpine C++ build, version fixed explicitly because source archives have no Git metadata. Actual x64 source/build/PATH smoke passed locally; musl/ARM64 CI covers the production recipe. |
| teensy_loader_cli | Official 2.3 source archive, SHA-256 pinned from downloaded bytes | Native Alpine C build using signed libusb-compat development packages. Actual x64 compile/PATH/MCU-capability smoke passed locally; source hash establishes the version. ARM64/musl recipe is exercised in CI. |
| wchisp | Official 0.3.0 source archive, SHA-256 pinned from downloaded bytes | Native Alpine Rust build with `--locked --no-default-features --jobs 2`. Cargo.lock pins/checks dependencies; dynamic system libusb enables the descriptor bridge. Actual x64 source/version/PATH/dynamic-link smoke passed locally; ARM64/musl recipe is exercised in CI. |
| mspdebug | Official v0.26 source archive, SHA-256 pinned from downloaded bytes | Native Alpine C build, readline omitted, libusb-compat dynamic backend. Actual x64 source/version/PATH smoke and the tool's simulator program/verify/mismatch checks passed locally. Source, license and upstream TI3410 transport firmware are retained. ARM64/musl recipe is exercised in CI. |
| pymcuprog UART UPDI | Existing 3.19.4.61 MIT wheel and support wheels, SHA-256 pinned | Existing PIC support downloader; no XC compiler or XC license download. |
| pc-nrfutil 6.1.7 | Byte-identical self-contained legacy executable inside the SHA-256-pinned official Nordic nrf5sdk-tools 1.1.0 bundle | Downloaded directly from Nordic on demand; complete licenses/docs retained. Native x64 or explicit ARM64 Box64 with a separately compiled guest serial shim. Native x64 synthetic Secure DFU succeeds and bad CRC fails; actual ARM64 ABI qualification gates listing the Store entry. |

Installers validate the native executable/capabilities before creating PATH aliases. Sources and upstream licenses
are retained. CI runs the same helpers on x64 and ARM64 and checks that native USB clients link the expected dynamic
USB backend. A successful host install does not prove execution on a physical Android tablet or target board.

The ESP-IDF 5.4.1 SDK recipe also fixes all 58 core/build Python distributions to exact releases and actual
PyPI SHA-256 hashes for native Ubuntu CPython 3.12 ARM64/x64. Its only source archive, esptool 4.12.0, builds
with locked setuptools/wheel and no isolated dependency downloads. The app installs a frozen vendor v5.4
constraints file and runs the unmodified upstream checker offline; it does not fetch rolling Python constraints
or run `install-python-env`. Clean x64 installation, `pip check`, upstream version checks and ARM64 archive/hash
selection passed locally. Actual SDK firmware builds use the separate production installer CI matrix;
project-selected components keep their own dependency manifests/locks and licenses. Pico SDK, Zephyr and
embedded Rust are offered in the Store after their production installer and actual firmware builds passed
on native ARM64 and x64 CI runners; these host CPU checks do not qualify physical Android USB operation.

### ESP build metadata and first upload

ESP32/S3/C3 PlatformIO starters include a post-build exporter that reads the actual resolved SCons uploader
arguments and image offsets. Existing ESP-IDF projects use their generated `flasher_args.json` and actual
`config/sdkconfig.json`. After a successful build, F5 can create a profile from validated IDF metadata; it does
not infer bootloader, partition or application offsets from the USB adapter or chip name.

Set `EspFlasherArguments` in a profile to select this route explicitly. Every image path must stay within the
build output; offsets and erase sectors must be disjoint, actual partition-table records must agree, and the
application must fit its real application partition. The allowed mode/frequency/size settings accompany one
`write-flash` operation and separate `verify-flash` operations for each image. Unmentioned sectors are preserved:
there is no merged image padded with `FF` across NVS or filesystem gaps. NVS, keys, data partitions, secure boot
and flash-encryption configurations are refused, including configurations that could burn eFuses on first boot.
All image and metadata files are SHA-256 bound and rechecked between operations. A saved bundle digest identifies
the ordered inputs, and the remembered target comes from the checked metadata. Actual reset/USB programming is
hardware-unverified.

### Additional route configuration

For BOSSA, `binaryAddress` is the application's actual target address. `flashBaseAddress` is the physical start of
main flash (zero by default for SAMD/SAME; SAM3X uses `524288`). The planner passes their checked difference to
`bossac -o`; it never silently writes a bootloader-offset image at offset zero. Page alignment is checked by bossac
against the connected chip's actual geometry. Whole-flash erase and GPNVM boot-configuration flags are omitted.

The flash-profile `protocol` accepts `Teensy`, `WchIsp`, `MspDebug`, `NrfDfu` and `PymcuprogUpdi` in addition to
previous routes. Installable routes prepare their declared Store client automatically and use the same parsed-image boundaries and content-stability checks. Normal transfer-only
outcomes remain distinct from target-verified outcomes; a successfully launched process is not sufficient.

- **Teensy:** set `target` to the exact logical board ID, such as `TEENSY40` or `TEENSY41`, and use the correct
  board's application HEX and flash map. The installed loader must use the compatible libusb backend on Android.
- **WCH ISP:** set `target` to the ISP chip-name prefix, `binaryAddress` to `0`, and use a raw BIN. Set
  `useSerialTransport: true` with an explicit serial port for UART ISP; otherwise a permissioned worker exposes
  USB device index zero. This is the ROM ISP tool, not WCH-Link SWIO. A dynamic-libusb build of wchisp is needed
  for the libusb descriptor bridge; its default vendored/static library build is not claimed compatible there.
- **MSP430:** set `programmer` to the supported mspdebug driver and supply HEX/ELF. `useSerialTransport` selects
  UART access for a serial-capable driver; a USB probe can use `probeSelector` as its serial. `mehfet` also needs
  the exact `usbSelector` VID:PID. No startup scripts, forced target IDs or BSL unlock sequence are executed.
- **Nordic Secure DFU:** the optional Store route installs the original **pc-nrfutil 6.1.7** executable from
  Nordic's official complete bundle under its license, retaining all original files. The legacy bundle has no native
  ARM64 release; that route uses explicit Box64 and the matching x64 serial ABI, qualified separately on CI. Supply an application-only
  ZIP and the actual `binaryAddress`/`applicationFlash` partition selection. Only three flat files are accepted:
  `manifest.json`, application BIN and init DAT. The DAT must describe an application with the matching size and
  SHA-256, without debug bypass, SoftDevice or bootloader bytes. A signature envelope is parsed without asserting
  local signature trust; the configured target bootloader performs that check. `nrfDfuUart` selects UART ping
  framing; its default is native CDC USB-serial. `hardwareFlowControl` enables RTS/CTS only when correctly wired.
- **UART UPDI:** set `target` to the exact supported `ATtiny...`, `ATmega...` or `AVR...` part. The profile selects
  the application flash interval; any HEX data outside it is refused. The existing pinned Python support is
  prepared automatically on Android. This does not install an initial bootloader or unlock a protected part.

CLI contracts were checked against [PJRC's loader usage](https://github.com/PaulStoffregen/teensy_loader_cli),
[wchisp](https://github.com/ch32-rs/wchisp), [MSPDebug's manual](https://github.com/dlbeer/mspdebug/blob/master/mspdebug.man),
[Nordic's legacy CLI and init format](https://github.com/NordicSemiconductor/pc-nrfutil), and
[Microchip's pymcuprog CLI](https://github.com/microchip-pic-avr-tools/pymcuprog). The declared native installation checks and
physical-board validation remain separate; the command/fixture tests do not stand in for either.

## Board starters

These are project starters with real upstream board identifiers, not a database of verified physical boards.
All need the selected core/SDK and dependencies. Existing profiles for known AVR bootloaders include their
published application limits; other boards require a reviewed flash profile rather than a guessed memory map.

| Board | Starter/profile | Verification |
| --- | --- | --- |
| Arduino Uno | PlatformIO `uno`; Optiboot application profile | Template/profile contracts tested; hardware-unverified. Public AVR build job configured. |
| Arduino Nano | PlatformIO `nanoatmega328new`; new bootloader profile | Template/profile contracts tested; old-bootloader clones require an explicit choice; hardware-unverified. |
| Arduino Mega 2560 | PlatformIO `megaatmega2560`; STK500v2 profile | Template/profile contracts tested; hardware-unverified. |
| Arduino Leonardo/Micro | PlatformIO `leonardo`/`micro`; AVR109 touch profiles | Profile contracts tested; hardware-unverified. |
| ESP32 DevKit | PlatformIO `esp32dev` | Starter emitted; physical module/flash layout must be selected; hardware-unverified. |
| ESP32-S3 DevKitC-1 | PlatformIO `esp32-s3-devkitc-1` | Template contracts tested; flash/PSRAM variants are distinct; hardware-unverified. |
| ESP32-C3 DevKitM-1 | PlatformIO `esp32-c3-devkitm-1` | Template contracts tested; hardware-unverified. |
| Raspberry Pi Pico | PlatformIO `pico` | Starter emitted; hardware-unverified. Pico SDK public build job is a separate proof. |
| Raspberry Pi Pico W | Pico SDK `pico_w` | Template/detection contracts tested; requires installed SDK; hardware-unverified. |
| Raspberry Pi Pico 2 | Pico SDK `pico2` | Template/detection contracts tested; ARM/RISC-V selection matters; hardware-unverified. |
| Raspberry Pi Pico 2 W | Pico SDK `pico2_w` | Template/detection contracts tested; requires a supporting SDK; hardware-unverified. |
| STM32 Nucleo | PlatformIO `nucleo_f401re` only | Template contracts tested; other Nucleo chips need their own selection; hardware-unverified. |
| STM32 Blue Pill | PlatformIO `bluepill_f103c8` | Starter emitted; chip authenticity/capacity cannot be inferred from the board name; hardware-unverified. |
| STM32 Black Pill | PlatformIO `blackpill_f411ce` | Starter emitted; F401/F411 are different selections; hardware-unverified. |
| nRF52840 DK | Zephyr `nrf52840dk/nrf52840` | Exact standalone nRF52840, 1 MiB flash/256 KiB RAM. The PlatformIO Arduino board currently selects an nRF52832 linker/core layout, so this starter uses Zephyr. A project requiring a SoftDevice needs its own partitioned layout. Hardware-unverified. |
| Seeed XIAO | PlatformIO `seeed_xiao`, SAMD21 only | Template contracts tested; ESP/nRF/RP XIAO variants need distinct definitions; hardware-unverified. |
| Teensy 4.0/4.1 | PlatformIO `teensy40`/`teensy41` | Starters plus explicit HalfKay HEX transfer. No readback/production verification. Hardware-unverified. |
| CH32V003 | Pinned upstream ch32fun startup/linker/headers, native RV32EC GCC, UART greeting and bounded WCH ISP profile | Actual native build proof: 732 B flash, 16 KiB code flash/2 KiB RAM map. UART TX PD5 at 115200. ROM UART ISP must already be active; V003 has no BOOT pin and this does not provide initial WCH-Link SWIO programming. Hardware-unverified. |

## Flash safety, backups and production work

Upload parses the actual HEX/BIN/ELF/UF2 image or the allowed Nordic application-only ZIP, validates its programmed bytes against explicit application ranges,
and binds the plan to the firmware's SHA-256. It does not trust a caller-supplied image-size list. BIN requires an
explicit start address. Normal upload never accepts arbitrary extra programmer flags for fuses, option bytes,
read-out protection or eFuses. The firmware must match the bootloader's application linker layout.

There is no general fuse/option-byte programming UI. The configuration-policy API requires an exact confirmation
describing the selected device, register, old/new values, reversibility and consequences for any future supported
configuration operation. It refuses eFuse burning and permanent read-out protection/RDP level 2 outright.
The SVD field editor also refuses configuration and side-effect registers; loading an SVD never authorizes a write.

Application address bounds alone cannot recognize security words embedded inside normal program flash.
Generic OpenOCD/probe-rs plans therefore inspect actual BIN/HEX/ELF bytes using a reviewed, exact-part policy:

| Reviewed parts | In-image check | Current supported application values |
| --- | --- | --- |
| LPC1769/68/67/66/65/64/63 and LPC1759/58/56/54/52, plus the explicitly listed package identifiers in `EmbeddedConfigurationGuard` | CRP word at `0x2FC` | Complete `0x00000000` or `0xFFFFFFFF`. CRP1/2/3, partial words and other unreviewed values are refused before erase. LPC1751 is excluded because support depends on its silicon part ID. |
| MK64FN1M0VLL12/VDC12/VLQ12/VMD12 and MK64FX512VLL12/VDC12/VLQ12/VMD12 | FCF at `0x400..0x40F` | All 16 bytes must be present: `FF FF FF FF FF FF FF FF FF FF FF FF FE FF FF FF`. An erased `FSEC=FF` enables security; it is refused. |

Sparse holes remain unknown rather than being filled with invented current-device values. K64 partial images
without the complete FCF are currently refused, including bootloader-offset updates, because a generic erase
plan cannot prove configuration preservation. Known `lpc17xx.cfg`/`kx.cfg` scripts require a reviewed exact
part. These checks do not silently rewrite the firmware or offer a confirmation bypass: the ordinary upload
has no live old-value read with which to authorize a configuration change. Other targets and custom scripts
need device-specific review; this small policy registry is not a universal security-word detector.

The policies follow NXP [UM10360 §32.6/Table 568](https://www.keil.com/dd/docs/datashts/philips/lpc17xx_um.pdf),
[AN4835 §5.1](https://www.nxp.com/docs/en/application-note/AN4835.pdf),
[AN4507 Tables 1/4](https://www.nxp.com/docs/en/application-note/AN4507.pdf), and the exact ordering table in
the [K64 datasheet](https://www.nxp.com/docs/en/data-sheet/K64P144M120SF5.pdf).

**Backup firmware** reads one explicitly mapped application interval twice using probe-rs, OpenOCD, stm32flash or
readback-capable STM32 DFU. Guarded WCH-Link uses two fresh read-only CH32V003 sessions. It only saves a new backup when both reads agree and match the expected byte count.
Read protection is never removed. **Compare firmware dumps** reports whole-file SHA-256 hashes, the exact number
of differing bytes and bounded contiguous difference ranges, including unequal file lengths.

**Production batch** selects attached devices with the same VID/PID, asks the operator to confirm the exact MCU
and layout, opens separate Android permissions/connections, and programs at most two concurrently. Each result
must verify independently; failures remain per-board failures. `.tabstudio/production/*.jsonl` records device IDs,
firmware digests, outcomes and errors. Identical VID/PID is an operator selection, not proof of identical silicon.
Probe-rs workers each receive a private single-device usbfs inventory and a selector rebound to their actual
granted VID/PID, preserving any explicit FTDI channel. The immutable reviewed firmware digest must match every
worker. Probe-rs uses explicit ELF format, preverification, preservation of unwritten sector bytes, verification
and reset; inherited erase/power/reset/preset overrides are cleared. Touch/re-enumerating bootloaders, HID-only
probe-rs devices, guarded multi-session WCH-Link and transfer-only loaders such as Teensy HalfKay are refused in
batches before programming. No real USB hub has been verified yet.

**Upload over Wi-Fi** implements ArduinoOTA-compatible transfer and old MD5/new PBKDF2 challenge formats, requires
the target's final acceptance, and keeps the entered password in memory for that upload. The board must already
run an OTA-capable application with a suitable partition layout. Its acknowledgment is not physical flash
readback or a security guarantee from TLS; this is the upstream OTA protocol, not a bootloader installer.

**Upload UF2 via BOOTSEL storage** uses Android's granted USB connection without mounting a disk or requesting
root. It accepts only `2e8a:0003`/`2e8a:000f`, a SCSI Bulk-Only interface and the matching `INFO_UF2.TXT` read
from the actual volume. The selected project's flash profile must name its `.uf2` and exact application ranges.
Every block must contain the correct RP2040/RP2350 family, a unique aligned 256-byte payload and an address
inside both those ranges and the conservative first 16 MiB XIP window. RAM, OTP, non-main-flash, mixed-family,
extension and custom white-labeled bootloader containers are refused by this route.

The block worker issues bounded SCSI INQUIRY, READ CAPACITY, READ(10) and WRITE(10) commands through the existing
USB descriptor bridge. Its read-only phase checks the actual 512-byte BPB and FAT12/16/32 allocation, mirrors or
active FAT, root entries and file chains. A superfloppy or one ordinary FAT MBR partition is accepted; GPT,
extended partitions, repair, formatting and overwriting existing files are never requested. It allocates free
clusters, stages the FAT copies, applicable FAT32 FSInfo and a new short root filename, then submits the UF2.
Failures attempt metadata rollback when transport is still available; that cannot undo target flash already
programmed by ROM. Pre-write cancellation sends no writes.

RP BOOTSEL is a synthetic filesystem that consumes UF2 rather than an ordinary persistent USB disk. The result
therefore reports USB command acknowledgment and optional filesystem-sector readback separately, and never
labels either as MCU flash verification. A disconnect after the last payload but before its CSW is explicitly
**submitted, final status unavailable**; it is not successful programming proof. Physical ROM behavior, USB
controllers and reset timing remain hardware-unverified.

## On-chip debugging, registers and RTOS tasks

**Debug on chip** uses the selected backend and matching ELF. `Backend: "ProbeRs"` starts the pinned official
probe-rs DAP server with an explicit `Chip`, through the permissioned nusb inventory. Its attach configuration
disables flashing, full erase, unlock permission and connect-under-reset overrides. Official DAP initialization
is tested on native ARM64/x64; a physical probe session is hardware-unverified.

The OpenOCD fallback starts the selected installed interface/target configuration through the permissioned libusb
connection. The GDB endpoint binds to loopback; Tcl/telnet endpoints are disabled. A
Python-enabled, all-target GDB 14+ with its real `gdb.dap` adapter attaches through TabStudio's existing DAP client.
GDB capability/module checks reject an unsuitable installation before claiming a debug session.

`.tabstudio/embedded-debug.json` contains the exact installed configuration names and matching ELF:

```json
{
  "Firmware": "build/firmware.elf",
  "InterfaceConfig": "interface/stlink.cfg",
  "TargetConfig": "target/stm32f4x.cfg",
  "FaultModel": "cortex-m-v7"
}
```

This attaches and halts; it does not silently flash a new program. Upload the matching image first. An optional
`ProbeSelector` selects an exact serial. Installed configuration files are constrained to the tool's scripts;
the typed profile does not accept arbitrary project Tcl or memory-write startup commands.

The existing DAP UI provides breakpoints, stepping, call stack and locals. DAP memory read/write transport has
strict length/address checks; the static memory and disassembly commands below use the corresponding protocol requests.
The QEMU/simavr tests exercise the real adapter/client path, not OpenOCD's physical probe transport. Availability
of a particular peripheral, multicore target or architecture still depends on OpenOCD/GDB support and wiring.

**View > Device Peripherals** opens the docked register browser. Peripheral/register selectors and named bit
fields display actual vendor metadata; changing selection performs no target access and reset defaults are never
presented as live values. **Read selected register** takes one halted snapshot, labeled with its epoch. **Edit
field** uses the same guarded write policy as the command below. There is no background polling.

**Load CMSIS-SVD** reads a local vendor file with DTD/entity resolution disabled and bounded expansion. Register
inspection names bitfields and enumerations, uses the selected SVD's address/width and requires an explicitly
little-endian halted target. Write-only registers and documented read side effects are not automatically read.

**Edit register field** additionally requires `.tabstudio/peripheral-access.json`: a reviewed device/register/address
allowlist. It refuses configuration/side-effect registers, previews the exact old/new value, requires that exact
operation to be typed, checks the register has not changed, writes, and reads back. This is an explicit register
operation in the dock or command picker. A metadata reload, core/thread change, resume or subsequent stop
invalidates pending inspection/confirmation; writes still require the current register value to match the preview.

Editor hover on an identifiable `PERIPHERAL->REGISTER` access adds SVD descriptions. **Load register header** reads
the actual chip header; **Ctrl+.** on an eligible literal mask offers an existing macro only when its resolved
definition equals the SVD field mask. It does not invent vendor macro names. Full register-aware completion across
arbitrary HAL abstractions/macros is not implemented.

**Read target memory** and **Disassemble target code** are static paused snapshots through the actual DAP
`readMemory` and `disassemble` requests. Enter an unsigned decimal/hex address and a bounded count (up to 4096
bytes or 256 instructions). Memory appears as hex bytes and printable ASCII; disassembly retains actual opcodes,
symbols, unavailable-instruction markers and existing mapped source links in Output/ informational Error List
rows. The adapter must advertise support. If the target resumes, stops again or disconnects while a request is
pending, the mixed/stale result is discarded. These commands do not poll, resume the CPU or write memory.

**RTOS threads** reads halted FreeRTOS lists or Zephyr's monitored thread chain through the firmware's actual
debug types and symbols. Names, states, priorities and available stack extents are bounded/cycle checked. All
cores must be halted. Zephyr needs thread monitoring; missing types/configurations are reported. The UI can scan a verified stack-fill pattern from `.tabstudio/embedded-rtos.json`; it never guesses the
initialization pattern or growth direction. Per-stack, total-byte, thread-count and timeout limits bound reads,
and optional reviewed RAM intervals refuse scans outside known stack memory. Per-task free-stack thresholds
produce Error List warnings; missing/ambiguous tasks, extents or evidence remain unverified. The actual DAP
`allThreadsStopped` event claim is reported separately from the requirement to halt physical participating cores.

An explicit profile can supply the actual facts below; these illustrative values must match the built kernel:

```json
{
  "kind": "FreeRtos",
  "stackFillByte": 165,
  "stackGrowsDown": true,
  "maximumThreads": 256,
  "maximumStackScanBytes": 65536,
  "maximumTotalStackScanBytes": 1048576,
  "timeoutSeconds": 30,
  "taskLimits": [{ "name": "Sensor", "minimumFreeBytes": 256, "expectedStackBytes": 2048 }]
}
```

The validated legacy `.tabstudio/rtos.json` fill/growth profile remains supported with a migration note. New
profiles take precedence. A verified fill scan estimates the minimum unused initialized extent; it does not
measure future stack demand or prove whole-system interrupt overhead.

**Start RTT/defmt logs** runs the official probe-rs 0.32 `attach` command and streams its native decoded output
into Output. Select `Backend: "ProbeRs"`, the exact `Chip` and the ELF for the firmware already running in
`.tabstudio/embedded-debug.json`. On Android, the selected USB probe uses the permissioned inventory/FD bridge;
desktop hosts use their installed probe-rs and an explicit `ProbeSelector`. Keep the writable `_SEGGER_RTT`
control-block symbol in the ELF. TabStudio reads that actual symbol instead of guessing a RAM scan range.
The upstream decoder uses the matching ELF's defmt metadata for the RTT channel named `defmt`; other channels
use upstream's ordinary channel handling. No custom defmt decoder or generated example log is substituted.

This command does not build, flash or request reset/erase/unlock. It uses upstream's `AttachToRunning` monitor,
which preserves the existing RTT header. Attaching can resume the target; vector catches are disabled and RTT
uses `no-block-skip` mode so a full buffer can drop logs rather than stall firmware. The native tool selects
the RTT core from the actual control-block address and chip memory map; the DAP `CoreIndex` is not a CLI logging
override. Stop disconnects the logger and releases its USB connection/inventory. A separate logger is refused
while a debugger owns the probe. Command construction and metadata guards have unit tests; physical acquisition
and decoding from a real target remain hardware-unverified.

## Memory, crash, stack and pin analysis

**Memory report** reads linked ELF32/64 in either byte order. It counts allocated load ranges for flash and RAM
ranges at runtime, including initialized RAM's flash image but excluding debug data and overlapping symbol
aliases from totals. Exact memory regions override the explicitly labeled writable-flag inference. Section
treemaps, the largest symbols and GNU-map object/archive contributions open in the existing web preview; reports
remain local under `.tabstudio/memory-report/`. Post-build Output/status show signed byte changes from the previous
report. Use the same memory-region settings when comparing builds. Unreserved heap/stack growth is not a linked
allocation, and unknown symbol source ownership is not invented.

`.tabstudio/embedded-analysis.json` can select artifacts and supply analysis facts:

```json
{
  "Firmware": "build/firmware.elf",
  "MapFile": "build/firmware.map",
  "Addr2Line": "arm-none-eabi-addr2line",
  "MemoryRegions": [
    { "Name": "FLASH", "Origin": 134217728, "Length": 524288, "IsRam": false },
    { "Name": "RAM", "Origin": 536870912, "Length": 131072, "IsRam": true }
  ],
  "StackUsageFiles": ["build/main.su"],
  "StackCallGraphFiles": ["build/main.ci"],
  "StackCalls": [{ "Caller": "sensor_task", "Callee": "read_sensor" }],
  "CallGraphComplete": false,
  "TaskBudgets": [
    { "Name": "Sensor", "EntryFunction": "sensor_task", "AllocatedBytes": 2048, "ExtraBytes": 128 }
  ]
}
```

Those illustrative addresses are not a default board map. With no explicit ELF, a unique artifact in conventional
build folders is required; multiple artifacts are not guessed. A unique GNU map is used when no `MapFile` is
specified. ELF analysis is bounded to 128 MB and 262,144 symbol entries; linker maps to 16 MB.

**Decode crash** consumes the active ESP panic text or a Cortex-M snapshot JSON object with decimal `Cfsr`, `Hfsr`,
`Mmfar`, `Bfar` and optional `StackedWords` (`r0-r3,r12,lr,pc,xPSR`). Flags produce plain-language Error List entries.
Unaligned access, division by zero and valid fault addresses are evidence; a possible null access is labeled as
possible. Its JSON input contains the eight core exception words. In a supported basic or extended hardware frame
these words begin at SP; extended floating-point state follows them, so adding 72 bytes to SP would decode the
wrong core frame. Captured PC/LR are not a reconstructed complete unwind. ESP PC:SP pairs preserve only PCs. For captured classic AVR reset flags, **Decode crash** accepts `{"format":"avr-mcusr","mcusr":12}` and places the
actual brown-out/watchdog flag meanings in the Error List. It does not read or clear MCUSR automatically; newer
AVR families with different reset-status registers need their documented layout. Exact source locations require the matching firmware ELF and configured
addr2line; unknown locations stay empty.

**Capture live fault** is a manual, read-only command for a paused on-chip GDB session. `FaultModel` must explicitly
select `cortex-m-v7` (supported M3/M4/M7 CPUIDs) or `cortex-m-v8-mainline` (supported M33/M35P/M55/M85 CPUIDs).
Cortex-M0/M0+, baseline and unknown cores are refused before CFSR access. The current profile, probe configuration
and ELF must match the attached session; the ELF's SHA-256 must remain unchanged. Each DAP read checks that the same
halt epoch is still active. Captured evidence is atomically saved under `.tabstudio/faults/` and decoded into the
Error List, using that session's exact ELF for configured addr2line resolution.

Capture reads actual CPUID/ICSR/VTOR, CFSR/HFSR/MMFAR/BFAR and top-frame LR/MSP/PSP/PC. Frame recovery additionally
requires the live PC to equal the active fault vector's entry and LR to be a known same-security EXC_RETURN value.
Use the probe's exception-entry halt or an instruction breakpoint at that entry; a source breakpoint after a
handler prologue may already have moved SP. Otherwise only fault status is reported, with the reason no frame was
decoded. EXC_RETURN selects MSP/PSP and basic/extended extent; the core words remain first in both layouts, and
FP/reserved words are excluded. Stacking-fault flags, security transitions and additional-state layouts suppress
frame recovery. Flags can be latched from an earlier fault: capture does not clear them, write target memory or
resume the CPU. After exact session/profile/ELF binding, an actual GDB DAP **exception** stop automatically checks the supported
core's ICSR. Active exceptions 3–6 trigger the same read-only capture; thread mode, NMI, SVC, SysTick and IRQ
stops do not publish old latched fault flags. Initial attach stops arriving before binding and ordinary breakpoint
stops remain manual. The selected core's frame must still satisfy the entry/EXC_RETURN checks. This is not a full
crash-unwinder or physical hardware verification.

Frame layout reference: Arm's [Exception Model User Guide, Figure 4-3](https://community.arm.com/cfs-file/__key/communityserver-discussions-components-files/471/armv8_2D00_m_5F00_exception_5F00_model_5F00_user_5F00_guide_5F00_107706_5F00_0100_5F00_01_5F00_en.pdf).

**Stack analysis** reads GCC `-fstack-usage` `.su` rows and imports final compiler call graphs emitted by
`-fcallgraph-info=su` in `.ci` files, computing the deepest bounded known call chain. Conventional build roots are
searched automatically; `StackUsageFiles` and `StackCallGraphFiles` can select exact artifacts. Compiler nodes bind
only when their source/name/line/column/frame size/boundedness match the current `.su` evidence. Direct edges,
indirect placeholders and compiler-inserted library calls are retained; stale or ambiguous nodes stay unknown.
Manual `StackCalls` can supply additional reviewed edges. Imported graphs always retain unknown whole-program
completeness, even if `CallGraphComplete` was set; a proven partial chain can still warn that a task is too small. The default does not assert graph completeness. Missing library frames, indirect calls (`Callee: null`),
recursive cycles, ambiguous static names and unbounded dynamic allocations remain unknown. Task checks add the
explicit byte allowance before comparing to the allocation. Configured task budgets run after each successful
firmware build and retain the memory report in the status bar; the full manual command remains available. Interrupt nesting, assembly, RTOS context frames and
other ABI overhead need reviewed allowances; a source parser cannot prove these from a function name. Link-time optimization may emit several `.ci` files; include every relevant output. The compiler dump does not
prove that separately built libraries, assembly, interrupt nesting or all dynamic dispatch are covered.

**Check pin conflicts** reads explicit CubeMX `.ioc`, selected devicetree/overlay forms and simple Arduino pin
uses, placing conflicts in the Error List. `.tabstudio/pin-map.json` can provide the actual chip's valid pins;
without it, nonexistent-pin validation is not claimed. Dynamic expressions, aliases, includes, full preprocessor
evaluation and every Zephyr pinctrl dialect are outside this bounded static check.

## Tests on the host and board

**Run embedded tests** runs an already built host Unity/TAP/GoogleTest executable or Cargo's actual host target.
`.tabstudio/embedded-tests.json` names the format, executable/arguments and host identity. Arguments remain
separate strings; Rust host tests explicitly override an inherited bare-metal target. GoogleTest gets a fresh XML
report per run. Nonzero exits, truncated output or incomplete harness summaries cannot become a passing run.

**Run board tests** uses the pinned official `probe-rs run` to flash, verify, reset and execute one explicitly
selected board's already-built test ELF. The same `.tabstudio/embedded-tests.json` supplies `Format`, the actual
`BoardId`, `TimeoutSeconds` (5–3600), `RttChannel` (0–15), and optionally `RttChannelName`. Set `Format` to `Unity`
for RTT text, `DefmtTest` for the native ELF-backed defmt decoder, or `Rust` for upstream's native `embedded-test`
harness. The exact test ELF and chip must agree between the `ProbeRs` flash/debug profiles. Its actual load
segments are checked against the configured application-flash map, copied to private storage and SHA-256 checked
again before invocation. The command requests preverification, post-flash verification and restoration of bytes
outside the image in erased sectors; it never requests chip erase or protection changes.

The full bounded stream feeds Test Explorer, retaining `BoardId` separately from the probe selector. Unity and
old defmt-test require the matching ELF's actual `_SEGGER_RTT` symbol and stop the monitor after their explicit
final summary. RTT blocks when full to preserve test records. A multi-channel image may need its exact
`RttChannelName` (`Terminal` for Unity and `defmt` for DefmtTest by default) to remove upstream's channel prefix.
Native `embedded-test` requires its real ELF metadata and a successful native command exit. Stop, timeout,
disconnect, lost summary and oversized/truncated output become errors, never inferred passes. Android uses the
same granted bulk-USB/nusb bridge; active debugging must be stopped first. Managed tests exercise real-form
harness streams through a fake process runtime. Physical flash, RTT acquisition and tests remain hardware-unverified.

**Run Unity tests over serial** captures one run from already-programmed firmware over the permissioned USB
serial bridge. Its separate `.tabstudio/embedded-serial-tests.json` requires the firmware's actual baud rate:

```json
{
  "BaudRate": 115200,
  "TimeoutSeconds": 60,
  "InterfaceNumber": 0,
  "BoardLabel": "Sensor prototype"
}
```

Omit `InterfaceNumber` only when the adapter's data interface is unambiguous. Start the firmware's harness after
Output says **Serial Unity capture ready**. Capture does not build, upload, send serial input or intentionally
change DTR/RTS, send break, reset the adapter or pulse modem outputs during cleanup. Required adapter and
baud/line-coding initialization still occurs; retention of an unknown adapter's electrical state is not guaranteed.
The full bounded strict-UTF-8 stream must include individual Unity results, matching summary counts and a final
newline-terminated `OK`, `FAIL` or `FAILED`. Stop, timeout, disconnect and incomplete runs are errors. Results retain
the actual selected USB VID/PID, attachment path and configured interface separately from the optional label;
these identify that attachment, not its MCU or installed firmware, and are not persistent identities after replug.
Synthetic streams exercise the actual PTY, native control shim and production receive-only client; physical board
capture remains hardware-unverified.

**Import test results** also reads an actual Unity, TAP, defmt-test, Rust text log or GoogleTest XML report and
retains the supplied board identity in Test Explorer. A host result is never labeled as a physical-board result.

## Verification and reproducibility

The dedicated [embedded workflow](../../.github/workflows/embedded.yml) is configured for push and pull-request
runs. Check the final PR SHA for completed results; adding a workflow is not evidence that it passed.

| Check | What it actually establishes |
| --- | --- |
| Embedded library tests | Parsers, safe image bounds and command plans, device associations, backups/diffs, OTA protocol contracts, SVD/pins/RTOS readers and test-result parsing. Analysis tests also read a real host ELF. |
| Native USB tests | An unchanged compiled POSIX C client uses the real PTY/shim; synthetic adapter requests and mock libusb ABI ownership are replayed. No physical captures. |
| BOOTSEL storage tests | Synthetic CBW/CSW and SCSI transcripts; FAT12/16/32 superfloppy/MBR images, mirrors/active FAT, packed FAT12 entries, FAT32 FSInfo, cancellation, refusal and rollback. GNU mtools independently reads six generated files byte-for-byte. No physical USB or flash verification. |
| IDE tests | Template/configuration contracts, actual analysis/test commands, treemap escaping/proportions, Test Explorer results and screenshots. |
| Cortex-M emulator | Real ARM cross-compile, QEMU Cortex-M3, GDB DAP and TabStudio DAP client: source breakpoint, stack, locals, stepping and memory. Locally exercised; not a physical STM32/probe test. |
| RISC-V emulator | Real RV32 cross-compile, QEMU, GDB DAP and TabStudio DAP client with the same debugger checks. Locally exercised; not an ESP32/CH32 hardware test. |
| AVR emulator | Real AVR compile, simavr/ATmega328P and AVR-enabled GDB 15 DAP passed locally: source breakpoint, stack, locals, stepping and instruction output. A suitable GDB is built from signed Ubuntu source because distro multiarch omits AVR and older AVR GDB lacks DAP. No physical AVR claim. |
| Android self-test | Production serial PTY/control bridge against a virtual backend inside the app's PRoot; explicit marker required. Does not connect to USB hardware. |
| Real public repositories | Seven pinned native build jobs with a zero-error baseline; these validate host ecosystem builds, not ARM64 Android SDK installation or USB programming. |

The embedded entries in [`build/ci/real-repos.json`](../../build/ci/real-repos.json) cover PlatformIO AVR Blink,
Arduino LiquidCrystal's HelloWorld, an STM32Cube-generated build, ESP-IDF hello_world for ESP32-C3, Pico examples,
Zephyr hello_world and an Embassy RP example. [`run-real-repos.py`](../../build/embedded/run-real-repos.py) retains
logs, results and generated dependency locks. Source commits are pinned; where an upstream ecosystem lacks a
complete transitive lock, a source SHA alone does not make every package resolution immutable.

Local reproduction:

```sh
python3 -m unittest discover -s build/embedded/tests -p 'test_*.py' -v
dotnet test tests/TabStudio.Toolchains.Tests --filter FullyQualifiedName~Embedded
dotnet test tests/TabStudio.LanguageServers.Tests --filter FullyQualifiedName~Embedded
dotnet test tests/TabStudio.App.Tests --filter FullyQualifiedName~Embedded
TABSTUDIO_FAT_PROOF=/tmp/tabstudio-fat-proof \
  dotnet test tests/TabStudio.Toolchains.Tests --filter FullyQualifiedName~StorageTests
python3 build/embedded/verify-fat-images.py /tmp/tabstudio-fat-proof
TABSTUDIO_EMBEDDED_EMULATORS=1 TABSTUDIO_REPO_ROOT="$PWD" \
  dotnet test tests/TabStudio.LanguageServers.Tests --filter FullyQualifiedName~EmbeddedQemuTests
```

The independent FAT proof requires GNU mtools (`mtype`). It reads only generated regular image files, never a
hardware disk. The emulator command requires the cross compilers, QEMU, simavr and a suitable GDB on the host. The CI workflow
lists those packages. Synthetic fixtures are described in [USB bridge verification](../../build/embedded/tests/README.md).
Real hardware captures still need the exact device/probe firmware, target part, tool version, firmware digest,
redistribution permission and expected result; none have been fabricated for this change.

## Native-first PlatformIO packages

On Android, `"hostPackageMode": "auto"` is the default in `.tabstudio/embedded.json`. Pinned PlatformIO
Core 6.1.18 prefers any compatible native version satisfying the project's requirements. If no compatible
ARM64 artifact exists, unchanged x86-64 host tools can run through the APK's pinned Box64. The app prepares
that translator before an auto-policy ARM64 build; native x64 builds need no translator downloads.
`"hostPackageMode": "native"` disables translation while retaining executable-architecture checks.
`"boxed-x86_64"` remains an alias for the earlier native-first fallback policy. The policy never changes the
tablet's global PlatformIO system type or substitutes an unrelated tool version.

Registry architecture labels are checked against executable ELF headers before package postinstall scripts
and before cached packages are used. An ARM64-labeled distribution containing an incompatible host ELF
(such as a 32-bit ARM executable) can retry only that exact package version's x64 registry artifact through
PlatformIO's normal SHA-256 downloader. The rejected native distribution is retained unchanged. Missing or
incompatible replacements fail visibly; foreign executables created by postinstall are also refused before
build. Shared libraries and cross-compiler object files do not receive executable launchers.

The boxed project gets a separate `.tabstudio/platformio-boxed` Core cache. The app reuses the APK's pinned,
patched Box64 and PIC's SHA-256-pinned Debian runtime archives without downloading XC or asking for an XC
licence. Its private ARM64 loader and libraries leave Ubuntu's loader/libc intact. Only the app's translator
copy has its existing ELF interpreter field retargeted to that private loader, so Box64-spawned children
use the matching loader too; package/compiler binary bytes are unchanged. The interpreter is selected
conservatively. Original package files remain unchanged in a retained sibling directory; a symlink overlay
adds native Python launchers only for x86 executables. GCC starts at its original path and finds its original
`cc1`, assembler and linker rather than recursively invoking wrappers. Boxed builds can be slower, and the
four runtime libraries cannot satisfy every arbitrary third-party dependency.

Native tests cover ELF32/ELF64 and PIE versus shared-library classification, corrupt-header refusal,
original-byte preservation and automatic/native-only process/configuration routing. The production glibc CI harness additionally requires the real x86 AVR GCC
to compile/link on both x64 and ARM64, runs the existing patched-Box64 32-child/static-spawn regression with
inherited FD 3, and compares the complete resulting AVR ELF bytes across runners. A separate loopback-registry fixture uses the
real pinned PlatformIO manager, HTTP mirror, archive and checksum path to require native download before
an exact-version x64 retry, refusal of the incompatible native postinstall, unchanged retained binaries and
independent native-only/postinstall-generated-executable refusal. These are CI requirements;
they are not physical tablet/USB qualification or a claim that every x86-only platform is compatible.

Frozen x64 serial clients additionally require the control shim compiled for x64. Their Box64 child clears
the native ARM `LD_PRELOAD` and uses only `BOX64_LD_PRELOAD` for that guest shim; the native worker keeps
ownership of USB and the PTY. CI compares every control RPC against an independent native C client,
including custom baud, parity/stop bits, DTR/RTS, break, flush/drain and HUPCL. It retains the shim's actual
glibc symbol-version requirements. A native ARM preload alone is not treated as proof of x64 serial support.

## Engineering decisions and remaining work

The useful additions are repeatable verified backups, exact dump comparisons, per-board production logs,
source-preserving vendor import, build-size regressions, explicit unknown stack bounds and safe register inspection.
They answer ordinary development questions without needing a second computer or measuring-instrument features.

The implementation reuses the Toolchain Store, PIC USB lifetime/drivers, existing terminal, clangd/rust-analyzer,
Error List, Test Explorer and DAP client. It does not introduce another process runtime or bypass Android USB
permissions. Official probe-rs DAP and the OpenOCD plus GDB DAP fallback reuse the same debugger UI. The separate PRoot usbfs bridge
has CI-verified static/raw FD routing and stock nusb enumeration, which establishes the transport foundation;
it does not establish a physical probe-rs programming or debug session.

Remaining work includes physical nusb/usbfs, HIDAPI and BOOTSEL storage I/O qualification, a native probe-rs hidraw
backend, exhaustive third-party board/core package compatibility, automatic interrogation on plug-in and a complete
device-association UI, WCH-Link SWIO/vendor debug support and exhaustive Nordic bootloader compatibility and complete crash
unwinding beyond supported exception-stop capture, complete call-graph proof and hardware RTT/defmt qualification,
parallel on-target test campaigns, F5 orchestration across every unsupported board/transport and physical-board
verification. The existing [Windows Bridge](../ARCHITECTURE.md#windows-bridge) remains available for workflows
that need vendor desktop software. See [third-party notices](../../THIRD-PARTY-NOTICES.md) for downloaded tools.
