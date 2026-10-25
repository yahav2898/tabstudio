namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// A package as an Alpine index (<c>APKINDEX</c>) or the installed database (<c>lib/apk/db/installed</c>)
/// describes it.
/// </summary>
public sealed record ApkPackage
{
    /// <summary>The package name (<c>P:</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The version (<c>V:</c>), such as <c>3.14.8-r0</c>.</summary>
    public required string Version { get; init; }

    /// <summary>The architecture (<c>A:</c>).</summary>
    public string? Architecture { get; init; }

    /// <summary>The size of the package file in bytes (<c>S:</c>).</summary>
    public long Size { get; init; }

    /// <summary>The size of its files once installed, in bytes (<c>I:</c>).</summary>
    public long InstalledSize { get; init; }

    /// <summary>One line about it (<c>T:</c>).</summary>
    public string? Description { get; init; }

    /// <summary>Its license (<c>L:</c>).</summary>
    public string? License { get; init; }

    /// <summary>
    /// The checksum of the package's control section (<c>C:</c>): <c>Q1</c> and the base64 of its SHA-1.
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>What it needs (<c>D:</c>).</summary>
    public IReadOnlyList<ApkDependency> Depends { get; init; } = [];

    /// <summary>What it provides besides its own name (<c>p:</c>), such as <c>so:libpython3.14.so.1.0=1.0</c>.</summary>
    public IReadOnlyList<ApkDependency> Provides { get; init; } = [];

    /// <summary>The packages whose presence installs it automatically (<c>i:</c>).</summary>
    public IReadOnlyList<ApkDependency> InstallIf { get; init; } = [];

    /// <summary>The priority among packages that provide the same name (<c>k:</c>).</summary>
    public int ProviderPriority { get; init; }

    /// <summary>The repository it comes from, such as <c>main</c>; <see langword="null"/> in the installed database.</summary>
    public string? Repository { get; init; }

    /// <summary>The package file's name in its repository folder, as <c>apk</c> names it.</summary>
    public string FileName => $"{Name}-{Version}.apk";

    /// <summary>Whether the package provides a name, at a version that satisfies a dependency.</summary>
    /// <param name="dependency">The dependency.</param>
    public bool Satisfies(ApkDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (string.Equals(Name, dependency.Name, StringComparison.Ordinal))
        {
            return dependency.AcceptsVersion(Version);
        }

        foreach (var provided in Provides)
        {
            if (string.Equals(provided.Name, dependency.Name, StringComparison.Ordinal))
            {
                // A provided name without a version satisfies only a dependency without a constraint.
                return dependency.Operation is null || dependency.AcceptsVersion(provided.Version);
            }
        }

        return false;
    }
}
