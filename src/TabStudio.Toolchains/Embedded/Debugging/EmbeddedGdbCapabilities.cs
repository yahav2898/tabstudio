using System.Text.Json;

namespace TabStudio.Toolchains.Embedded.Debugging;

/// <summary>Checks real GDB Python/DAP and architecture capabilities before opening a target.</summary>
public static class EmbeddedGdbCapabilities
{
    /// <summary>Marker separating capability JSON from normal GDB output.</summary>
    public const string Marker = "TABSTUDIO_GDB_CAPABILITIES:";

    /// <summary>A batch-only Python query; finding the DAP module does not import it or start its reader thread.</summary>
    public const string Query = "python import gdb,importlib.util,json; print('" + Marker + "'+json.dumps({'dap':importlib.util.find_spec('gdb.dap') is not None,'architectures':gdb.architecture_names()}))";

    /// <summary>Requires the DAP module and the firmware ELF's architecture, rather than trusting a binary's name.</summary>
    /// <param name="output">The GDB batch query's output.</param>
    /// <param name="elfMachine">Firmware ELF e_machine.</param>
    public static void RequireSupport(string output, ushort elfMachine)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Length > 65536) throw new InvalidDataException("The GDB capability response exceeds 64 KiB.");
        var line = output.Split('\n').FirstOrDefault(text => text.StartsWith(Marker, StringComparison.Ordinal));
        if (line is null) throw new NotSupportedException("The installed GDB cannot report Python/DAP capabilities. Install multi-target GDB 14+ with its gdb.dap module.");
        using var document = JsonDocument.Parse(line[Marker.Length..], new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("GDB did not return a capability object.");
        if (!root.TryGetProperty("dap", out var dap) || dap.ValueKind != JsonValueKind.True)
            throw new NotSupportedException("The installed GDB has no Python DAP module. Install a GDB 14+ build with gdb.dap.");
        if (!root.TryGetProperty("architectures", out var architectures) || architectures.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GDB did not report its compiled-in architectures.");
        var family = elfMachine switch
        {
            4 => "m68k", 8 => "mips", 40 => "arm", 83 => "avr", 94 => "xtensa", 105 => "msp430", 183 => "aarch64", 243 => "riscv",
            _ => throw new NotSupportedException("This ELF architecture needs a vendor debugger; it is not mapped to this OpenOCD/GDB route (e_machine=" + elfMachine.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")."),
        };
        var supported = architectures.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String
            && value.GetString() is { } name && (name.Equals(family, StringComparison.OrdinalIgnoreCase) || name.StartsWith(family + ":", StringComparison.OrdinalIgnoreCase)));
        if (!supported) throw new NotSupportedException("The installed GDB does not include " + family + " target support. A multiarch label alone does not guarantee every architecture; install a matching DAP-enabled vendor GDB or use the Windows Bridge.");
    }
}
