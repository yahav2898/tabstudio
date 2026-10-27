using System.Globalization;
using System.Text;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>Selected flashing transport, never guessed from a USB-serial adapter's vendor ID.</summary>
public enum EmbeddedFlashProtocol
{
    /// <summary>AVR application flash through a bootloader or explicit programmer.</summary>
    Avrdude,
    /// <summary>Espressif ROM UART/native USB download protocol.</summary>
    Esptool,
    /// <summary>STM32 ROM UART bootloader.</summary>
    Stm32Flash,
    /// <summary>STM32 DfuSe readback-capable DFU; raw binary only.</summary>
    Stm32Dfu,
    /// <summary>Raspberry Pi ROM USB interface through picotool.</summary>
    Picotool,
    /// <summary>SAM-BA compatible bootloader through bossac.</summary>
    Bossac,
    /// <summary>A probe and target explicitly configured in OpenOCD.</summary>
    OpenOcd,
    /// <summary>Native host probe-rs. Android nusb transport requires separate integration.</summary>
    ProbeRs,
    /// <summary>Teensy HalfKay through teensy_loader_cli; no readback verification.</summary>
    Teensy,
    /// <summary>WCH ROM ISP through the USB or UART transport in wchisp.</summary>
    WchIsp,
    /// <summary>MSP430 through a supported non-BSL mspdebug driver.</summary>
    MspDebug,
    /// <summary>Nordic SDK Secure DFU application ZIP through legacy pc-nrfutil 6.x.</summary>
    NrfDfu,
    /// <summary>AVR UPDI through pymcuprog's UART tool, with readback verification.</summary>
    PymcuprogUpdi,
}

/// <summary>Inputs to a safe upload. Addresses are bytes; the ranges must come from parsing the actual image and target map.</summary>
public sealed record EmbeddedFlashRequest
{
    /// <summary>Chosen protocol.</summary>
    public required EmbeddedFlashProtocol Protocol { get; init; }
    /// <summary>Firmware file, resolved before invoking a tool.</summary>
    public required string FirmwarePath { get; init; }
    /// <summary>Workspace for tool output and readback files.</summary>
    public required string WorkingDirectory { get; init; }
    /// <summary>The MCU/tool chip identifier, such as m328p, esp32c3 or nRF52840_xxAA.</summary>
    public required string Target { get; init; }
    /// <summary>Serial bridge device path, where the selected protocol requires one.</summary>
    public string? SerialPort { get; init; }
    /// <summary>Serial baud rate.</summary>
    public int BaudRate { get; init; } = 115200;
    /// <summary>avrdude programmer ID, such as arduino, wiring, avr109 or serialupdi.</summary>
    public string? Programmer { get; init; }
    /// <summary>OpenOCD interface configuration, selected from an installed tool's configuration files.</summary>
    public string? InterfaceConfig { get; init; }
    /// <summary>OpenOCD target configuration, selected for the actual target.</summary>
    public string? TargetConfig { get; init; }
    /// <summary>Probe serial/selector to prevent a desktop run from selecting another attached board.</summary>
    public string? ProbeSelector { get; init; }
    /// <summary>Raw-binary start address; image formats that encode addresses retain them.</summary>
    public ulong? BinaryAddress { get; init; }
    /// <summary>Physical start of main flash for tools using flash-relative offsets; zero for SAMD/SAME, 0x80000 for SAM3X.</summary>
    public ulong FlashBaseAddress { get; init; }
    /// <summary>Authoritative ESP-IDF flasher_args.json or resolved template export. When set, every metadata image is flashed separately.</summary>
    public string? EspFlasherArguments { get; init; }
    /// <summary>Parsed image segments.</summary>
    public IReadOnlyList<FlashAddressRange> FirmwareRegions { get; init; } = [];
    /// <summary>Permitted application flash for the actual chip and bootloader.</summary>
    public IReadOnlyList<FlashAddressRange> ApplicationFlash { get; init; } = [];
    /// <summary>DFU alternate setting; it must address application flash.</summary>
    public int DfuAlternate { get; init; }
    /// <summary>DFU VID/PID selector, formatted hhhh:hhhh.</summary>
    public string? UsbSelector { get; init; }
    /// <summary>Whether a 1200-baud touch/re-enumeration is required before opening the programming port.</summary>
    public bool Touch1200Baud { get; init; }
    /// <summary>Use UART for wchisp or a serial-capable mspdebug driver; other serial protocols always use the serial bridge.</summary>
    public bool UseSerialTransport { get; init; }
    /// <summary>Nordic DFU uses UART ping framing rather than the native CDC USB-serial variant.</summary>
    public bool NrfDfuUart { get; init; }
    /// <summary>Enable RTS/CTS flow control for Nordic UART DFU only when the bootloader and wiring require it.</summary>
    public bool HardwareFlowControl { get; init; }
}

/// <summary>A complete upload sequence. Readback comparison is performed only after all steps succeed.</summary>
/// <param name="Steps">Ordered unmodified upstream tool invocations.</param>
/// <param name="Touch1200Baud">The caller must touch and reacquire the bootloader port before running steps.</param>
/// <param name="Verification">How verification is established.</param>
/// <param name="ReadbackPath">A raw flash dump to compare byte-for-byte to the input, if needed.</param>
/// <param name="FirmwarePath">Input file for readback comparison.</param>
/// <param name="FirmwareSha256">Digest bound to the validated firmware ranges.</param>
/// <param name="Verified">The route performs target verification, rather than reporting transport completion only.</param>
public sealed record EmbeddedFlashPlan(IReadOnlyList<ProcessStartSpec> Steps, bool Touch1200Baud, string Verification,
    string? ReadbackPath = null, string? FirmwarePath = null, string? FirmwareSha256 = null, bool Verified = true)
{
    /// <summary>Additional immutable inputs used by a multi-image plan, including its build/security metadata.</summary>
    public IReadOnlyList<EmbeddedFirmwareInput> FirmwareInputs { get; init; } = [];
}

/// <summary>A concrete validated input whose digest must remain unchanged before, between and after programming operations.</summary>
public sealed record EmbeddedFirmwareInput(string Path, string Sha256);

/// <summary>Creates argument-safe upload commands. It never accepts arbitrary extra flags or configuration-write operations.</summary>
public static class EmbeddedFlashPlanner
{
    /// <summary>Whether an upload needs the USB-serial bridge rather than permissioned libusb.</summary>
    public static bool RequiresSerial(EmbeddedFlashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Protocol is EmbeddedFlashProtocol.Avrdude or EmbeddedFlashProtocol.Esptool or EmbeddedFlashProtocol.Stm32Flash or EmbeddedFlashProtocol.Bossac
            or EmbeddedFlashProtocol.NrfDfu or EmbeddedFlashProtocol.PymcuprogUpdi ||
            request.UseSerialTransport && request.Protocol is EmbeddedFlashProtocol.WchIsp or EmbeddedFlashProtocol.MspDebug;
    }

    /// <summary>Plans an application upload; incomplete identity is rejected before erase and verification limits are explicit.</summary>
    public static EmbeddedFlashPlan Plan(EmbeddedFlashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EspFlasherArguments is not null)
        {
            if (request.Protocol != EmbeddedFlashProtocol.Esptool) throw new ArgumentException("ESP multi-image metadata requires the esptool protocol.", nameof(request));
            return EspFlashManifestReader.Plan(request);
        }
        if (new FileInfo(request.FirmwarePath).Length is <= 0 or > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Firmware must contain 1 byte..128 MiB.");
        }
        var imageBytes = File.ReadAllBytes(request.FirmwarePath);
        var actualRanges = request.Protocol == EmbeddedFlashProtocol.NrfDfu
            ? new[] { NrfDfuPackage.Inspect(imageBytes, request.BinaryAddress ?? throw new ArgumentException("Select the actual Nordic application-partition address.", nameof(request))).ApplicationRange }
            : FirmwareImageInspector.Inspect(imageBytes, Path.GetExtension(request.FirmwarePath), request.BinaryAddress);
        FlashSafety.ValidateFirmware(actualRanges, request.ApplicationFlash);
        EmbeddedConfigurationGuard.Validate(request, imageBytes);
        request = request with { FirmwareRegions = actualRanges };
        Token(request.Target, nameof(request.Target));
        if (request.BaudRate is < 300 or > 4_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Baud rate must be 300..4000000.");
        }
        var firmware = Path.GetFullPath(request.FirmwarePath);
        var directory = Path.GetFullPath(request.WorkingDirectory);
        var extension = Path.GetExtension(firmware).ToLowerInvariant();
        if (extension is not (".hex" or ".bin" or ".elf" or ".uf2") && !(extension == ".zip" && request.Protocol == EmbeddedFlashProtocol.NrfDfu))
        {
            throw new ArgumentException("Firmware must be HEX, BIN, ELF or UF2.", nameof(request));
        }
        var steps = new List<ProcessStartSpec>();
        string? readback = null;
        var verified = true;
        string? verification = null;
        void Add(string program, params string[] arguments) => steps.Add(new()
        {
            Program = program,
            Arguments = arguments,
            WorkingDirectory = directory,
        });
        var address = request.BinaryAddress?.ToString("X", CultureInfo.InvariantCulture);
        switch (request.Protocol)
        {
            case EmbeddedFlashProtocol.Avrdude:
                if (extension is not (".hex" or ".bin") || firmware.Contains(':', StringComparison.Ordinal) || extension == ".bin" && request.BinaryAddress != 0)
                {
                    throw new ArgumentException("avrdude requires a HEX/BIN path without a colon. Raw BIN starts at flash byte zero; use addressed HEX for a nonzero application address.", nameof(request));
                }
                Token(request.Programmer, nameof(request.Programmer));
                Serial(request.SerialPort);
                var format = extension == ".hex" ? "i" : "r";
                Add("avrdude", "-p", request.Target, "-c", request.Programmer!, "-P", request.SerialPort!, "-b", request.BaudRate.ToString(CultureInfo.InvariantCulture),
                    "-D", "-U", $"flash:w:{firmware}:{format}", "-U", $"flash:v:{firmware}:{format}");
                break;
            case EmbeddedFlashProtocol.Esptool:
                if (request.Target is not ("esp32" or "esp32s2" or "esp32s3" or "esp32c3" or "esp32c6" or "esp32h2") || extension != ".bin" || address is null)
                {
                    throw new ArgumentException("esptool requires an exact ESP chip and an explicitly located BIN image; use the core's flash arguments for multi-image firmware.", nameof(request));
                }
                Serial(request.SerialPort);
                Add("esptool.py", "--chip", request.Target, "--port", request.SerialPort!, "--baud", request.BaudRate.ToString(CultureInfo.InvariantCulture),
                    "--after", "no-reset", "write-flash", "0x" + address, firmware);
                Add("esptool.py", "--chip", request.Target, "--port", request.SerialPort!, "--before", "no-reset", "--after", "hard-reset", "verify-flash", "0x" + address, firmware);
                break;
            case EmbeddedFlashProtocol.Stm32Flash:
                Serial(request.SerialPort);
                if (extension != ".bin" || address is null || request.FirmwareRegions.Count != 1)
                {
                    throw new ArgumentException("stm32flash requires one explicitly located raw binary.", nameof(request));
                }
                Add("stm32flash", "-b", request.BaudRate.ToString(CultureInfo.InvariantCulture), "-S", "0x" + address + ":" + request.FirmwareRegions[0].Length.ToString(CultureInfo.InvariantCulture),
                    "-w", firmware, "-v", request.SerialPort!);
                break;
            case EmbeddedFlashProtocol.Stm32Dfu:
                if (extension != ".bin" || address is null || request.FirmwareRegions.Count != 1 || request.DfuAlternate < 0 || !UsbId(request.UsbSelector))
                {
                    throw new ArgumentException("STM32 DFU requires a binary address, one range, alternate and exact VID:PID.", nameof(request));
                }
                readback = Path.Combine(directory, ".tabstudio", "embedded", Guid.NewGuid().ToString("N") + ".readback.bin");
                var alternate = request.DfuAlternate.ToString(CultureInfo.InvariantCulture);
                Add("dfu-util", "-d", request.UsbSelector!, "-a", alternate, "-s", "0x" + address, "-D", firmware);
                Add("dfu-util", "-d", request.UsbSelector!, "-a", alternate, "-s", "0x" + address + ":" + request.FirmwareRegions[0].Length.ToString(CultureInfo.InvariantCulture), "-U", readback);
                break;
            case EmbeddedFlashProtocol.Picotool:
                if (extension is not (".elf" or ".uf2" or ".bin"))
                {
                    throw new ArgumentException("picotool requires ELF, UF2 or explicitly located BIN.", nameof(request));
                }
                if (extension == ".bin")
                {
                    if (address is null)
                    {
                        throw new ArgumentException("A raw binary needs its target address.", nameof(request));
                    }
                    Add("picotool", "load", "-v", "-t", "bin", "-o", "0x" + address, firmware);
                }
                else
                {
                    Add("picotool", "load", "-v", firmware);
                }
                break;
            case EmbeddedFlashProtocol.Bossac:
                Serial(request.SerialPort);
                if (extension != ".bin" || request.BinaryAddress is not { } binaryStart || binaryStart < request.FlashBaseAddress ||
                    binaryStart - request.FlashBaseAddress > int.MaxValue)
                {
                    throw new ArgumentException("bossac requires a board-specific application BIN and a representable offset from the actual main-flash base.", nameof(request));
                }
                // No -b boot-configuration flag: application upload is not permission to change GPNVM bits.
                Add("bossac", "--port=" + request.SerialPort, "-o", (binaryStart - request.FlashBaseAddress).ToString(CultureInfo.InvariantCulture), "-w", "-v", firmware, "-R");
                break;
            case EmbeddedFlashProtocol.OpenOcd:
                Config(request.InterfaceConfig);
                Config(request.TargetConfig);
                if (extension == ".uf2")
                {
                    throw new ArgumentException("Convert UF2 to an ELF/HEX/BIN for OpenOCD.", nameof(request));
                }
                var commands = new List<string> { "-f", request.InterfaceConfig!, "-f", request.TargetConfig! };
                if (request.ProbeSelector is { } selector)
                {
                    commands.AddRange(["-c", "adapter serial " + TclWord(selector)]);
                }
                if (extension == ".bin" && address is null)
                {
                    throw new ArgumentException("OpenOCD raw binaries need an address.", nameof(request));
                }
                commands.AddRange(["-c", "program " + TclWord(firmware) + (extension == ".bin" ? " 0x" + address : string.Empty) + " verify reset exit"]);
                Add("openocd", [.. commands]);
                break;
            case EmbeddedFlashProtocol.ProbeRs:
                if (request.ProbeSelector is null)
                {
                    throw new ArgumentException("Select a specific probe before flashing.", nameof(request));
                }
                if (extension != ".elf")
                {
                    throw new ArgumentException("This probe-rs route requires an addressed ELF.", nameof(request));
                }
                Add("probe-rs", "download", "--chip", request.Target, "--probe", request.ProbeSelector, "--verify", firmware);
                break;
            case EmbeddedFlashProtocol.Teensy:
                if (extension != ".hex" || request.Target.ToUpperInvariant() is not ("TEENSY2" or "TEENSY2PP" or "TEENSYLC" or "TEENSY30" or "TEENSY31" or "TEENSY32" or "TEENSY35" or "TEENSY36" or "TEENSY40" or "TEENSY41" or "TEENSY_MICROMOD"))
                {
                    throw new ArgumentException("Select the exact Teensy board ID (for example TEENSY40 or TEENSY41) and its HEX image.", nameof(request));
                }
                // No -s: it soft-reboots the first serial Teensy it finds, potentially a different board.
                Add("teensy_loader_cli", "--mcu=" + request.Target.ToUpperInvariant(), "-v", firmware);
                verified = false;
                verification = "HalfKay transport completed; this upstream loader does not offer flash readback. Production verification is unavailable.";
                break;
            case EmbeddedFlashProtocol.WchIsp:
                if (extension != ".bin" || request.BinaryAddress != 0 || !request.Target.StartsWith("CH", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("WCH ISP requires a raw BIN explicitly based at code-flash address zero. Addressed images must be converted without rebasing their application.", nameof(request));
                }
                var transport = new List<string>();
                if (request.UseSerialTransport)
                {
                    Serial(request.SerialPort);
                    if (request.BaudRate is not (115200 or 1_000_000 or 2_000_000))
                    {
                        throw new ArgumentException("wchisp UART supports 115200, 1000000 or 2000000 baud.", nameof(request));
                    }
                    transport.AddRange(["--serial", "--port", request.SerialPort!, "--baudrate", request.BaudRate.ToString(CultureInfo.InvariantCulture)]);
                }
                else
                {
                    if (request.ProbeSelector is { } index && (!int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0))
                    {
                        throw new ArgumentException("wchisp's USB selector is a non-negative device index; a permissioned worker exposes index zero only.", nameof(request));
                    }
                    transport.AddRange(["--usb", "--device", request.ProbeSelector ?? "0"]);
                }
                // info --chip checks the actual ISP-reported chip prefix; flash does not call config/unprotect.
                Add("wchisp", [.. transport, "info", "--chip", request.Target.ToUpperInvariant()]);
                Add("wchisp", [.. transport, "flash", firmware]);
                verification = "wchisp verifies programmed code flash through the ISP protocol; configuration reset/unprotect commands are never requested.";
                break;
            case EmbeddedFlashProtocol.MspDebug:
                if (extension is not (".hex" or ".elf") || request.Programmer is not ("rf2500" or "olimex" or "olimex-v1" or "olimex-iso" or "olimex-iso-mk2" or "uif" or "goodfet" or "mehfet"))
                {
                    throw new ArgumentException("Select an addressed MSP430 HEX/ELF and a non-BSL mspdebug driver. BSL drivers that auto-unlock/mass-erase are refused.", nameof(request));
                }
                var mspArguments = new List<string> { "-n" }; // Never run project/home .mspdebug startup commands.
                if (request.UseSerialTransport)
                {
                    Serial(request.SerialPort);
                    mspArguments.AddRange(["-d", request.SerialPort!]);
                }
                else if (request.ProbeSelector is { } serial)
                {
                    if (serial.Any(char.IsControl))
                    {
                        throw new ArgumentException("A probe serial cannot contain control characters.", nameof(request));
                    }
                    mspArguments.AddRange(["-s", serial]);
                }
                if (request.Programmer == "mehfet")
                {
                    if (!UsbId(request.UsbSelector))
                    {
                        throw new ArgumentException("The mehfet driver requires its explicit VID:PID.", nameof(request));
                    }
                    mspArguments.AddRange(["-V", request.UsbSelector!]);
                }
                if (firmware.Any(c => c is '\'' or '"' or '\\' or ';' || char.IsControl(c)))
                {
                    throw new ArgumentException("mspdebug firmware paths cannot contain command quoting/control characters; move the image to a simple path.", nameof(request));
                }
                mspArguments.AddRange([request.Programmer, "prog \"" + firmware + "\"", "verify \"" + firmware + "\""]);
                Add("mspdebug", [.. mspArguments]);
                verification = "mspdebug compares the addressed firmware with target memory; BSL/protection/fuse operations and startup scripts are disabled.";
                break;
            case EmbeddedFlashProtocol.NrfDfu:
                if (extension != ".zip" || !(request.Target.StartsWith("nrf51", StringComparison.OrdinalIgnoreCase) || request.Target.StartsWith("nrf52", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Legacy Nordic Secure DFU requires an nRF51/nRF52 target and an application-only package ZIP.", nameof(request));
                }
                Serial(request.SerialPort);
                Add("nrfutil", "dfu", request.NrfDfuUart ? "serial" : "usb-serial", "--package", firmware, "--port", request.SerialPort!,
                    "--baud-rate", request.BaudRate.ToString(CultureInfo.InvariantCulture), "--flow-control", request.HardwareFlowControl ? "1" : "0");
                verification = "Nordic Secure DFU validates object CRCs and the bootloader validates application compatibility/signature; this route does not provide raw flash readback.";
                break;
            case EmbeddedFlashProtocol.PymcuprogUpdi:
                if (extension != ".hex" || !(request.Target.StartsWith("ATtiny", StringComparison.OrdinalIgnoreCase) || request.Target.StartsWith("ATmega", StringComparison.OrdinalIgnoreCase) || request.Target.StartsWith("AVR", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Serial UPDI requires an exact supported AVR device and an application-only Intel HEX image.", nameof(request));
                }
                Serial(request.SerialPort);
                steps.Add(new()
                {
                    Program = "python3",
                    Arguments = ["-m", "pymcuprog.pymcuprog", "write", "-t", "uart", "-u", request.SerialPort!, "-d", request.Target.ToLowerInvariant(),
                        "-c", request.BaudRate.ToString(CultureInfo.InvariantCulture), "-f", firmware, "--verify"],
                    WorkingDirectory = directory,
                    Environment = new Dictionary<string, string?> { ["PYTHONPATH"] = TabStudio.Toolchains.Microchip.MicrochipInstallation.ToolsDirectory + "/python" },
                });
                verification = "pymcuprog reads back and verifies application memory over UART UPDI; no high-voltage activation, chip unlock, user-row or fuse operations are requested.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
        return new(steps, request.Touch1200Baud, verification ?? (readback is null ? "Upstream tool verifies programmed application flash." : "Independent raw-flash readback compared byte-for-byte."), readback, firmware, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(imageBytes)), verified);
    }

    private static void Token(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || !value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
        {
            throw new ArgumentException("An explicit tool identifier containing letters, digits, underscore, hyphen or dot is required.", name);
        }
    }

    private static void Serial(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) || !Path.IsPathRooted(path))
        {
            throw new ArgumentException("Select an absolute serial bridge device path.", nameof(path));
        }
    }

    private static void Config(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-' or '.')) || path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new ArgumentException("Select an installed OpenOCD configuration file.", nameof(path));
        }
    }

    private static bool UsbId(string? value) => value is { Length: 9 } && value[4] == ':' && value.Where((_, index) => index != 4).All(Uri.IsHexDigit);

    private static string TclWord(string value)
    {
        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Control characters are not valid in a tool path or probe selector.", nameof(value));
        }
        var result = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '\\' or '"' or '$' or '[' or ']')
            {
                result.Append('\\');
            }
            result.Append(c);
        }
        return result.Append('"').ToString();
    }
}
