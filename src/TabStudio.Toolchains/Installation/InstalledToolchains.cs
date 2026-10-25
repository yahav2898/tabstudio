using System.Text.Json;

namespace TabStudio.Toolchains.Installation;

/// <summary>What the Store installed in a Linux environment, kept next to it as <c>toolchains.json</c>.</summary>
/// <remarks>
/// <c>apk</c>'s own database says which packages are installed; this file adds what only TabStudio knows: which
/// toolchains the user installed (a package installed from the Terminal does not make a toolchain installed),
/// which version of each setup step ran, and the packages the environment came with, which are nobody's.
/// </remarks>
internal sealed class InstalledToolchains
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>The installed toolchains, by id.</summary>
    public Dictionary<string, InstalledToolchain> Toolchains { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The packages the environment had right after setup.</summary>
    public List<string> BasePackages { get; init; } = [];

    /// <summary>Reads the file, or starts empty when there is none or it cannot be read.</summary>
    public static InstalledToolchains Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<InstalledToolchains>(File.ReadAllText(path), Options) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Writes the file, replacing it in one step so a crash never leaves half of it.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Options));
        File.Move(temporary, path, overwrite: true);
    }
}

/// <summary>One toolchain the Store installed.</summary>
internal sealed record InstalledToolchain
{
    /// <summary>The version of its version package when it was installed or last updated.</summary>
    public string? Version { get; init; }

    /// <summary>The setup steps that ran, by name, with their fingerprints.</summary>
    public Dictionary<string, string> Setup { get; init; } = new(StringComparer.Ordinal);

    /// <summary>When it was installed or last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
