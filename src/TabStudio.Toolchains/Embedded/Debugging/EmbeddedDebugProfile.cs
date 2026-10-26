using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Debugging;

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

    /// <summary>An installed <c>interface/*.cfg</c> name, such as <c>interface/stlink.cfg</c>.</summary>
    public required string InterfaceConfig { get; init; }

    /// <summary>An installed <c>target/*.cfg</c> name, such as <c>target/stm32f4x.cfg</c>.</summary>
    public required string TargetConfig { get; init; }

    /// <summary>Optional exact probe serial; ASCII letters, digits, dot, underscore, colon and hyphen only.</summary>
    public string? ProbeSelector { get; init; }

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
        ValidateConfiguration(InterfaceConfig, "interface/");
        ValidateConfiguration(TargetConfig, "target/");
        if (ProbeSelector is not null && (ProbeSelector.Length is 0 or > 256 || ProbeSelector.Any(character =>
            !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-'))))
        {
            throw new InvalidDataException("The probe selector must be an exact serial using letters, digits, dot, underscore, colon or hyphen.");
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
    public ProcessStartSpec CreateServer(string projectDirectory, string scriptsDirectory, int gdbPort)
    {
        Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsDirectory);
        if (gdbPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(gdbPort));
        List<string> arguments = ["-f", CombineGuest(scriptsDirectory, InterfaceConfig)];
        if (ProbeSelector is { } selector) arguments.AddRange(["-c", "adapter serial " + selector]);
        arguments.AddRange(["-f", CombineGuest(scriptsDirectory, TargetConfig),
            "-c", "bindto 127.0.0.1", "-c", "gdb_port " + gdbPort.ToString(CultureInfo.InvariantCulture),
            "-c", "tcl_port disabled", "-c", "telnet_port disabled", "-c", "init", "-c", "halt"]);
        return new ProcessStartSpec { Program = "openocd", Arguments = arguments, WorkingDirectory = projectDirectory };
    }

    private static string CombineGuest(string directory, string config) => directory.TrimEnd('/', '\\') + "/" + config;

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
}
