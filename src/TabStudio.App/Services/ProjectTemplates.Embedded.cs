using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Flashing;

namespace TabStudio.App.Services;

/// <summary>Embedded starter projects that use upstream PlatformIO board definitions.</summary>
public sealed partial class ProjectTemplates
{
    private const string EmbeddedPrefix = "embedded-";
    private static readonly JsonSerializerOptions AvrFlashJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static IEnumerable<ProjectTemplate> EmbeddedTemplates => EmbeddedBoardProfiles.All
        .Where(board => board.PlatformIoBoard is not null && PlatformFor(board.Id) is not null || PicoBoard(board.Id) is not null || board.Id is "ch32v003" or "nrf52840dk")
        .Select(board => new ProjectTemplate(EmbeddedPrefix + board.Id, board.Name + " (" + TemplateSystem(board.Id) + ")",
            "Serial greeting application for " + board.Target + ". " + board.Notes + " Build package installation is required; physical board verification is separate.", [board.Id is "ch32v003" or "nrf52840dk" ? "C" : "C++", "Embedded", TemplateSystem(board.Id), board.Target]));

    private static string TemplateSystem(string boardId) => boardId switch
    {
        "ch32v003" => "ch32fun",
        "nrf52840dk" => "Zephyr",
        _ => PicoBoard(boardId) is null ? "PlatformIO" : "Pico SDK",
    };

    private static string? CreateEmbeddedProject(string templateId, string root, string name)
    {
        if (!templateId.StartsWith(EmbeddedPrefix, StringComparison.Ordinal))
        {
            return null;
        }
        var board = EmbeddedBoardProfiles.All.FirstOrDefault(x => EmbeddedPrefix + x.Id == templateId);
        if (board?.Id == "ch32v003") return CreateCh32FunProject(root);
        if (board?.Id == "nrf52840dk") return CreateNordicZephyrProject(root);
        if (board is not null && PicoBoard(board.Id) is { } sdkBoard)
        {
            return CreatePicoSdkProject(root, name, sdkBoard);
        }
        if (board is null || PlatformFor(board.Id) is not { } platform)
        {
            return null;
        }
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "platformio.ini"), $"; {name}: {board.Name}\n[env:{board.Id}]\nplatform = {platform}\nboard = {board.PlatformIoBoard}\nframework = arduino\nmonitor_speed = 115200\nbuild_flags = -fstack-usage\n");
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), """
            #include <Arduino.h>

            void setup() {
                Serial.begin(115200);
                // Configure pins here for your exact board and wiring.
            }

            void loop() {
                Serial.println("Hello from TabStudio");
                delay(1000);
            }
            """ + "\n");
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".pio/\n.tabstudio/\n");
        WriteKnownAvrFlashProfile(root, board.Id);
        WriteEspMetadataFlashProfile(root, board.Id);
        WriteKnownBoardFlashProfile(root, board.Id);
        return root;
    }

    private static string CreateNordicZephyrProject(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), """
            cmake_minimum_required(VERSION 3.20.0)
            find_package(Zephyr REQUIRED HINTS $ENV{ZEPHYR_BASE})
            project(tabstudio_greeting)
            target_sources(app PRIVATE src/main.c)
            target_compile_options(app PRIVATE -fstack-usage)
            """ + "\n");
        File.WriteAllText(Path.Combine(root, "prj.conf"), "CONFIG_PRINTK=y\nCONFIG_CONSOLE=y\nCONFIG_UART_CONSOLE=y\n");
        File.WriteAllText(Path.Combine(root, "src", "main.c"), """
            #include <zephyr/kernel.h>
            #include <zephyr/sys/printk.h>

            int main(void) {
                while (true) {
                    printk("Hello from TabStudio\n");
                    k_sleep(K_SECONDS(1));
                }
                return 0;
            }
            """ + "\n");
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".tabstudio/\n");
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        File.WriteAllText(Path.Combine(root, ".tabstudio", "embedded.json"), "{\n  \"board\": \"nrf52840dk/nrf52840\"\n}\n");
        File.WriteAllText(Path.Combine(root, "README.md"), """
            # nRF52840 DK serial greeting

            Uses Zephyr's exact nrf52840dk/nrf52840 board definition, native SDK startup, linker
            layout and UART console. Install the Zephyr and probe-rs Toolchain Store packages.
            F5 builds, uploads through the selected onboard J-Link or compatible external probe,
            and opens the same attachment's compatible USB-serial interface when available.
            A separate USB-UART adapter must be selected explicitly in the Serial Monitor.

            This is a complete standalone Zephyr application without a SoftDevice. Upload replaces
            overlapping application flash; preserve a required SoftDevice by building a matching
            partitioned project instead. No UICR, protection or option update is generated.
            Physical probe, firmware and UART behavior still require hardware verification.
            """ + "\n");
        WriteFlashProfile(root, new()
        {
            Protocol = EmbeddedFlashProtocol.ProbeRs, Target = "nRF52840_xxAA",
            FirmwarePath = ".tabstudio/embedded/build/zephyr/zephyr.elf", WorkingDirectory = root,
            ApplicationFlash = [new(0, 1024 * 1024)],
        });
        return root;
    }

    private static void WriteKnownBoardFlashProfile(string root, string boardId)
    {
        var firmware = ".pio/build/" + boardId + "/firmware";
        EmbeddedFlashRequest? profile = boardId switch
        {
            "nucleo-f401re" => Probe("STM32F401RE", 512 * 1024),
            "bluepill" => Probe("STM32F103C8", 64 * 1024),
            "blackpill" => Probe("STM32F411CE", 512 * 1024),
            "pico" => new()
            {
                Protocol = EmbeddedFlashProtocol.Picotool, Target = "RP2040", FirmwarePath = firmware + ".uf2",
                WorkingDirectory = root, ApplicationFlash = [new(0x10000000, 2 * 1024 * 1024)],
            },
            "xiao-samd21" => new()
            {
                Protocol = EmbeddedFlashProtocol.Bossac, Target = "SAMD21G18A", FirmwarePath = firmware + ".bin",
                WorkingDirectory = root, BinaryAddress = 0x2000, FlashBaseAddress = 0,
                ApplicationFlash = [new(0x2000, 256 * 1024 - 0x2000)], Touch1200Baud = true, SerialPort = "/dev/ttyUSB0",
            },
            "teensy40" or "teensy41" => new()
            {
                Protocol = EmbeddedFlashProtocol.Teensy, Target = boardId.ToUpperInvariant(), FirmwarePath = firmware + ".hex",
                WorkingDirectory = root, ApplicationFlash = [new(0x60000000, boardId == "teensy40" ? 2031616UL : 8126464UL)],
            },
            _ => null,
        };
        if (profile is not null) WriteFlashProfile(root, profile);

        EmbeddedFlashRequest Probe(string chip, ulong size) => new()
        {
            Protocol = EmbeddedFlashProtocol.ProbeRs, Target = chip, FirmwarePath = firmware + ".elf",
            WorkingDirectory = root, ApplicationFlash = [new(0x08000000, size)],
        };
    }

    private static void WriteFlashProfile(string root, EmbeddedFlashRequest profile)
    {
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        File.WriteAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json"), JsonSerializer.Serialize(profile, AvrFlashJson) + "\n");
    }

    private static string CreateCh32FunProject(string root)
    {
        foreach (var (name, content) in TabStudio.Toolchains.Embedded.Installation.EmbeddedCh32FunInstallation.ProjectFiles)
        {
            File.WriteAllText(Path.Combine(root, name), content);
        }
        File.WriteAllText(Path.Combine(root, "README.md"), """
            # CH32V003F4P6 UART greeting

            Build uses the Toolchain Store's pinned ch32fun startup, linker script and headers,
            with ordinary native RV32EC/ILP32E GCC. The firmware uses 16 KiB flash and 2 KiB RAM.
            UART output is PD5 TX, 115200 baud. Connect a 3.3 V USB-UART adapter RX and common GND.

            The generated UART ISP profile requires a device already running its ROM bootloader.
            CH32V003 has no USB ISP or BOOT pin: entry is requested by existing application firmware.
            First programming/recovery normally uses a compatible WCH-Link probe on PD1 SWIO.
            Selecting this template does not switch option bytes, unlock protection or install a bootloader.
            Use F5 after selecting the actual connected adapter and bootloader state.
            """ + "\n");
        var profile = new EmbeddedFlashRequest
        {
            Protocol = EmbeddedFlashProtocol.WchIsp, Target = "CH32V003F4P6", FirmwarePath = "firmware.bin",
            WorkingDirectory = root, BinaryAddress = 0, ApplicationFlash = [new(0, 16 * 1024)],
            SerialPort = "/dev/ttyUSB0", UseSerialTransport = true,
        };
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        File.WriteAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json"), JsonSerializer.Serialize(profile, AvrFlashJson) + "\n");
        return root;
    }

    private static void WriteEspMetadataFlashProfile(string root, string boardId)
    {
        if (boardId is not ("esp32" or "esp32s3" or "esp32c3")) return;
        File.AppendAllText(Path.Combine(root, "platformio.ini"), "extra_scripts = post:tabstudio_esp_flash.py\n");
        File.WriteAllText(Path.Combine(root, "tabstudio_esp_flash.py"), TabStudio.Toolchains.Embedded.Installation.EmbeddedFlashToolInstallation.PlatformIoEspExporterSource);
        var metadata = ".pio/build/" + boardId + "/tabstudio-flasher-args.json";
        var profile = new EmbeddedFlashRequest
        {
            Protocol = EmbeddedFlashProtocol.Esptool, Target = "FROM_METADATA", FirmwarePath = metadata,
            EspFlasherArguments = metadata, WorkingDirectory = root, SerialPort = "/dev/ttyUSB0",
        };
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        File.WriteAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json"), JsonSerializer.Serialize(profile, AvrFlashJson) + "\n");
    }

    private static void WriteKnownAvrFlashProfile(string root, string boardId)
    {
        // Application limits/programmers/baud match platformio/platform-atmelavr board definitions.
        // Nano's new Optiboot image still uses the board's published 30,720-byte limit (2 KiB boot section).
        (string Target, string Programmer, ulong MaximumSize, int Baud, bool Touch)? board = boardId switch
        {
            "uno" => ("m328p", "arduino", 32256, 115200, false),
            "nano" => ("m328p", "arduino", 30720, 115200, false),
            "mega" => ("m2560", "wiring", 253952, 115200, false),
            "leonardo" or "micro" => ("m32u4", "avr109", 28672, 57600, true),
            _ => null,
        };
        if (board is not { } avr)
        {
            return;
        }
        var profile = new EmbeddedFlashRequest
        {
            Protocol = EmbeddedFlashProtocol.Avrdude,
            Target = avr.Target,
            Programmer = avr.Programmer,
            FirmwarePath = ".pio/build/" + boardId + "/firmware.hex",
            WorkingDirectory = root,
            SerialPort = "/dev/ttyUSB0",
            BaudRate = avr.Baud,
            Touch1200Baud = avr.Touch,
            ApplicationFlash = [new(0, avr.MaximumSize)],
        };
        Directory.CreateDirectory(Path.Combine(root, ".tabstudio"));
        File.WriteAllText(Path.Combine(root, ".tabstudio", "embedded-flash.json"), JsonSerializer.Serialize(profile, AvrFlashJson) + "\n");
    }

    private static string? PicoBoard(string id) => id switch
    {
        "picow" => "pico_w",
        "pico2" => "pico2",
        "pico2w" => "pico2_w",
        _ => null,
    };

    private static string CreatePicoSdkProject(string root, string name, string board)
    {
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), $$"""
            cmake_minimum_required(VERSION 3.13)
            if(NOT DEFINED PICO_BOARD)
                set(PICO_BOARD {{board}})
            endif()
            if(NOT DEFINED ENV{PICO_SDK_PATH})
                message(FATAL_ERROR "Set PICO_SDK_PATH to your installed, version-pinned Pico SDK")
            endif()
            include("$ENV{PICO_SDK_PATH}/external/pico_sdk_import.cmake")
            project({{name}} C CXX ASM)
            pico_sdk_init()
            add_executable({{name}} main.c)
            target_link_libraries({{name}} pico_stdlib)
            target_compile_options({{name}} PRIVATE -fstack-usage)
            pico_enable_stdio_usb({{name}} 1)
            pico_enable_stdio_uart({{name}} 0)
            pico_add_extra_outputs({{name}})
            """ + "\n");
        File.WriteAllText(Path.Combine(root, "main.c"), """
            #include <stdio.h>
            #include "pico/stdlib.h"

            int main(void) {
                stdio_init_all();
                while (true) {
                    puts("Hello from TabStudio");
                    sleep_ms(1000);
                }
            }
            """ + "\n");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "build/\n.tabstudio/\n");
        WriteFlashProfile(root, new()
        {
            Protocol = EmbeddedFlashProtocol.Picotool, Target = board == "pico_w" ? "RP2040" : "RP2350",
            FirmwarePath = ".tabstudio/embedded/build/" + name + ".uf2", WorkingDirectory = root,
            ApplicationFlash = [new(0x10000000, board == "pico_w" ? 2UL * 1024 * 1024 : 4UL * 1024 * 1024)],
        });
        return root;
    }

    private static string? PlatformFor(string board) => board switch
    {
        "uno" or "nano" or "mega" or "leonardo" or "micro" => "atmelavr@5.3.0",
        "esp32" or "esp32s3" or "esp32c3" => "espressif32@8.0.0",
        "pico" => "raspberrypi@1.20.0",
        "nucleo-f401re" or "bluepill" or "blackpill" => "ststm32@20.0.0",
        "xiao-samd21" => "atmelsam@9.0.0",
        "teensy40" or "teensy41" => "teensy@6.0.0",
        // Other profiles remain selectable SDK targets; do not invent PlatformIO packages/board IDs.
        _ => null,
    };
}
