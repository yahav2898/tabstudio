namespace TabStudio.Toolchains.Embedded.Devices;

/// <summary>A project-template board identifier. Selecting a profile does not assert that attached hardware has that board.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Name">Board name.</param>
/// <param name="PlatformIoBoard">PlatformIO's board ID, where available.</param>
/// <param name="ArduinoFqbn">Arduino fully qualified board name, where available.</param>
/// <param name="Target">MCU or family that the user must confirm against the board.</param>
/// <param name="Notes">Board-specific assumptions.</param>
public sealed record EmbeddedBoardProfile(string Id, string Name, string? PlatformIoBoard, string? ArduinoFqbn, string Target, string Notes);

/// <summary>Board choices for project creation. Package installation and physical-board verification remain separate.</summary>
public static class EmbeddedBoardProfiles
{
    /// <summary>Common boards covered by upstream build ecosystems; all variants require the correct core/package.</summary>
    public static IReadOnlyList<EmbeddedBoardProfile> All { get; } =
    [
        new("uno", "Arduino Uno", "uno", "arduino:avr:uno", "ATmega328P", "Optiboot, 115200 baud."),
        new("nano", "Arduino Nano", "nanoatmega328new", "arduino:avr:nano:cpu=atmega328", "ATmega328P", "Select old bootloader explicitly for clones that need it."),
        new("mega", "Arduino Mega 2560", "megaatmega2560", "arduino:avr:mega:cpu=atmega2560", "ATmega2560", "STK500v2, 115200 baud."),
        new("leonardo", "Arduino Leonardo", "leonardo", "arduino:avr:leonardo", "ATmega32U4", "1200-baud touch; reacquire the bootloader device after re-enumeration."),
        new("micro", "Arduino Micro", "micro", "arduino:avr:micro", "ATmega32U4", "1200-baud touch; bootloader VID/PID may differ from the sketch."),
        new("esp32", "ESP32 DevKit", "esp32dev", "esp32:esp32:esp32", "ESP32", "Flash layout and bootloader images come from the selected core build."),
        new("esp32s3", "ESP32-S3 DevKitC-1", "esp32-s3-devkitc-1", "esp32:esp32:esp32s3", "ESP32-S3", "Confirm module flash/PSRAM variant."),
        new("esp32c3", "ESP32-C3 DevKitM-1", "esp32-c3-devkitm-1", "esp32:esp32:esp32c3", "ESP32-C3", "Native USB Serial/JTAG or external UART."),
        new("pico", "Raspberry Pi Pico", "pico", null, "RP2040", "Pico SDK: PICO_BOARD=pico."),
        new("picow", "Raspberry Pi Pico W", "rpipicow", null, "RP2040", "Pico SDK: PICO_BOARD=pico_w."),
        new("pico2", "Raspberry Pi Pico 2", null, null, "RP2350", "Pico SDK: PICO_BOARD=pico2; select ARM or RISC-V explicitly."),
        new("pico2w", "Raspberry Pi Pico 2 W", null, null, "RP2350", "Pico SDK: PICO_BOARD=pico2_w; requires a supporting SDK."),
        new("nucleo-f401re", "STM32 Nucleo-F401RE", "nucleo_f401re", "STMicroelectronics:stm32:Nucleo_64:pnum=NUCLEO_F401RE", "STM32F401RE", "The Nucleo family is not one target; this profile is specifically F401RE."),
        new("bluepill", "STM32 Blue Pill", "bluepill_f103c8", null, "STM32F103C8", "Confirm genuine part and 64 KiB flash; clone capacities must not be guessed."),
        new("blackpill", "STM32 Black Pill F411CE", "blackpill_f411ce", null, "STM32F411CE", "F401 and F411 Black Pill boards need distinct chip selections."),
        new("nrf52840dk", "Nordic nRF52840 DK", "nrf52840_dk", null, "nRF52840", "Exact Zephyr board: nrf52840dk/nrf52840. Standalone firmware; use a matching partitioned project to preserve a required SoftDevice."),
        new("xiao-samd21", "Seeed XIAO SAMD21", "seeed_xiao", "Seeeduino:samd:seeed_XIAO_m0", "SAMD21G18A", "XIAO ESP32/nRF/RP variants are distinct boards."),
        new("teensy40", "Teensy 4.0", "teensy40", null, "IMXRT1062", "HalfKay; retain Teensy's bootloader and use the selected board's linker script."),
        new("teensy41", "Teensy 4.1", "teensy41", null, "IMXRT1062", "Select Teensy 4.1 rather than assuming 4.0 flash geometry."),
        new("ch32v003", "WCH CH32V003", null, null, "CH32V003F4P6", "Pinned ch32fun startup/linker and RV32EC GCC. UART ISP requires existing ROM-bootloader entry; WCH-Link SWIO is a separate probe route."),
    ];
}
