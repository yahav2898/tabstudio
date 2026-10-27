using System.Globalization;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>Native probe-rs0.32 RTT/defmt logging from a matching ELF, using attach rather than flash/run.</summary>
/// <remarks>
/// Source contract: probe-rs commit48f5e4d53c690a1d40c2454033c6f785b4f4f95c,
/// cmd/attach.rs, util/cli.rs and rpc/functions/monitor.rs. AttachToRunning does not flash/reset or clear the RTT
/// header. The upstream decoder uses the ELF defmt table for an RTT channel named defmt. The target may be
/// resumed and its RTT channel mode changed in RAM; this is separate from a debugger owning the same probe.
/// </remarks>
public static class ProbeRsLogPlan
{
    /// <summary>Requires one actual writable RTT control-block object symbol; no SRAM scan or device address is guessed.</summary>
    public static ulong ControlBlockAddress(FirmwareMemoryReport firmware)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        var matches = firmware.Symbols.Where(symbol => symbol.Name == "_SEGGER_RTT" && symbol.Size >= 24 &&
            firmware.Sections.Any(section => section.Name == symbol.Section && section.IsRam &&
                symbol.Address >= section.Address && symbol.Address - section.Address < section.Size &&
                symbol.Size <= section.Size - (symbol.Address - section.Address))).Select(symbol => symbol.Address).Distinct().ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("RTT logging needs the running firmware's matching ELF with one writable _SEGGER_RTT control-block symbol. Keep that symbol; TabStudio does not guess a RAM scan range.");
        return matches[0];
    }

    /// <summary>Attaches to an explicitly selected chip/probe and streams all upstream RTT channels; never emits a flash, erase or reset command.</summary>
    public static ProcessStartSpec Command(EmbeddedDebugProfile profile, string projectDirectory, string firmware,
        ulong rttAddress, string? selectedProbe = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (profile.Backend != EmbeddedDebugBackend.ProbeRs) throw new InvalidOperationException("Select Backend ProbeRs and its actual chip in embedded-debug.json before starting native RTT/defmt logs.");
        if (!Path.IsPathFullyQualified(firmware)) throw new ArgumentException("Select the actual absolute matching ELF path.", nameof(firmware));
        var selector = profile.ProbeSelector ?? selectedProbe;
        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidDataException("Select the exact probe before starting RTT logs.");
        if (selector.Length > 256 || selector.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_' or '.')))
            throw new InvalidDataException("Invalid probe selector.");
        return new ProcessStartSpec
        {
            Program = "probe-rs",
            Arguments = ["attach", "--chip", profile.Chip!, "--probe", selector, "--non-interactive",
                "--no-catch-reset", "--no-catch-hardfault", "--no-catch-svc", "--no-catch-hlt",
                "--rtt-channel-mode", "no-block-skip", "--log-format", "full",
                "--scan-region", "0x" + rttAddress.ToString("x", CultureInfo.InvariantCulture), "--", firmware],
            WorkingDirectory = projectDirectory,
            Environment = ProbeTargetInfoReader.SafeAttachEnvironment(),
        };
    }
}
