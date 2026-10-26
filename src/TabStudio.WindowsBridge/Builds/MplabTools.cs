namespace TabStudio.WindowsBridge.Builds;

/// <summary>Microchip tools installed and licensed by the PC's owner.</summary>
/// <param name="Root">MPLAB X version directory.</param>
/// <param name="Make">MPLAB's GNU make.</param>
/// <param name="Generator">MPLAB's project makefile generator.</param>
/// <param name="Ipe">ipecmd executable or jar.</param>
/// <param name="Ide">MPLAB X IDE launcher.</param>
/// <param name="Java">Bundled Java, when IPE is a jar.</param>
public sealed record MplabTools(string Root, string? Make, string? Generator, string? Ipe, string? Ide, string? Java)
{
    /// <summary>Finds the newest installed MPLAB X without downloading or running it.</summary>
    /// <param name="programFolders">Program Files folders, or test locations.</param>
    public static MplabTools? Find(IEnumerable<string> programFolders)
    {
        var roots = programFolders.Select(p => Path.Combine(p, "Microchip", "MPLABX"))
            .Where(Directory.Exists).SelectMany(p => Directory.EnumerateDirectories(p).Append(p))
            .OrderByDescending(p => Version.TryParse(Path.GetFileName(p).TrimStart('v', 'V'), out var v) ? v : new Version(0, 0));
        foreach (var root in roots)
        {
            string? First(params string[] names) => names.Select(n => Path.Combine(root, n.Replace('/', Path.DirectorySeparatorChar))).FirstOrDefault(File.Exists);
            var make = First("gnuBins/GnuWin32/bin/make.exe");
            var generator = First("mplab_platform/bin/prjMakefilesGenerator.bat", "mplab_ide/bin/prjMakefilesGenerator.bat");
            var ipe = First("mplab_platform/mplab_ipe/ipecmd.exe", "mplab_platform/mplab_ipe/ipecmd.jar");
            var ide = First("mplab_platform/bin/mplab_ide64.exe", "mplab_ide/bin/mplab_ide64.exe", "mplab_platform/bin/mplab_ide.exe");
            var javaRoot = Path.Combine(root, "sysjava");
            var java = Directory.Exists(javaRoot) ? Directory.EnumerateFiles(javaRoot, "java.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (make is not null || ipe is not null || ide is not null) return new MplabTools(root, make, generator, ipe, ide, java);
        }

        return null;
    }

    /// <summary>Arguments to program and verify with IPE; target power stays external.</summary>
    /// <param name="device">PIC device.</param>
    /// <param name="firmware">Firmware path.</param>
    /// <param name="tool">Explicit Microchip tool id.</param>
    /// <param name="serial">Optional programmer serial number.</param>
    public IReadOnlyList<string> ProgrammingArguments(string device, string firmware, string tool, string? serial)
    {
        device = device.ToUpperInvariant();
        if (device.StartsWith("PIC", StringComparison.Ordinal)) device = device[3..];
        if (device.Length == 0 || !device.All(char.IsAsciiLetterOrDigit) || tool is not ("PK3" or "PK4" or "PK5" or "SNAP" or "ICD4" or "ICD5"))
            throw new BridgeBuildException("Select a PIC part and a supported IPE tool (PK3, PK4, PK5, SNAP, ICD4 or ICD5).");
        if (serial is not null && (serial.Length > 128 || !serial.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new BridgeBuildException("The programmer serial number contains unsupported characters.");
        List<string> arguments = ["-P" + device, "-TP" + tool, "-F" + firmware, "-M", "-Y"];
        if (!string.IsNullOrEmpty(serial)) arguments.Add("-TS" + serial);
        if (Ipe?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) == true) arguments.InsertRange(0, ["-jar", Ipe]);
        return arguments;
    }
}
