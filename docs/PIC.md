# Microchip PIC on the tablet

TabStudio builds PIC firmware on 64-bit Android without a PC. Install **Microchip XC8** or **Microchip XC32** in **Tools > Toolchain Store**. Read and accept Microchip's software licence before any compiler download. The Store downloads the pinned official Linux installer directly from Microchip, runs it unattended in Free mode without activating a licence manager, and checks every distribution's SHA-256. If the bounded installer attempt fails or times out, it extracts Microchip's separately verified official distribution archive. This is reported in installation output.

**The XC compilers are installed by the user under Microchip's licence and are never redistributed by TabStudio.** Installers, compiler archives, device packs, PICkit firmware and `PK2DeviceFile.dat` are absent from the APK and CI artifacts.

## Build and edit

The APK includes a source-built, pinned Box64 0.4.4 with a small child-stack fix. It runs the unchanged x86-64 XC binaries in TabStudio's Alpine 3.24.3 / Termux PRoot environment. Verified Debian amd64 glibc, libstdc++, libgcc and zlib supply the x86 runtime; ARM64 versions supply Box64's native runtime. These do not replace Alpine's musl loader. XC8 uses the dynamic translator; XC32 uses the interpreter, the mode that passed the ARM spike. They are separate Linux processes, not libraries loaded into Android's Bionic process.

Create one of these projects, then build with **Ctrl+F5** or **Build Solution**:

| Template | Device | Example LED | Local simulation |
| --- | --- | --- | --- |
| PIC18 Blink (XC8) | PIC18F14K22 | RC0, with a resistor; internal 16 MHz oscillator | F5 through gpsim |
| PIC32 Blink (XC32) | PIC32MX250F128B | RB0, with a resistor; internal FRC oscillator | On-chip debugging through the Windows Bridge |

The phase-zero benchmark uses a separate PIC18F45K22/RD0 blink; it remains in `build/pic/pic18-spike.c`. That device does **not** have a gpsim model. Changing a board requires selecting its actual part, clock and pins; simulation never substitutes another MCU.

Existing MPLAB X projects are recognized by `nbproject/configurations.xml`. Device, compiler/version, packs, options, project files and per-file exclusions/overrides are parsed without executing XML or resolving external entities. **Debug > MPLAB Configuration** selects an existing named configuration; the selection is saved in `.tabstudio/pic-build.json`.

The tablet delegates the build to MPLAB's generated `nbproject/Makefile-<configuration>.mk`, with its `.build-conf` production target and the installed compiler/pack paths. This retains MPLAB's option spellings, libraries, ordering, custom steps and per-file rules. Keep these generated makefiles with imported projects. If they are absent, TabStudio refuses to guess a different build; generate them with MPLAB's `prjMakefilesGenerator`, or use a paired Windows Bridge. The templates include portable makefiles and need no PC. Desktop-specific custom build commands still require porting or the Bridge.

The selected compiler version must match the installed version. Exact project DFP versions must appear in the verified catalog; they are never silently replaced. The shipped catalog pins 31 compatible pack versions and their actual device lists. A project without an explicit pack reference resolves the newest pinned pack containing its part. Only that pack is downloaded from `packs.download.microchip.com` and SHA-256 checked. Older unpinned versions require updating the catalog or using the Bridge.

Build diagnostics populate the Error List. Successful builds report program/data usage from the compiler's own output. Missing memory totals are shown as unreported. XC32's static-data total excludes its separately reserved stack/heap. `clangd` uses the device headers, a per-file compilation database, XC's queried `-E -dM` predefined macros and editor-only declarations for XC extensions/intrinsics. Clang is an editor parser, not an ABI-accurate replacement compiler; only XC builds firmware. User-owned `.clangd` and root `compile_commands.json` files are preserved. Build metadata is under `.tabstudio/pic/`.

## USB-OTG flashing

Open **Debug > PIC Target** and edit `.tabstudio/pic-target.json` for the board's actual bootloader or programmer. **Flash** appears for a configured local protocol; other targets say **Use the Windows Bridge**. Android asks for permission for the selected USB device. A serial-monitor session is released before flashing. The parent keeps `UsbDeviceConnection` alive until the worker exits, duplicates only that connection into child fd 3, and the native Alpine libusb worker calls `libusb_wrap_sys_device`. No root, hidraw, udev rules or unrestricted USB enumeration is used.

| Profile `protocol` | Supported route | Requirements |
| --- | --- | --- |
| `an1388-hid` | PIC32 AN1388, 64-byte USB HID | Compatible bootloader already on the board; its application linker script and byte-address limits |
| `an1388-uart` | PIC32 AN1388 over USB serial | Compatible UART bootloader and matching baud |
| `mcc-uart` | Compatible MCC/UBHA 8-bit PIC16/18 UART protocol | Nine-byte request header, `0x55` response synchronization; exact bootloader page size and address units |
| `pymcuprog` | PIC ICSP through **PKOB nano / nEDBG** | An upstream device module below and its DFP programming scripts; `tool: "nedbg"` |
| `pk2cmd` | PICkit 2 or PICkit 3 with scripting firmware | Separate Microchip PICkit licence acceptance; a user-owned, SHA-256-pinned `PK2DeviceFile.dat` containing the part |
| `pic32prog` | PIC32 with PICkit 2/3 scripting firmware | Part supported by the pinned upstream tool |
| `windows-bridge` | MPLAB IPE / `ipecmd` on a paired PC | PICkit 3/4/5, Snap or ICD supported by the installed Microchip software |

Bootloaders require **application** HEX images linked at their configured offset, not the default reset-vector blink image. Set `applicationStart` and exclusive `applicationEnd` to the bootloader's HEX **byte** addresses; configuration words, EEPROM and bootloader writes outside that interval are rejected **before erase**. For MCC, also set `pageSizeBytes` and `addressUnitBytes` (`1` for PIC18, `2` for PIC16 word addressing). Melody's `0x55` auto-baud training character is sent before each request; fixed-baud variants can set `autoBaud: false`. Firmware/bootloader variants must match this framing and page-write/readback protocol. AN1388 verifies CRCs; MCC verifies page readback. No reset/jump is requested after a failed verification. The code does not install an initial bootloader on an unprogrammed chip.

Optional programmer/simulator sources and Python distributions are downloaded with pinned hashes and built natively for aarch64-musl when requested. Build tools are installed from signed Alpine indexes. Source and licence files remain beside the installed tools. `pk2cmd` is **not free software**: its fork has a restrictive Microchip licence, shown separately before downloading source. Its maintainer removed release downloads and the device database on 2026-09-27; TabStudio does not fetch an old removed database or claim its former 1,588-device list. A PICkit 3 that lacks scripting firmware must be initialized with Windows software. The `pk2cmd` route selects external target power; supply the board appropriately.

### pymcuprog device coverage

The pinned pymcuprog **3.19.4.61** documents PICkit 4 and Snap in **AVR mode**, and PIC programming through PKOB nano. It does not document PICkit 5 support. Consequently PICkit 4/5 and Snap PIC targets use the Windows Bridge; attaching one does not make an unsupported device flashable locally.

Its PIC16/18 device modules are:

| Family | Exact upstream modules |
| --- | --- |
| PIC16 | PIC16F13145, PIC16F15244, PIC16F15276, PIC16F15376, PIC16F17146, PIC16F17556, PIC16F17576, PIC16F1768, PIC16F1769, PIC16F1779, PIC16F18076, PIC16F18146, PIC16F18446, PIC16F18456, PIC16LF18456 |
| PIC18 | PIC18F06Q20, PIC18F16Q20, PIC18F16Q40, PIC18F16Q41, PIC18F46Q24, PIC18F46Q71, PIC18F47K40, PIC18F47K42, PIC18F47Q10, PIC18F47Q43, PIC18F56Q24, PIC18F56Q71, PIC18F57Q43, PIC18F57Q84 |

Upstream also contains PIC24FJ128GA705 and PIC24FJ64GU205 modules; PIC24/XC16 is outside these XC8/XC32 toolchains. A device module is necessary but not sufficient: the installed DFP must also contain `scripts/<device>/` and the connected PKOB nano must support that target. Devices outside this list use another compatible bootloader/programmer route or the Bridge.

## Serial output and debugging

**View > Serial Monitor** is independent of the firmware run. It installs only Python/libusb, with no XC licence or compiler download required. Select the adapter, baud and optional interface number, then Connect. Standard CDC-ACM, CH340/341, CP210x and FTDI bulk serial are supported at 8-N-1; sent lines use UTF-8/LF. FTDI status bytes are removed per USB packet. DTR/RTS default low. Disconnect or USB detach releases the handle. Use firmware's UART/CDC output for `printf`; the monitor does not add a UART implementation to XC's C library.

**F5** uses a small stdio DAP adapter for gpsim 0.32.1. gpsim loads the HEX image; pyelftools maps XC8 ELF/DWARF source lines independently. Supported models have source/instruction breakpoints, bounded source/instruction stepping, step-out through the PIC18 hardware return stack, a current-frame location, CPU registers, SFRs and register evaluation (`reg(0xADDR)` is also accepted). A full reconstructed C call stack and local-variable expressions are not implemented. Hardware peripheral timing is only as accurate as gpsim's model.

The exact PIC18 model list is in [`gpsim.devices.json`](../src/TabStudio.Toolchains/Microchip/gpsim.devices.json). Unsupported PIC18 models and PIC32 on-chip debugging use **Debug > On-chip Debugging in MPLAB X**. The paired PC opens its installed MPLAB X project in the Bridge pane; select the PICkit 4/5 and use MPLAB's Debug Project controls there. This is vendor debugging on the PC, not a local open PICkit DAP implementation.

On the Windows Bridge, install MPLAB X, IPE, the selected XC version and DFPs yourself. TabStudio finds the vendor makefile generator, GNU make, IPE and bundled Java, builds the synchronized project, streams diagnostics back, and programs/verifies the resulting HEX with `ipecmd`. Select the IPE tool and optional serial via `bridgeTool`/`bridgeSerial`. Target-power defaults are left to IPE; TabStudio supplies no voltage override.

## Phase zero and validation

The real ARM64 experiment ran on GitHub's **ubuntu-24.04-arm**, in the same Alpine/PRoot user space as the application, without binfmt, QEMU or a privileged container. Unmodified Box64 mishandled static `clone(CLONE_VM|CLONE_VFORK|SIGCHLD, child_stack, ...)` as stack-less `vfork`. The pinned one-condition fix routes calls with a child stack through its existing clone implementation. A separately cross-compiled static `posix_spawn` regression must execute a child and preserve exit status 42 before compiler testing.

Reference run [40250577763](https://github.com/yahav2898/tabstudio/actions/runs/40250577763), three complete compile/link trials each:

| Compiler/device | x86-64 trials, seconds | ARM64 trials, seconds | Warm mean x86 / ARM | Ratio |
| --- | --- | --- | --- | --- |
| XC8 4.00 / PIC18F45K22 | 1.718, 1.868, 2.119 | 4.572, 4.574, 4.522 | 1.993 / 4.548 | 2.28× |
| XC32 6.00 / PIC32MX250F128B | 1.166, 1.267, 0.365 | 5.780, 5.782, 5.782 | 0.816 / 5.782 | 7.09× |

Warm means use the last two trials; sample size is small, and the x86 XC32 timings vary substantially. Runner CPUs/load differ, so this ratio is not isolated translation overhead or a tablet-performance claim. XC8 used Box64's dynamic translator; XC32 used its interpreter. HEX output was byte-identical across architectures. PIC18 used 60 program bytes and 2 data bytes; PIC32 used 1,644 program bytes and zero static-data bytes (stack reserved separately). The workflow republishes current measurements on every relevant PR change.

Native XC32 GCC and SDCC/gputils were explored while Box64 was failing. SDCC/gputils emitted a PIC18 object on aarch64-musl; that alone was not treated as linked firmware or as an XC8 replacement. Rebuilding XC32 reached its prerequisite libraries/binutils but was not established as a complete supported compiler. Once both official compilers worked, the selected product path remained the unchanged official XC distributions through Box64. Native fallback jobs run if that compiler proof fails.

CI also builds both shipped MPLAB templates through the production driver, queries real predefined macros, builds native programmer/gpsim sources for ARM64 musl, and checks gpsim DAP against supported-model XC8 firmware. The Android emulator check uses the APK's PRoot as the app UID; it attempts the PIC18 template if the Box64 probe runs. Only that disposable emulator APK enables `run-as`. Unsupported emulator translation is reported as a conditional skip, not as compiled firmware. Logs and result JSON distinguish it from the mandatory ARM64-host proof.

Protocol fixtures record output from an independent upstream AN1388 encoder and the MCC reference frame format; they are explicitly **not physical USB captures**. No physical PIC board or programmer is attached to CI. USB electrical behavior, vendor firmware transitions and on-chip debugging therefore still need testing with the user's actual hardware.

References: [Microchip compiler downloads](https://www.microchip.com/en-us/tools-resources/develop/mplab-xc-compilers), [MPLAB builds outside the IDE](https://developerhelp.microchip.com/xwiki/bin/view/software-tools/ides/x/outside/building/), [AN1388](https://ww1.microchip.com/downloads/en/Appnotes/01388B.pdf), [MCC Melody reference](https://github.com/microchip-pic-avr-solutions/pic18f57q43-cnano-bootloader-melody), [pymcuprog](https://github.com/microchip-pic-avr-tools/pymcuprog), [libusb descriptor wrapping](https://libusb.sourceforge.io/api-1.0/group__libusb__dev.html).
