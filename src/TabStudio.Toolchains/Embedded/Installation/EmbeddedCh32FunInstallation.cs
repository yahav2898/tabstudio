using System.Collections.ObjectModel;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>The CH32V003 starter uses unmodified pinned upstream startup, linker and peripheral declarations.</summary>
public static class EmbeddedCh32FunInstallation
{
    /// <summary>Native Alpine source-and-compiler SDK Store entry, owning CH32fun projects and their C language server.</summary>
    public const string ToolchainId = "embedded-ch32fun";
    /// <summary>Immutable upstream source revision used by the template and installation proof.</summary>
    public const string Revision = "a800cbd1f1ca5e250c73da83c669f0965d2052e5";
    /// <summary>Actual directory produced by extracting the pinned GitHub source archive.</summary>
    public const string SourceDirectory = "/opt/tabstudio/ch32fun/ch32fun-" + Revision;

    /// <summary>Canonical project files, shared with native x64/ARM64 build verification.</summary>
    public static IReadOnlyDictionary<string, string> ProjectFiles { get; } = ReadFiles();

    private static ReadOnlyDictionary<string, string> ReadFiles()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "Makefile", "firmware.c", "funconfig.h" })
        {
            using var stream = typeof(EmbeddedCh32FunInstallation).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded.Ch32Fun." + name)
                ?? throw new InvalidOperationException("CH32fun template resource is missing: " + name);
            using var reader = new StreamReader(stream);
            files.Add(name, reader.ReadToEnd().Replace("@CH32FUN@", SourceDirectory + "/ch32fun", StringComparison.Ordinal));
        }
        files.Add(".tabstudio-ch32fun", "CH32V003F4P6\n");
        files.Add(".gitignore", "firmware.elf\nfirmware.bin\nfirmware.hex\nfirmware_ext.bin\nfirmware.map\nfirmware.lst\n*.su\n.tabstudio/\n");
        files.Add(".clangd", "CompileFlags:\n  Add: [--target=riscv32-unknown-elf, -DCH32V003, -I., -I" + SourceDirectory + "/ch32fun, -I" + SourceDirectory + "/extralibs]\n");
        return new ReadOnlyDictionary<string, string>(files);
    }
}
