namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// What <c>apk</c> has installed in a root file system: its database (<c>lib/apk/db/installed</c>) and the
/// packages installed on request rather than as dependencies (<c>etc/apk/world</c>).
/// </summary>
/// <param name="Packages">The installed packages.</param>
/// <param name="World">The names installed on request.</param>
public sealed record ApkInstalledDatabase(IReadOnlyList<ApkPackage> Packages, IReadOnlySet<string> World)
{
    /// <summary>The database's path, relative to the root file system.</summary>
    public const string DatabasePath = "lib/apk/db/installed";

    /// <summary>The world file's path, relative to the root file system.</summary>
    public const string WorldPath = "etc/apk/world";

    /// <summary>An empty database.</summary>
    public static ApkInstalledDatabase Empty { get; } = new([], new HashSet<string>());

    /// <summary>Reads the database of a root file system; an environment without one has nothing installed.</summary>
    /// <param name="rootfs">The root file system.</param>
    public static ApkInstalledDatabase Read(string rootfs)
    {
        ArgumentNullException.ThrowIfNull(rootfs);
        var database = Path.Combine(rootfs, DatabasePath);
        var world = Path.Combine(rootfs, WorldPath);
        var packages = File.Exists(database) ? ReadPackages(database) : [];
        var names = File.Exists(world)
            ? File.ReadAllText(world).Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(n => ApkDependency.Parse(n).Name)
                .ToHashSet(StringComparer.Ordinal)
            : [];
        return new ApkInstalledDatabase(packages, names);
    }

    /// <summary>The installed package of a name, or <see langword="null"/>.</summary>
    /// <param name="name">The package name.</param>
    public ApkPackage? Find(string name) => Packages.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    /// <summary>Whether every one of the names is installed.</summary>
    /// <param name="names">Package names.</param>
    public bool HasAll(IEnumerable<string> names) => names.All(n => Find(n) is not null);

    private static List<ApkPackage> ReadPackages(string path)
    {
        using var reader = new StreamReader(path);
        return ApkDatabaseText.Read(reader);
    }
}
