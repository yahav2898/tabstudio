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
}

/// <summary>A complete upload sequence. Readback comparison is performed only after all steps succeed.</summary>
/// <param name="Steps">Ordered unmodified upstream tool invocations.</param>
/// <param name="Touch1200Baud">The caller must touch and reacquire the bootloader port before running steps.</param>
/// <param name="Verification">How verification is established.</param>
/// <param name="ReadbackPath">A raw flash dump to compare byte-for-byte to the input, if needed.</param>
/// <param name="FirmwarePath">Input file for readback comparison.</param>
/// <param name="FirmwareSha256">Digest bound to the validated firmware ranges.</param>
public sealed record EmbeddedFlashPlan(IReadOnlyList<ProcessStartSpec> Steps, bool Touch1200Baud, string Verification,
    string? ReadbackPath = null, string? FirmwarePath = null, string? FirmwareSha256 = null);

/// <summary>Creates argument-safe upload commands. It never accepts arbitrary extra flags or configuration-write operations.</summary>
public static class EmbeddedFlashPlanner
{
    /// <summary>Plans an application upload; unsupported verification or incomplete identity is rejected before erase.</summary>
    public static EmbeddedFlashPlan Plan(EmbeddedFlashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var imageBytes = File.ReadAllBytes(request.FirmwarePath);
        var actualRanges = FirmwareImageInspector.Inspect(imageBytes, Path.GetExtension(request.FirmwarePath), request.BinaryAddress);
        FlashSafety.ValidateFirmware(actualRanges, request.ApplicationFlash);
        request = request with { FirmwareRegions = actualRanges };
        Token(request.Target, nameof(request.Target));
        if (request.BaudRate is < 300 or > 4_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Baud rate must be 300..4000000.");
        }
        var firmware = Path.GetFullPath(request.FirmwarePath);
        var directory = Path.GetFullPath(request.WorkingDirectory);
        var extension = Path.GetExtension(firmware).ToLowerInvariant();
        if (extension is not (".hex" or ".bin" or ".elf" or ".uf2"))
        {
            throw new ArgumentException("Firmware must be HEX, BIN, ELF or UF2.", nameof(request));
        }
        var steps = new List<ProcessStartSpec>();
        string? readback = null;
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
                if (extension is not (".hex" or ".bin") || firmware.Contains(':', StringComparison.Ordinal))
                {
                    throw new ArgumentException("avrdude requires a HEX/BIN path without a colon.", nameof(request));
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
                    "write_flash", "0x" + address, firmware);
                Add("esptool.py", "--chip", request.Target, "--port", request.SerialPort!, "verify_flash", "0x" + address, firmware);
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
                if (extension != ".bin")
                {
                    throw new ArgumentException("bossac requires a board-specific application BIN.", nameof(request));
                }
                // No -b boot-configuration flag: application upload is not permission to change GPNVM bits.
                Add("bossac", "--port=" + request.SerialPort, "-w", "-v", firmware, "-R");
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
                throw new NotSupportedException("teensy_loader_cli cannot read back application flash. A verified production upload is unavailable through this route.");
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
        return new(steps, request.Touch1200Baud, readback is null ? "Upstream tool verifies programmed application flash." : "Independent raw-flash readback compared byte-for-byte.", readback, firmware, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(imageBytes)));
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
