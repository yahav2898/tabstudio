using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Embedded.Analysis;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>The explicitly selected on-chip debug backend.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmbeddedDebugBackend>))]
public enum EmbeddedDebugBackend
{
    /// <summary>OpenOCD's GDB server and multi-target GDB DAP.</summary>
    OpenOcd,
    /// <summary>probe-rs' own DAP server, with flashing and mass erase disabled.</summary>
    ProbeRs,
}

/// <summary>Explicit on-chip debug choices; no arbitrary OpenOCD/Tcl commands are accepted.</summary>
public sealed record EmbeddedDebugProfile
{
    /// <summary>Project-relative path of the typed configuration.</summary>
    public const string FileName = ".tabstudio/embedded-debug.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>ELF containing symbols for the firmware already programmed onto the selected chip.</summary>
    public required string Firmware { get; init; }

    /// <summary>Explicit backend; existing profiles retain OpenOCD.</summary>
    public EmbeddedDebugBackend Backend { get; init; }

    /// <summary>Exact built-in target name required by the probe-rs backend.</summary>
    public string? Chip { get; init; }

    /// <summary>Single core to debug through probe-rs, from 0 to 15.</summary>
    public int CoreIndex { get; init; }

    /// <summary>An installed <c>interface/*.cfg</c> name, such as <c>interface/stlink.cfg</c>.</summary>
    public string InterfaceConfig { get; init; } = string.Empty;

    /// <summary>An installed <c>target/*.cfg</c> name, such as <c>target/stm32f4x.cfg</c>.</summary>
    public string TargetConfig { get; init; } = string.Empty;

    /// <summary>Optional exact probe serial; ASCII letters, digits, dot, underscore, colon and hyphen only.</summary>
    public string? ProbeSelector { get; init; }

    /// <summary>Explicit live-fault model: cortex-m-v7 or cortex-m-v8-mainline. Null disables live capture.</summary>
    public string? FaultModel { get; init; }

    /// <summary>Optional RTT control-block search start, within explicitly configured SRAM.</summary>
    [JsonConverter(typeof(RttAddressConverter))]
    public ulong? RttAddress { get; init; }

    /// <summary>Optional bounded SRAM search size, 16 to 65,536 bytes; required with RttAddress.</summary>
    public uint? RttSearchSize { get; init; }

    /// <summary>RTT up-channel containing UTF-8 text; binary defmt is not decoded by this text logger.</summary>
    public int RttChannel { get; init; }

    /// <summary>Parses bounded JSON, rejecting unknown fields, invalid config names and Tcl metacharacters.</summary>
    /// <param name="json">Configuration text.</param>
    public static EmbeddedDebugProfile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 65536) throw new InvalidDataException("The embedded debug profile exceeds 64 KiB.");
        var profile = JsonSerializer.Deserialize<EmbeddedDebugProfile>(json, JsonOptions)
            ?? throw new InvalidDataException("The embedded debug profile is empty.");
        profile.Validate();
        return profile;
    }

    /// <summary>Validates the profile without starting tools or touching a device.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Firmware) || Firmware.Length > 4096 || Firmware.Any(char.IsControl))
            throw new InvalidDataException("Set the actual firmware ELF in embedded-debug.json.");
        if (!Enum.IsDefined(Backend)) throw new InvalidDataException("Select the OpenOcd or ProbeRs debug backend.");
        if (Backend == EmbeddedDebugBackend.OpenOcd)
        {
            ValidateConfiguration(InterfaceConfig, "interface/");
            ValidateConfiguration(TargetConfig, "target/");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Chip) || Chip.Length > 128 || Chip.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')))
                throw new InvalidDataException("Select the exact probe-rs built-in chip name.");
            if (CoreIndex is < 0 or > 15) throw new InvalidDataException("The probe-rs core index must be from 0 to 15.");
            if (FaultModel is not null || RttAddress is not null || RttSearchSize is not null || RttChannel != 0)
                throw new InvalidDataException("Live GDB fault capture and configured OpenOCD RTT are available through the OpenOcd backend. Probe-rs binary RTT/defmt is not decoded by this attach path.");
            ValidateProbeRsSelector(ProbeSelector);
        }
        if (FaultModel is not null && !CortexFaultCapture.IsSupportedModel(FaultModel))
            throw new InvalidDataException("FaultModel must be cortex-m-v7 or cortex-m-v8-mainline; Cortex-M0/M0+ and unknown layouts are not supported.");
        if (ProbeSelector is not null && (ProbeSelector.Length is 0 or > 256 || ProbeSelector.Any(character =>
            !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-'))))
        {
            throw new InvalidDataException("The probe selector must be an exact serial using letters, digits, dot, underscore, colon or hyphen.");
        }
        if (RttAddress.HasValue != RttSearchSize.HasValue || RttSearchSize is < 16 or > 65536 || RttChannel is < 0 or > 15
            || (RttAddress is { } address && ((address & 3) != 0 || address > ulong.MaxValue - RttSearchSize!.Value))
            || (RttAddress is null && RttChannel != 0))
        {
            throw new InvalidDataException("RTT requires an aligned address, a 16–65,536-byte search range, and a channel from 0 to 15.");
        }
    }

    /// <summary>Resolves and validates a firmware ELF without reading or changing any target memory.</summary>
    /// <param name="projectDirectory">Project folder.</param>
    public string ResolveFirmware(string projectDirectory)
    {
        Validate();
        var firmware = Path.GetFullPath(Firmware, projectDirectory);
        using var stream = File.OpenRead(firmware);
        Span<byte> header = stackalloc byte[64];
        var count = stream.Read(header);
        if (count < 52 || header[0] != 0x7f || header[1] != 'E' || header[2] != 'L' || header[3] != 'F'
            || header[4] is not (1 or 2) || header[5] is not (1 or 2) || header[6] != 1 || (header[4] == 2 && count < 64))
        {
            throw new InvalidDataException("On-chip debugging requires the build's ELF with matching debug symbols, not HEX/BIN/UF2.");
        }
        var type = header[5] == 1 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[16..])
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header[16..]);
        if (type is not (2 or 3)) throw new InvalidDataException("Select an executable firmware ELF, not a relocatable object file.");
        return firmware;
    }

    /// <summary>ELF e_machine used to check the debugger's actual compiled-in architecture list.</summary>
    /// <param name="projectDirectory">Project folder.</param>
    public ushort ElfMachine(string projectDirectory)
    {
        var firmware = ResolveFirmware(projectDirectory);
        using var stream = File.OpenRead(firmware);
        Span<byte> header = stackalloc byte[20];
        stream.ReadExactly(header);
        return header[5] == 1 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[18..])
            : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header[18..]);
    }

    /// <summary>Starts a loopback-only GDB endpoint with Tcl and telnet servers disabled; does not flash firmware.</summary>
    /// <param name="projectDirectory">Working directory.</param>
    /// <param name="scriptsDirectory">The installed OpenOCD scripts directory, not the project folder.</param>
    /// <param name="gdbPort">Reserved loopback GDB port.</param>
    /// <param name="rttPort">Separate loopback RTT port when logging is enabled.</param>
    /// <param name="memoryRegions">Actual chip memory regions from embedded-analysis.json; RTT never guesses SRAM.</param>
    public ProcessStartSpec CreateServer(string projectDirectory, string scriptsDirectory, int gdbPort, int? rttPort = null, IReadOnlyList<FirmwareMemoryRegion>? memoryRegions = null)
    {
        Validate();
        if (Backend != EmbeddedDebugBackend.OpenOcd) throw new InvalidOperationException("This profile uses probe-rs DAP rather than an OpenOCD GDB server.");
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsDirectory);
        if (gdbPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(gdbPort));
        List<string> arguments = ["-f", CombineGuest(scriptsDirectory, InterfaceConfig)];
        if (ProbeSelector is { } selector) arguments.AddRange(["-c", "adapter serial " + selector]);
        arguments.AddRange(["-f", CombineGuest(scriptsDirectory, TargetConfig),
            "-c", "bindto 127.0.0.1", "-c", "gdb_port " + gdbPort.ToString(CultureInfo.InvariantCulture),
            "-c", "tcl_port disabled", "-c", "telnet_port disabled", "-c", "init", "-c", "halt"]);
        if (RttAddress is { } address)
        {
            ValidateRttMemory(memoryRegions);
            if (rttPort is not { } port || port is < 1 or > 65535 || port == gdbPort) throw new ArgumentOutOfRangeException(nameof(rttPort));
            var setup = "rtt setup 0x" + address.ToString("x", CultureInfo.InvariantCulture) + " " + RttSearchSize!.Value.ToString(CultureInfo.InvariantCulture) + " \"SEGGER RTT\"; rtt start; rtt server start "
                + port.ToString(CultureInfo.InvariantCulture) + " " + RttChannel.ToString(CultureInfo.InvariantCulture);
            // All substitutions above are typed numbers. A missing control block
            // must report an RTT error without aborting an otherwise useful debugger.
            arguments.AddRange(["-c", "if {[catch {" + setup + "} tabstudio_rtt_error]} {echo \"TABSTUDIO_RTT_ERROR: $tabstudio_rtt_error\"}"]);
        }
        return new ProcessStartSpec { Program = "openocd", Arguments = arguments, WorkingDirectory = projectDirectory };
    }

    /// <summary>Requires the entire bounded scan inside an explicit SRAM region for the selected chip.</summary>
    /// <param name="regions">MemoryRegions from embedded-analysis.json.</param>
    public void ValidateRttMemory(IReadOnlyList<FirmwareMemoryRegion>? regions)
    {
        Validate();
        if (RttAddress is not { } address) return;
        var size = RttSearchSize!.Value;
        var withinRam = regions?.Any(region => region is not null && region.IsRam && region.Length > 0
            && region.Origin <= ulong.MaxValue - region.Length && address >= region.Origin
            && address - region.Origin <= region.Length && size <= region.Length - (address - region.Origin)) == true;
        if (!withinRam) throw new InvalidDataException("The entire RTT search must fit an explicit IsRam MemoryRegions range for this chip in .tabstudio/embedded-analysis.json.");
    }

    private static string CombineGuest(string directory, string config) => directory.TrimEnd('/', '\\') + "/" + config;

    private static void ValidateProbeRsSelector(string? selector)
    {
        if (selector is null) return;
        var parts = selector.Split(':', 3);
        if (parts.Length < 2 || parts[0].Length is < 1 or > 4 || !ushort.TryParse(parts[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _))
            throw new InvalidDataException("Probe-rs selector must be VID:PID[-INTERFACE][:SERIAL].");
        var product = parts[1].Split('-', 2);
        if (product[0].Length is < 1 or > 4 || !ushort.TryParse(product[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)
            || (product.Length == 2 && !byte.TryParse(product[1], NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            throw new InvalidDataException("Probe-rs selector must be VID:PID[-INTERFACE][:SERIAL].");
        }
    }

    private static void ValidateConfiguration(string? value, string prefix)
    {
        if (value is null || value.Length > 256 || !value.StartsWith(prefix, StringComparison.Ordinal)
            || !value.EndsWith(".cfg", StringComparison.Ordinal)
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.'))
            || value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Select an installed OpenOCD " + prefix + "*.cfg for the actual probe and chip.");
        }
    }

    private sealed class RttAddressConverter : JsonConverter<ulong?>
    {
        public RttAddressConverter() { }

        public override ulong? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetUInt64(out var numeric)) return numeric;
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } text)
            {
                var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                if (ulong.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out numeric)) return numeric;
            }
            throw new JsonException("RTT address must be an unsigned integer or a hexadecimal 0x address.");
        }

        public override void Write(Utf8JsonWriter writer, ulong? value, JsonSerializerOptions options)
        {
            if (value is { } address) writer.WriteStringValue("0x" + address.ToString("x", CultureInfo.InvariantCulture));
            else writer.WriteNullValue();
        }
    }
}
