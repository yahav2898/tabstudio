namespace TabStudio.Toolchains.Alpine;

/// <summary>What installing or updating packages takes: the packages to download, and names nothing provides.</summary>
/// <param name="Packages">The packages to download and install, new ones and newer versions of installed ones.</param>
/// <param name="Missing">Dependencies no repository satisfies.</param>
public sealed record ApkPlan(IReadOnlyList<ApkPackage> Packages, IReadOnlyList<string> Missing)
{
    /// <summary>The bytes to download.</summary>
    public long DownloadSize => Packages.Sum(p => p.Size);

    /// <summary>The bytes the packages take once installed.</summary>
    public long InstalledSize => Packages.Sum(p => p.InstalledSize);
}

/// <summary>
/// Works out which packages installing or updating a set of packages downloads, from the repositories' index and
/// what is installed, as <c>apk</c> will resolve them.
/// </summary>
/// <remarks>
/// <para>
/// The plan lets TabStudio download every package itself, with progress, resuming and verification, before
/// <c>apk</c> installs them from a local folder without the network. Dependencies are followed through package
/// names and through what packages provide (<c>so:</c>, <c>cmd:</c>, <c>pc:</c>); among several providers, the
/// highest provider priority and then the newest version wins, and an installed provider always does. Packages
/// that install themselves with others (<c>i:</c>) are added when everything they wait for will be there.
/// </para>
/// <para>
/// <c>apk</c> stays the authority on what gets installed. If it ever wants a package the plan did not download,
/// the installer lets <c>apk</c> fetch the rest itself.
/// </para>
/// </remarks>
public static class ApkResolver
{
    /// <summary>Plans installing packages.</summary>
    /// <param name="index">The repositories' packages.</param>
    /// <param name="installed">The installed packages.</param>
    /// <param name="names">The packages to install.</param>
    public static ApkPlan PlanInstall(ApkIndex index, IReadOnlyList<ApkPackage> installed, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(names);
        return Resolve(index, installed, names.Select(n => new ApkDependency(n, null, null, false)), upgrade: new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Plans updating packages: every installed package they depend on, and they themselves, that has a newer
    /// version in the index, with whatever new dependencies the new versions bring.
    /// </summary>
    /// <param name="index">The repositories' packages.</param>
    /// <param name="installed">The installed packages.</param>
    /// <param name="names">The packages to update, with what they depend on.</param>
    public static ApkPlan PlanUpgrade(ApkIndex index, IReadOnlyList<ApkPackage> installed, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(names);
        var upgradable = Closure(installed, names)
            .Where(p => index.Find(p.Name) is { } newest && ApkVersion.Compare(newest.Version, p.Version) > 0)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        return Resolve(index, installed, upgradable.Select(n => new ApkDependency(n, null, null, false)), upgradable);
    }

    /// <summary>
    /// The installed packages a set of packages consists of: themselves and, recursively, the installed packages
    /// that satisfy their dependencies.
    /// </summary>
    /// <param name="installed">The installed packages.</param>
    /// <param name="names">The packages to start from.</param>
    public static IReadOnlyList<ApkPackage> Closure(IReadOnlyList<ApkPackage> installed, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(names);
        var byName = installed.GroupBy(p => p.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var result = new Dictionary<string, ApkPackage>(StringComparer.Ordinal);
        var queue = new Queue<string>(names);
        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (result.ContainsKey(name) || !byName.TryGetValue(name, out var package))
            {
                continue;
            }

            result[name] = package;
            foreach (var dependency in package.Depends.Where(d => !d.IsConflict))
            {
                if (installed.FirstOrDefault(p => p.Satisfies(dependency)) is { } provider)
                {
                    queue.Enqueue(provider.Name);
                }
            }
        }

        return [.. result.Values];
    }

    private static ApkPlan Resolve(ApkIndex index, IReadOnlyList<ApkPackage> installed, IEnumerable<ApkDependency> roots, HashSet<string> upgrade)
    {
        var kept = installed.Where(p => !upgrade.Contains(p.Name)).ToList();
        var selected = new Dictionary<string, ApkPackage>(StringComparer.Ordinal);
        var missing = new List<string>();
        var queue = new Queue<ApkDependency>(roots);

        bool IsSatisfied(ApkDependency dependency) =>
            selected.Values.Any(p => p.Satisfies(dependency)) || kept.Any(p => p.Satisfies(dependency));

        void Drain()
        {
            while (queue.Count > 0)
            {
                var dependency = queue.Dequeue();
                if (dependency.IsConflict || IsSatisfied(dependency))
                {
                    continue;
                }

                var candidate = index.Candidates(dependency).FirstOrDefault(p => !selected.ContainsKey(p.Name));
                if (candidate is null)
                {
                    if (!missing.Contains(dependency.ToString()))
                    {
                        missing.Add(dependency.ToString());
                    }

                    continue;
                }

                selected[candidate.Name] = candidate;
                foreach (var next in candidate.Depends)
                {
                    queue.Enqueue(next);
                }
            }
        }

        Drain();

        // Packages such as documentation or shell completions that install themselves once all they wait for is there.
        var added = true;
        while (added)
        {
            added = false;
            foreach (var automatic in index.InstallIfPackages())
            {
                if (selected.ContainsKey(automatic.Name) || kept.Any(p => p.Name == automatic.Name))
                {
                    continue;
                }

                if (automatic.InstallIf.All(IsSatisfied) && automatic.InstallIf.Any(d => selected.Values.Any(p => p.Satisfies(d))))
                {
                    selected[automatic.Name] = automatic;
                    foreach (var next in automatic.Depends)
                    {
                        queue.Enqueue(next);
                    }

                    Drain();
                    added = true;
                }
            }
        }

        return new ApkPlan([.. selected.Values], missing);
    }
}
