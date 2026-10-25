namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// The packages of one or more Alpine repositories, looked up by name and by what they provide.
/// </summary>
public sealed class ApkIndex
{
    private readonly Dictionary<string, List<ApkPackage>> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ApkPackage>> _byProvided = new(StringComparer.Ordinal);

    /// <summary>Creates an index of packages.</summary>
    /// <param name="packages">The packages, from every repository, in repository order.</param>
    public ApkIndex(IEnumerable<ApkPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        foreach (var package in packages)
        {
            Add(_byName, package.Name, package);
            foreach (var provided in package.Provides)
            {
                Add(_byProvided, provided.Name, package);
            }

            Count++;
        }
    }

    /// <summary>How many packages the index holds.</summary>
    public int Count { get; }

    /// <summary>The newest version of a package, or <see langword="null"/> when no repository has it.</summary>
    /// <param name="name">The package name.</param>
    public ApkPackage? Find(string name) =>
        _byName.TryGetValue(name, out var packages) ? Newest(packages) : null;

    /// <summary>
    /// The packages that satisfy a dependency: the package of that name, or the packages that provide it, best
    /// first (highest provider priority, then newest).
    /// </summary>
    /// <param name="dependency">The dependency.</param>
    public IReadOnlyList<ApkPackage> Candidates(ApkDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        var result = new List<ApkPackage>();
        if (_byName.TryGetValue(dependency.Name, out var named))
        {
            result.AddRange(named.Where(p => dependency.AcceptsVersion(p.Version)));
        }

        if (_byProvided.TryGetValue(dependency.Name, out var providers))
        {
            result.AddRange(providers.Where(p => p.Satisfies(dependency) && !result.Contains(p)));
        }

        return result
            .OrderByDescending(p => string.Equals(p.Name, dependency.Name, StringComparison.Ordinal))
            .ThenByDescending(p => p.ProviderPriority)
            .ThenByDescending(p => p.Version, Comparer<string>.Create(ApkVersion.Compare))
            .ToList();
    }

    /// <summary>The packages that are installed automatically with others (they have <c>i:</c> lines).</summary>
    public IEnumerable<ApkPackage> InstallIfPackages() =>
        _byName.Values.Select(Newest).Where(p => p.InstallIf.Count > 0);

    private static ApkPackage Newest(List<ApkPackage> packages) =>
        packages.Count == 1 ? packages[0] : packages.MaxBy(p => p.Version, Comparer<string>.Create(ApkVersion.Compare))!;

    private static void Add(Dictionary<string, List<ApkPackage>> map, string key, ApkPackage package)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }

        list.Add(package);
    }
}
