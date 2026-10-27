using System.Globalization;
using System.Text.Json.Nodes;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>probe-rs 0.32.0 DAP attach schema; no flashing, recovery, reset-under-connect or external listeners.</summary>
public static class ProbeRsDebugPlan
{
    /// <summary>Creates a loopback DAP server; omit single-session so its ready log is flushed before connecting.</summary>
    /// <param name="directory">Project working directory.</param>
    /// <param name="port">Private DAP port.</param>
    public static ProcessStartSpec Server(string directory, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        return new ProcessStartSpec
        {
            Program = "probe-rs", Arguments = ["dap-server", "--ip", "127.0.0.1", "--port", port.ToString(CultureInfo.InvariantCulture)],
            WorkingDirectory = directory,
            Environment = new Dictionary<string, string?>
            {
                ["PROBE_RS_ALLOW_ERASE_ALL"] = null, ["PROBE_RS_CYCLE_POWER"] = null,
                ["PROBE_RS_CONNECT_UNDER_RESET"] = null, ["PROBE_RS_CHIP_DESCRIPTION_PATH"] = null,
                ["PROBE_RS_CHIP"] = null, ["PROBE_RS_PROBE"] = null, ["PROBE_RS_PROTOCOL"] = null,
                ["PROBE_RS_SPEED"] = null, ["PROBE_RS_PREFER_FLASH_ALGO"] = null,
            },
        };
    }

    /// <summary>Builds the upstream SessionConfig/CoreConfig attach properties with destructive permissions disabled.</summary>
    /// <param name="profile">Validated explicit chip/core configuration.</param>
    /// <param name="projectDirectory">Workspace/project working directory.</param>
    /// <param name="firmware">Actual absolute matching firmware ELF.</param>
    /// <param name="port">Already listening DAP server port.</param>
    /// <param name="selectedProbe">Optional VID:PID of the one Android permissioned inventory.</param>
    public static JsonObject Attach(EmbeddedDebugProfile profile, string projectDirectory, string firmware, int port, string? selectedProbe = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (profile.Backend != EmbeddedDebugBackend.ProbeRs) throw new ArgumentException("Select the ProbeRs backend.", nameof(profile));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (!Path.IsPathFullyQualified(firmware)) throw new ArgumentException("Probe-rs requires the actual absolute firmware ELF.", nameof(firmware));
        return new JsonObject
        {
            ["name"] = "On-chip probe-rs", ["type"] = "embedded-probe-rs", ["request"] = "attach", ["debugServer"] = port,
            ["program"] = firmware, ["cwd"] = projectDirectory, ["chip"] = profile.Chip,
            ["probe"] = profile.ProbeSelector ?? selectedProbe,
            ["allowEraseAll"] = false, ["connectUnderReset"] = false,
            ["flashingConfig"] = new JsonObject
            {
                ["flashingEnabled"] = false, ["haltAfterReset"] = false, ["fullChipErase"] = false,
                ["restoreUnwrittenBytes"] = false,
            },
            ["coreConfigs"] = new JsonArray(new JsonObject
            {
                ["coreIndex"] = profile.CoreIndex, ["programBinary"] = firmware, ["rttEnabled"] = false,
            }),
        };
    }
}
