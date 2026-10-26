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
builds/uploads and opens the Serial Monitor only for the same USB serial identity; without either profile it
builds firmware. Known Uno/Nano(new)/Mega/Leonardo/Micro starters include safe bootloader profiles. Other boards
still need explicit configuration, and physical reset/re-enumeration/monitor behavior is hardware-unverified.
The existing PIC F5 simulator and ordinary language F5 behavior remain documented separately.

### Project systems

| Project | Detection and native build | IntelliSense and limits |
| --- | --- | --- |
| PlatformIO | `platformio.ini`; `pio run`, preserving environments, inheritance, platform versions and native scripts | Existing `compile_commands.json` is preserved. Run `pio run -t compiledb` for exact clangd flags. Core is installed; every platform's ARM64/musl package availability is a separate question. |
| Arduino sketches and libraries | `.ino`, `sketch.yaml`, `library.properties`; `arduino-cli compile` with an explicit FQBN; a library requires an explicit `Sketch` example directory | The build first requests Arduino's compilation database. Install the selected board core/libraries separately. A library itself is not an executable sketch. |
| STM32CubeMX/CubeIDE | `.ioc` plus generated CMake or Make files; preserve their startup, CPU flags and linker script | CMake exports compilation commands. An Eclipse `.cproject` is not independently translated into a complete Cube build; generate/retain a portable build first. CubeMX's generator is not bundled. |
| ESP-IDF | IDF CMake markers, `sdkconfig`; `idf.py` with the selected target | Requires a matching installed ESP-IDF environment and target tools. No automatic Espressif glibc SDK installer is supplied yet. |
| Zephyr | west/Zephyr markers; `west build` with the exact board | Requires a prepared west workspace, matching modules and toolchain. No complete SDK installer is supplied yet. |
| Raspberry Pi Pico SDK | `pico_sdk_import.cmake` or Pico CMake usage; CMake with the selected `PICO_BOARD` | Requires the SDK and a `PICO_SDK_PATH`/project configuration visible to the build process. Pico/Pico W/Pico 2/Pico 2 W are distinct choices. |
| Rust embedded | Cargo with embedded target/linker markers, including `memory.x`; Cargo preserves its configuration and uses `--locked` when a lockfile exists | Existing rust-analyzer handles editing. Install the target's standard-library artifacts and linker tools separately. Stock Android probe-rs/nusb is not enabled by the libusb bridge. |
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
affect live debug state. Native HID-only CMSIS-DAP v1 is refused. Physical target interrogation is hardware-unverified;
automatic interrogation on plug-in and a complete device-association editing UI are not implemented yet.

### Probes and target families

| Probe | Current route | Verification and limits |
| --- | --- | --- |
| ST-Link V2/V3 | OpenOCD's supported libusb backend and installed interface/target scripts | Identity hints, safe plans and debug startup contracts tested locally; no hardware verification. Exact clone/firmware/target support belongs to the installed OpenOCD version. |
| CMSIS-DAP v2, DAPLink bulk interface | OpenOCD libusb route | Backend integration is implemented; no physical DAPLink verification. |
| CMSIS-DAP v1 HID, DAPLink HID interface | Requires a libusb-backed HIDAPI build | Native Linux hidraw/udev builds cannot use this bridge. No ready verified HIDAPI rebuild is provided. |
| Raspberry Pi Debug Probe/picoprobe | CMSIS-DAP/OpenOCD where that probe firmware and target are supported | Descriptor hint and command contracts only; hardware-unverified. |
| J-Link | OpenOCD's applicable open backend | USB hint only is not SEGGER software installation or proof of target support. No hardware verification. |
| ESP USB-Serial-JTAG | Serial download through an installed esptool; debug needs a supporting Espressif OpenOCD build | The stock Alpine OpenOCD package does not establish vendor-fork coverage. No hardware verification. |
| WCH-Link | Requires the matching WCH SWIO/JTAG implementation and target support | VID/PID hint exists. The matching vendor OpenOCD/wlink package and complete route are not supplied/verified. |
| probe-rs probes | Stock nusb bulk-USB transport foundation; native-host typed download plan | Permissioned inventory enumeration and raw FD routing are verified on CI. Android tool packaging/integration and physical probe/target I/O need separate qualification; native hidraw CMSIS-DAP v1 remains unsupported. |

| Requested target families | Present foundation | Remaining qualification |
| --- | --- | --- |
| STM32, all families | Exact OpenOCD config plus ARM toolchain or native Cube/PlatformIO build | No blanket all-family validation; each part/config/protection state requires confirmation and hardware tests. |
| nRF51/52/53 | OpenOCD where supported; native Zephyr/PlatformIO/Rust project plans | SDK/core installation, multicore behavior and each part's probe support remain unverified. |
| RP2040/RP2350 | Pico SDK/PlatformIO plans; OpenOCD and picotool command routes | Board/architecture selection is explicit. No user-space BOOTSEL FAT mass-storage writer. |
| SAMD/SAME | ARM builds, OpenOCD or compatible bossac bootloader plan | Exact bootloader offset and installed tool are required; no hardware verification. |
| NXP LPC/Kinetis/i.MX RT | ARM/native ecosystem build and applicable OpenOCD config | No packaged vendor SDK or claim that every NXP target works. Teensy's bootloader route has separate limits below. |
| GD32, CH32V | Applicable upstream/vendor OpenOCD and GCC architecture | Vendor forks/SWIO and CH32 programmer installation remain incomplete; a generic RISC-V toolchain is insufficient. |
| ESP32-C3/C6/H2 | ESP-IDF/native ecosystem build; exact esptool chip selection | Espressif compiler/runtime installation and USB-JTAG debug integration require further work. |
| Other probe-rs/OpenOCD targets | Typed OpenOCD configuration can address targets supported by the installed backend | Upstream support lists are not TabStudio hardware verification or Android nusb compatibility. |

### Bootloaders and programming routes

| Board/protocol | Implemented operation | Verification and limits |
| --- | --- | --- |
| Arduino AVR Optiboot/STK500v1 | avrdude application write plus explicit verify | Safe image/range/argument plans and AVR profile tests; avrdude is in the Store. Hardware-unverified. |
| Arduino Mega STK500v2 | avrdude `wiring` application upload/verify | Explicit ATmega2560 profile; hardware-unverified. |
| Leonardo/Micro AVR109 | 1200-baud touch, re-permission, avrdude write/verify | Known template profiles and bridge contracts; physical bootloader transitions unverified. |
| SAMD SAM-BA | 1200-baud flow plus bossac `-w -v -R`, without boot-configuration flags | bossac must be installed separately. Exact bootloader/layout required; hardware-unverified. |
| ESP32/S2/S3/C3/C6/H2 ROM loader | Exact esptool chip, addressed BIN write then verify | esptool must be installed separately. One-image safe plan; core-provided multi-image flash arguments are not automatically imported. Reset sequencing is hardware-unverified. |
| STM32 ROM UART | stm32flash explicit BIN range with verify | stm32flash is installed through the embedded USB Store entry from signed Alpine packages. BOOT pins/reset and exact target map are external prerequisites. Hardware-unverified. |
| STM32 USB DFU | dfu-util write, independent upload/readback and byte comparison | dfu-util is in the Store. Exact VID/PID, alternate and application range required. Read-protected/non-readback DFU cannot pass this route. Hardware-unverified. |
| RP2040/RP2350 BOOTSEL | picotool addressed ELF/UF2/BIN load with verify | picotool must be installed separately. UF2 image parsing is tested; FAT mass-storage transfer over USB Host API is not implemented. Hardware-unverified. |
| nRF51/52 Nordic SDK Secure DFU | Application-only ZIP through legacy pc-nrfutil 6.x `dfu usb-serial` or `dfu serial` | ZIP/manifest/protobuf size/type/SHA-256 checks are unit-tested. Target bootloader validates compatibility/signature and object CRCs. Requires separately installed compatible pc-nrfutil and an existing Secure DFU bootloader; not modern nRFutil, BLE DFU or SoftDevice/bootloader updates. Hardware-unverified. |
| Teensy HalfKay | Exact-board `teensy_loader_cli --mcu=TEENSY40/TEENSY41 -v` HEX transfer | Transfer completion only: `-v` means verbose, not verify. UI says **Firmware transferred; readback unavailable** and production batches refuse this route. Loader must be installed separately; press the board’s Program button before granting its HalfKay device. No first-device soft-reboot command is used. Hardware-unverified. |
| WCH ROM ISP | wchisp 0.3-compatible `info --chip` preflight, then default `flash` with ISP verification | Raw BIN must explicitly start at code-flash address zero. USB or explicit UART transport is supported; UART baud is 115200/1000000/2000000. Requires separately installed compatible wchisp. No config-reset/unprotect flags. CH32V003 has UART ISP, no USB ISP, and requires application-controlled bootloader entry. WCH-Link/wlink SWIO is a separate route. Hardware-unverified. |
| MSP430 mspdebug | Addressed HEX/ELF `prog` then target-memory `verify`, with `-n` disabling startup scripts | Explicit rf2500, olimex variants, uif, goodfet or mehfet driver; serial/USB as supported by that driver. BSL drivers with automatic mass erase/unlock and vendor tilib are refused. Requires separately installed mspdebug; legacy libusb compatibility is a signed Alpine dependency. Hardware-unverified. |
| AVR UPDI | pymcuprog UART `write --verify` using an exact supported AVR and application-only HEX; installed avrdude `serialupdi` can also be selected | Android prepares the existing SHA-256-pinned MIT Python support wheels without downloading/licensing XC. Requires a compatible UPDI adapter/circuit and device module. No high-voltage activation, chip unlock, user-row, fuse or explicit chip-erase operation. Hardware-unverified. |
| PIC AN1388 HID/UART, MCC UART, PKOB nano, PICkit scripting, gpsim, Bridge | Existing PIC implementations are reused | See [PIC support](pic.md) for exact device/protocol/compiler evidence and vendor limits. Those historical tests do not verify newly added physical boards. |

### Additional route configuration

The flash-profile `protocol` accepts `Teensy`, `WchIsp`, `MspDebug`, `NrfDfu` and `PymcuprogUpdi` in addition to
previous routes. These use the same parsed-image boundaries and content-stability checks. Normal transfer-only
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
- **Nordic Secure DFU:** install the legacy **pc-nrfutil 6.x** CLI under Nordic's license. Supply an application-only
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
[Microchip's pymcuprog CLI](https://github.com/microchip-pic-avr-tools/pymcuprog). Compatible tool installation and
physical-board validation are still required; the command/fixture tests do not stand in for either.

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
| nRF52840 DK | PlatformIO `nrf52840_dk` | Template contracts tested; preserve any separate SoftDevice; hardware-unverified. |
| Seeed XIAO | PlatformIO `seeed_xiao`, SAMD21 only | Template contracts tested; ESP/nRF/RP XIAO variants need distinct definitions; hardware-unverified. |
| Teensy 4.0/4.1 | PlatformIO `teensy40`/`teensy41` | Starters plus explicit HalfKay HEX transfer. No readback/production verification. Hardware-unverified. |
| CH32V003 | Explicit device metadata profile | No invented PlatformIO package or one-tap starter. Toolchain/SWIO programming integration incomplete; hardware-unverified. |

## Flash safety, backups and production work

Upload parses the actual HEX/BIN/ELF/UF2 image or the allowed Nordic application-only ZIP, validates its programmed bytes against explicit application ranges,
and binds the plan to the firmware's SHA-256. It does not trust a caller-supplied image-size list. BIN requires an
explicit start address. Normal upload never accepts arbitrary extra programmer flags for fuses, option bytes,
read-out protection or eFuses. The firmware must match the bootloader's application linker layout.

There is no general fuse/option-byte programming UI. The configuration-policy API requires an exact confirmation
describing the selected device, register, old/new values, reversibility and consequences for any future supported
configuration operation. It refuses eFuse burning and permanent read-out protection/RDP level 2 outright.
The SVD field editor also refuses configuration and side-effect registers; loading an SVD never authorizes a write.

**Backup firmware** reads one explicitly mapped application interval twice using OpenOCD, stm32flash or
readback-capable STM32 DFU. It only saves a new backup when both reads agree and match the expected byte count.
Read protection is never removed. **Compare firmware dumps** reports whole-file SHA-256 hashes, the exact number
of differing bytes and bounded contiguous difference ranges, including unequal file lengths.

**Production batch** selects attached devices with the same VID/PID, asks the operator to confirm the exact MCU
and layout, opens separate Android permissions/connections, and programs at most two concurrently. Each result
must verify independently; failures remain per-board failures. `.tabstudio/production/*.jsonl` records device IDs,
firmware digests, outcomes and errors. Identical VID/PID is an operator selection, not proof of identical silicon.
Touch/re-enumerating bootloaders, stock nusb and transfer-only loaders such as Teensy HalfKay are refused in batches before programming. No real USB hub has been verified yet.

**Upload over Wi-Fi** implements ArduinoOTA-compatible transfer and old MD5/new PBKDF2 challenge formats, requires
the target's final acceptance, and keeps the entered password in memory for that upload. The board must already
run an OTA-capable application with a suitable partition layout. Its acknowledgment is not physical flash
readback or a security guarantee from TLS; this is the upstream OTA protocol, not a bootloader installer.

## On-chip debugging, registers and RTOS tasks

**Debug on chip** builds symbols and starts the selected installed OpenOCD interface/target configuration through
the permissioned libusb connection. The GDB endpoint binds to loopback; Tcl/telnet endpoints are disabled. A
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
strict length/address checks; instruction output can be requested through the adapter's console where supported.
The QEMU/simavr tests exercise the real adapter/client path, not OpenOCD's physical probe transport. Availability
of a particular peripheral, multicore target or architecture still depends on OpenOCD/GDB support and wiring.

**Load CMSIS-SVD** reads a local vendor file with DTD/entity resolution disabled and bounded expansion. Register
inspection names bitfields and enumerations, uses the selected SVD's address/width and requires an explicitly
little-endian halted target. Write-only registers and documented read side effects are not automatically read.

**Edit register field** additionally requires `.tabstudio/peripheral-access.json`: a reviewed device/register/address
allowlist. It refuses configuration/side-effect registers, previews the exact old/new value, requires that exact
operation to be typed, checks the register has not changed, writes, and reads back. This is an explicit register
picker workflow, not a continuously polled instrumentation panel.

Editor hover on an identifiable `PERIPHERAL->REGISTER` access adds SVD descriptions. **Load register header** reads
the actual chip header; **Ctrl+.** on an eligible literal mask offers an existing macro only when its resolved
definition equals the SVD field mask. It does not invent vendor macro names. Full register-aware completion across
arbitrary HAL abstractions/macros is not implemented.

**RTOS threads** reads halted FreeRTOS lists or Zephyr's monitored thread chain through the firmware's actual
debug types and symbols. Names, states, priorities and available stack extents are bounded/cycle checked. All
cores must be halted. Zephyr needs thread monitoring; missing types/configurations are reported. The reader can
scan a verified stack-fill pattern when explicitly supplied by a caller; the current UI does not guess fill
settings or claim a stack high-water mark when these are unknown.

Live RTT acquisition and binary defmt decoding are not integrated into this workflow yet. OpenOCD/debug output
and ordinary serial logs use existing Output/Serial Monitor. The test importer accepts already decoded logs;
that is not a claim of real-time RTT/defmt transport.

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
wrong core frame. Captured PC/LR are not a reconstructed complete unwind. ESP PC:SP pairs preserve only PCs. The classic AVR MCUSR reset decoder is an engine
API, not a dedicated UI capture workflow. Exact source locations require the matching firmware ELF and configured
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
resume the CPU. This is not an automatic full crash-unwinder or physical hardware verification.

Frame layout reference: Arm's [Exception Model User Guide, Figure 4-3](https://community.arm.com/cfs-file/__key/communityserver-discussions-components-files/471/armv8_2D00_m_5F00_exception_5F00_model_5F00_user_5F00_guide_5F00_107706_5F00_0100_5F00_01_5F00_en.pdf).

**Stack analysis** reads GCC `-fstack-usage` `.su` rows and supplied call edges, computing the deepest bounded call
chain. The default does not assert graph completeness. Missing library frames, indirect calls (`Callee: null`),
recursive cycles, ambiguous static names and unbounded dynamic allocations remain unknown. Task checks add the
explicit byte allowance before comparing to the allocation. Interrupt nesting, assembly, RTOS context frames and
other ABI overhead need reviewed allowances; a source parser cannot prove these from a function name. Automatic
whole-program call-graph extraction is not provided yet.

**Check pin conflicts** reads explicit CubeMX `.ioc`, selected devicetree/overlay forms and simple Arduino pin
uses, placing conflicts in the Error List. `.tabstudio/pin-map.json` can provide the actual chip's valid pins;
without it, nonexistent-pin validation is not claimed. Dynamic expressions, aliases, includes, full preprocessor
evaluation and every Zephyr pinctrl dialect are outside this bounded static check.

## Tests on the host and board results

**Run embedded tests** runs an already built host Unity/TAP/GoogleTest executable or Cargo's actual host target.
`.tabstudio/embedded-tests.json` names the format, executable/arguments and host identity. Arguments remain
separate strings; Rust host tests explicitly override an inherited bare-metal target. GoogleTest gets a fresh XML
report per run. Nonzero exits, truncated output or incomplete harness summaries cannot become a passing run.

**Import test results** reads an actual Unity, TAP, defmt-test, Rust text log or GoogleTest XML report and retains
the supplied board identity in Test Explorer. This is useful for logs produced by a supported external/on-target
harness, but TabStudio does not yet flash/run a complete on-target Unity/defmt-test campaign or acquire RTT data
automatically. A host result is never labeled as a physical-board result.

## Verification and reproducibility

The dedicated [embedded workflow](../../.github/workflows/embedded.yml) is configured for push and pull-request
runs. Check the final PR SHA for completed results; adding a workflow is not evidence that it passed.

| Check | What it actually establishes |
| --- | --- |
| Embedded library tests | Parsers, safe image bounds and command plans, device associations, backups/diffs, OTA protocol contracts, SVD/pins/RTOS readers and test-result parsing. Analysis tests also read a real host ELF. |
| Native USB tests | An unchanged compiled POSIX C client uses the real PTY/shim; synthetic adapter requests and mock libusb ABI ownership are replayed. No physical captures. |
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
TABSTUDIO_EMBEDDED_EMULATORS=1 TABSTUDIO_REPO_ROOT="$PWD" \
  dotnet test tests/TabStudio.LanguageServers.Tests --filter FullyQualifiedName~EmbeddedQemuTests
```

The emulator command requires the cross compilers, QEMU, simavr and a suitable GDB on the host. The CI workflow
lists those packages. Synthetic fixtures are described in [USB bridge verification](../../build/embedded/tests/README.md).
Real hardware captures still need the exact device/probe firmware, target part, tool version, firmware digest,
redistribution permission and expected result; none have been fabricated for this change.

## Engineering decisions and remaining work

The useful additions are repeatable verified backups, exact dump comparisons, per-board production logs,
source-preserving vendor import, build-size regressions, explicit unknown stack bounds and safe register inspection.
They answer ordinary development questions without needing a second computer or measuring-instrument features.

The implementation reuses the Toolchain Store, PIC USB lifetime/drivers, existing terminal, clangd/rust-analyzer,
Error List, Test Explorer and DAP client. It does not introduce another process runtime or bypass Android USB
permissions. OpenOCD plus GDB DAP supplies the current implemented debug route. The separate PRoot usbfs bridge
has CI-verified static/raw FD routing and stock nusb enumeration, which establishes the transport foundation;
it does not establish a physical probe-rs programming or debug session.

Remaining work includes physical nusb/usbfs I/O qualification and native hidraw/HIDAPI v1 transport,
user-space BOOTSEL FAT storage, complete board/core/SDK ARM64/glibc
installation and Box64 package fallback, automatic interrogation on plug-in and device-association UI, the unimplemented
programmers listed above, automatic crash detection/full unwind beyond explicit supported capture, complete call-graph extraction, RTT/defmt acquisition,
fully automated on-target test campaigns, F5 orchestration across every unsupported board/transport and physical-board
verification. The existing [Windows Bridge](../ARCHITECTURE.md#windows-bridge) remains available for workflows
that need vendor desktop software. See [third-party notices](../../THIRD-PARTY-NOTICES.md) for downloaded tools.
