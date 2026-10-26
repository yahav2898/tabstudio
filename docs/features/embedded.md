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

The serial bridge runs an unchanged dynamically linked POSIX client on a real raw PTY. A scoped native shim
forwards termios/ioctl changes to the existing CDC-ACM, CH340/341, CP210x and FTDI drivers: baud, data/parity/stop
format, DTR/RTS, break, hangup, drain and flush. Linux termios2 arbitrary baud rates are covered. The worker uses
an explicit private alias, normally `.tabstudio/dev/ttyUSB0`, rather than creating kernel serial devices.

| Transport | Implemented verification | Limit |
| --- | --- | --- |
| libusb | Synthetic ABI replay: enumeration, two contexts, references, VID/PID filtering, FD ownership and permission denial | No physical USB/probe test. Static libusb, direct usbfs, nusb and native hidraw bypass this shim. |
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
is never labeled a probed chip identity. Automatic target IDCODE/chip-ID interrogation and a complete association
editing UI are not implemented yet. Physical detach/re-enumeration timing remains hardware-unverified.

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
| probe-rs probes | Native-host typed download plan; Android transport pending | Current stock probe-rs uses nusb, not libusb; its HID paths may also use hidraw. Android rejects this route explicitly. No Android probe-rs DAP support claim. |

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
| STM32 ROM UART | stm32flash explicit BIN range with verify | Tool must be installed separately. BOOT pins/reset and exact target map are external prerequisites. Hardware-unverified. |
| STM32 USB DFU | dfu-util write, independent upload/readback and byte comparison | dfu-util is in the Store. Exact VID/PID, alternate and application range required. Read-protected/non-readback DFU cannot pass this route. Hardware-unverified. |
| RP2040/RP2350 BOOTSEL | picotool addressed ELF/UF2/BIN load with verify | picotool must be installed separately. UF2 image parsing is tested; FAT mass-storage transfer over USB Host API is not implemented. Hardware-unverified. |
| nRF DFU | No complete local nrfutil/DFU route | Not implemented or hardware-verified; use a supported probe/vendor workflow. |
| Teensy HalfKay | Board templates/USB identity only for programming | Safe upload explicitly refuses teensy_loader_cli because this route has no readback verification. No unverified upload is labeled verified. |
| CH32 wchisp/wlink | No complete packaged bootloader/programmer route | Not implemented or hardware-verified; CH32V003 SWIO is not generic RISC-V JTAG. |
| MSP430 mspdebug | No complete packaged route | Not implemented or hardware-verified. |
| AVR UPDI | Typed avrdude programmer identifier can select a supporting installed `serialupdi` backend | No dedicated UPDI installation/board workflow or hardware proof. Existing pymcuprog remains documented with PIC support; this change does not claim all AVR programmer modes. |
| PIC AN1388 HID/UART, MCC UART, PKOB nano, PICkit scripting, gpsim, Bridge | Existing PIC implementations are reused | See [PIC support](pic.md) for exact device/protocol/compiler evidence and vendor limits. Those historical tests do not verify newly added physical boards. |

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
| Teensy 4.0/4.1 | PlatformIO `teensy40`/`teensy41` | Starter/profile metadata only; verified HalfKay upload is unavailable. Hardware-unverified. |
| CH32V003 | Explicit device metadata profile | No invented PlatformIO package or one-tap starter. Toolchain/SWIO programming integration incomplete; hardware-unverified. |

## Flash safety, backups and production work

Upload parses the actual HEX/BIN/ELF/UF2 image, validates its addressed bytes against explicit application ranges,
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
Touch/re-enumerating bootloaders and stock nusb are refused in batches. No real USB hub has been verified yet.

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
  "TargetConfig": "target/stm32f4x.cfg"
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
possible. The Cortex reader does not automatically collect live HardFault registers. Its input is a basic
exception frame; an extended FPU frame must be skipped correctly before supplying it. Captured PC/LR are not a
reconstructed complete unwind. ESP PC:SP pairs preserve only PCs. The classic AVR MCUSR reset decoder is an engine
API, not a dedicated UI capture workflow. Exact source locations require the matching firmware ELF and configured
addr2line; unknown locations stay empty.

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
permissions. OpenOCD plus GDB DAP is the implemented debug route because the permissioned libusb backend can be
exercised independently; calling stock nusb probe-rs supported would be incorrect.

Remaining work includes nusb/hidraw transport, user-space BOOTSEL FAT storage, complete board/core/SDK ARM64/glibc
installation and Box64 package fallback, automatic chip identification and device-association UI, the unimplemented
programmers listed above, automatic live crash capture, complete call-graph extraction, RTT/defmt acquisition,
fully automated on-target test campaigns, F5 orchestration across every unsupported board/transport and physical-board
verification. The existing [Windows Bridge](../ARCHITECTURE.md#windows-bridge) remains available for workflows
that need vendor desktop software. See [third-party notices](../../THIRD-PARTY-NOTICES.md) for downloaded tools.
