using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Projects;

/// <summary>Names a Cargo package's run after its crate, as the Output panel names a .NET program after its project.</summary>
public static partial class RunPlanner
{
    /// <summary>
    /// The <c>name</c> in a <c>Cargo.toml</c>'s <c>[package]</c> table, such as <c>hello</c>; <see langword="null"/>
    /// for a virtual workspace, a manifest without a name, or one that cannot be read.
    /// </summary>
    /// <param name="manifest">The <c>Cargo.toml</c> file.</param>
    public static string? CargoPackageName(string manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        try
        {
            var inPackage = false;
            foreach (var raw in File.ReadLines(manifest))
            {
                var line = raw.Trim();
                if (line.StartsWith('['))
                {
                    inPackage = TableHeader().Match(line) is { Success: true } header && header.Groups["name"].Value == "package";
                    continue;
                }

                if (inPackage && PackageName().Match(line) is { Success: true } name && name.Groups["value"].Value.Trim() is { Length: > 0 } value)
                {
                    return value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cargo reports an unreadable manifest itself when it runs.
        }

        return null;
    }

    /// <summary>What a Cargo entry runs, for the Output panel: the crate's name, else the manifest's path.</summary>
    private static string CargoEntryPoint(EntryFile entry) =>
        string.Equals(Path.GetFileName(entry.FullPath), "Cargo.toml", StringComparison.Ordinal) && CargoPackageName(entry.FullPath) is { } crate
            ? crate
            : entry.RelativePath;

    [GeneratedRegex(@"^\[\s*(?<name>[A-Za-z0-9_.-]+)\s*\]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex TableHeader();

    [GeneratedRegex("""^name\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PackageName();
}
