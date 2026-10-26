using System.Text.Json;

namespace TabStudio.Toolchains.Microchip;

/// <summary>Explicit board/bootloader selection. A compiler device alone does not identify its programmer.</summary>
/// <param name="Protocol">Local protocol or windows-bridge.</param>
/// <param name="Device">Exact project device.</param>
/// <param name="Firmware">Project-relative HEX path.</param>
/// <param name="BridgeTool">IPE tool identifier.</param>
/// <param name="BridgeSerial">Optional IPE tool serial.</param>
public sealed record PicFlashProfile(string Protocol, string Device, string Firmware, string BridgeTool, string? BridgeSerial)
{
    /// <summary>Project-relative board configuration.</summary>
    public const string FileName = ".tabstudio/pic-target.json";

    /// <summary>Whether the protocol has an Android worker.</summary>
    public bool IsLocal => Protocol is "an1388-hid" or "an1388-uart" or "mcc-uart" or "pymcuprog" or "pk2cmd" or "pic32prog";

    /// <summary>Reads and validates a profile against the active MPLAB configuration.</summary>
    /// <param name="project">MPLAB project.</param>
    public static PicFlashProfile Load(MplabProject project)
    {
        var path = Path.Combine(project.Directory, FileName);
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("PIC target profile is too large.");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var protocol = root.GetProperty("protocol").GetString() ?? string.Empty;
        var device = root.GetProperty("device").GetString() ?? string.Empty;
        var firmware = root.GetProperty("firmware").GetString() ?? string.Empty;
        if (!device.Equals(project.SelectedConfiguration().Device, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The flash profile's device differs from the selected MPLAB configuration.");
        var full = Path.GetFullPath(Path.Combine(project.Directory, firmware));
        var relative = Path.GetRelativePath(project.Directory, full);
        if (Path.IsPathRooted(firmware) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !full.EndsWith(".hex", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a project-relative .hex firmware path.");
        if (protocol.StartsWith("an1388-", StringComparison.Ordinal) && !device.StartsWith("PIC32", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("AN1388 is a PIC32 bootloader.");
        if (protocol == "mcc-uart" && !device.StartsWith("PIC16", StringComparison.OrdinalIgnoreCase) && !device.StartsWith("PIC18", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("MCC UART requires a compatible PIC16/18 bootloader.");
        if (protocol == "pymcuprog" && (!root.TryGetProperty("tool", out var tool) || tool.GetString() != "nedbg")) throw new InvalidDataException("pymcuprog PIC uses PKOB nano (nedbg). For PICkit 4/5 or Snap use the Windows Bridge.");
        if (protocol == "pymcuprog")
        {
            using var supported = JsonDocument.Parse(MicrochipInstallation.Resource("pymcuprog.devices.json"));
            if (!supported.RootElement.GetProperty("devices").EnumerateArray().Any(d => string.Equals(d.GetString(), device, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("pymcuprog has no device module for " + device + "; use the Windows Bridge.");
        }
        return new PicFlashProfile(protocol, device, firmware, root.TryGetProperty("bridgeTool", out var b) ? b.GetString() ?? "PK4" : "PK4",
            root.TryGetProperty("bridgeSerial", out var s) ? s.GetString() : null);
    }
}
