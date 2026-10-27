using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Toolchains.Embedded.Installation;

namespace TabStudio.Toolchains.Embedded.Devices;

/// <summary>The specific evidence reported by probe-rs info, independent of VID/PID guesses.</summary>
public enum ProbeTargetFactKind
{
    /// <summary>The upstream tool explicitly reported a detected chip model.</summary>
    DetectedChip,
    /// <summary>A silicon JTAG IDCODE, not a unique board identity or complete MCU model.</summary>
    Idcode,
    /// <summary>A reported ARM debug-port version/designer record.</summary>
    DebugPort,
    /// <summary>The CPUID implementer reported by the core.</summary>
    CoreImplementer,
    /// <summary>The CPUID part name reported by the upstream decoder.</summary>
    CorePart,
    /// <summary>The reported CPUID variant.</summary>
    CoreVariant,
    /// <summary>The reported CPUID revision.</summary>
    CoreRevision,
    /// <summary>An explicitly reported vendor device-identification register.</summary>
    VendorDeviceId,
}

/// <summary>An explicit tool-reported fact with the original evidence line and protocol context.</summary>
/// <param name="Kind">What was reported.</param>
/// <param name="Value">Exact decoded value reported by the tool, without a guessed chip mapping.</param>
/// <param name="Protocol">SWD/JTAG context, if present.</param>
/// <param name="Architecture">Architecture explicitly named by the tool's output, if present.</param>
/// <param name="CoreOrdinal">One-based CPUID block ordinal, or null for non-core facts.</param>
/// <param name="Line">One-based original output line.</param>
/// <param name="Evidence">The original line after ANSI escape removal.</param>
public sealed record ProbeTargetFact(ProbeTargetFactKind Kind, string Value, string? Protocol,
    string? Architecture, int? CoreOrdinal, int Line, string Evidence);

/// <summary>A bounded parse of probe-rs 0.32 info output. An empty fact list never means a chip was identified.</summary>
/// <param name="Facts">Only explicit recognized records from the tool's output.</param>
public sealed record ProbeTargetInfo(IReadOnlyList<ProbeTargetFact> Facts)
{
    /// <summary>Whether the tool reported at least one concrete identity/debug/core record.</summary>
    public bool HasFacts => Facts.Count > 0;
}

/// <summary>Safe command construction and parsing for upstream probe-rs 0.32 info.</summary>
/// <remarks>
/// Source contract: probe-rs commit 48f5e4d53c690a1d40c2454033c6f785b4f4f95c,
/// probe-rs-tools/src/bin/probe-rs/cmd/info.rs and rpc/functions/info.rs.
/// Verbose info uses DefaultArmSequence with erase-all/cycle-power permissions false.
/// It attaches to inspect debug registers; it is not a flash command and may affect live debug state.
/// </remarks>
public static class ProbeTargetInfoReader
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly string[] ProbeEnvironment =
    [
        "PROBE_RS_CHIP", "PROBE_RS_CHIP_DESCRIPTION_PATH", "PROBE_RS_PROTOCOL", "PROBE_RS_CYCLE_POWER",
        "PROBE_RS_NON_INTERACTIVE", "PROBE_RS_PROBE", "PROBE_RS_SPEED", "PROBE_RS_CONNECT_UNDER_RESET",
        "PROBE_RS_DRY_RUN", "PROBE_RS_ALLOW_ERASE_ALL", "PROBE_RS_PREFER_FLASH_ALGO", "PROBE_RS_CONFIG_PRESET",
        "PROBE_RS_REMOTE_HOST", "PROBE_RS_REMOTE_TOKEN"
    ];

    /// <summary>Clears inherited target/erase/reset/power overrides before a typed info/logging operation selects its explicit arguments.</summary>
    public static IReadOnlyDictionary<string, string?> SafeAttachEnvironment()
    {
        var environment = ProbeEnvironment.ToDictionary(name => name, _ => (string?)null, StringComparer.Ordinal);
        environment[EmbeddedGlibcRuntime.RoutingMarker] = "1";
        environment[ProbeRsSafeLaunch.Marker] = "1";
        return environment;
    }

    /// <summary>Constructs fixed verbose info arguments for exactly the selected USB VID/PID, clearing inherited attach/erase/reset overrides.</summary>
    public static ProcessStartSpec Command(UsbDeviceInfo device, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (device.VendorId is < 0 or > ushort.MaxValue || device.ProductId is < 0 or > ushort.MaxValue)
            throw new ArgumentException("USB VID/PID must be unsigned 16-bit values.", nameof(device));
        return new ProcessStartSpec
        {
            Program = "probe-rs",
            Arguments = ["info", "--verbose", "--non-interactive", "--probe", string.Create(CultureInfo.InvariantCulture, $"{device.VendorId:x4}:{device.ProductId:x4}")],
            WorkingDirectory = workingDirectory,
            Environment = SafeAttachEnvironment(),
        };
    }

    /// <summary>Parses only the upstream formatter's explicit IDCODE, debug-port, CPUID and vendor DID records; unrecognized text remains unclassified.</summary>
    public static ProbeTargetInfo Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length > 1024 * 1024) throw new InvalidDataException("Probe info output exceeds 1 MiB.");
        var facts = new List<ProbeTargetFact>();
        var lines = output.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        string? protocol = null;
        string? architecture = null;
        int? core = null;
        var coreCount = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var evidence = Regex.Replace(lines[i], "\u001B\\[[0-?]*[ -/]*[@-~]", "", RegexOptions.CultureInvariant, RegexTimeout);
            var line = evidence.TrimStart(' ', '\t', '│', '├', '└', '─', '┬', '|');
            if (line is "Probing target via JTAG" or "Probing target via SWD")
            {
                protocol = line.EndsWith("JTAG", StringComparison.Ordinal) ? "JTAG" : "SWD";
                architecture = null;
                core = null;
                continue;
            }
            if (line is "RISC-V Chip:" or "Xtensa Chip:")
            {
                architecture = line[..^6];
                core = null;
                continue;
            }
            if (line.StartsWith("Detected chip: ", StringComparison.Ordinal))
            {
                var chip = line[15..].Trim();
                if (chip.Length > 0) Add(ProbeTargetFactKind.DetectedChip, chip, null);
                continue;
            }
            if (line.StartsWith("Debug Port: DPv", StringComparison.Ordinal))
            {
                architecture = "ARM";
                core = null;
                Add(ProbeTargetFactKind.DebugPort, line[12..].Trim(), null);
                continue;
            }
            if (line == "CPUID") { architecture = "ARM"; core = ++coreCount; continue; }
            var idcode = Regex.Match(line, @"^IDCODE:\s*(?:0x)?([0-9a-fA-F]{1,10})\s*$", RegexOptions.CultureInvariant, RegexTimeout);
            if (idcode.Success && uint.TryParse(idcode.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var identity))
            {
                Add(ProbeTargetFactKind.Idcode, "0x" + identity.ToString("X8", CultureInfo.InvariantCulture), null);
                continue;
            }
            if (core is not null)
            {
                var field = Regex.Match(line, @"^(IMPLEMENTER|PARTNO|VARIANT|REVISION):\s*(\S[^\r\n]*)$", RegexOptions.CultureInvariant, RegexTimeout);
                if (field.Success)
                {
                    var kind = field.Groups[1].Value switch
                    {
                        "IMPLEMENTER" => ProbeTargetFactKind.CoreImplementer,
                        "PARTNO" => ProbeTargetFactKind.CorePart,
                        "VARIANT" => ProbeTargetFactKind.CoreVariant,
                        _ => ProbeTargetFactKind.CoreRevision,
                    };
                    Add(kind, field.Groups[2].Value.Trim(), core);
                    continue;
                }
            }
            var vendor = Regex.Match(line, @"^Atmel device \(DID = (0x[0-9a-fA-F]{8})\)$", RegexOptions.CultureInvariant, RegexTimeout);
            if (vendor.Success) Add(ProbeTargetFactKind.VendorDeviceId, vendor.Groups[1].Value, null);

            void Add(ProbeTargetFactKind kind, string value, int? ordinal) => facts.Add(new(kind, value, protocol, architecture, ordinal, i + 1, evidence));
        }
        return new(facts);
    }
}
