using NuGet.Frameworks;

namespace TabStudio.Packages.Assets;

/// <summary>
/// Chooses which assemblies of a package a project compiles against and runs with, following NuGet's conventions.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Assemblies are the <c>.dll</c> and <c>.exe</c> files directly inside a framework folder, such as
///   <c>lib/netstandard2.0/Newtonsoft.Json.dll</c>. Subfolders (satellite resources) and other files are ignored.</item>
///   <item>Compile assets come from the nearest <c>ref/{tfm}/</c> folder, or from <c>lib/{tfm}/</c> when no
///   <c>ref/</c> folder is compatible. Runtime assets come from the nearest <c>lib/{tfm}/</c>.</item>
///   <item>A folder that only holds the placeholder <c>_._</c> is compatible but contributes nothing, which is how
///   packages say "the framework already has this".</item>
///   <item>When nothing is compatible with the target, the <c>AssetTargetFallback</c> frameworks are tried in order.</item>
///   <item>Given the device's RIDs (see <see cref="RuntimeGraph.Expand"/>), runtime assets come from a
///   <c>runtimes/{rid}/lib/{tfm}/</c> folder when one fits, instead of <c>lib/{tfm}/</c>, as NuGet's
///   <c>runtimeTargets</c> and the .NET host choose them: the nearest framework first, then the nearest RID. This is
///   how a package such as Microsoft.Data.SqlClient replaces its <c>lib/</c> "not supported on this platform" stub with
///   its <c>runtimes/unix/lib/</c> implementation. A matching folder with only <c>_._</c> means "nothing for this RID".
///   Compile assets never come from <c>runtimes/</c>, as in the SDK.</item>
///   <item>Native libraries come from the <c>runtimes/{rid}/native/</c> folder of the nearest RID that has one.</item>
/// </list>
/// Analyzers and source generators come from <c>analyzers/</c> as the .NET SDK picks them: C# and language-neutral
/// assemblies, and of those in <c>roslyn{version}</c> folders (<c>analyzers/dotnet/roslyn4.12/cs/</c> or
/// <c>analyzers/roslyn5.0/dotnet/cs/</c>), the highest version the in-app compiler supports.
/// MSBuild files and <c>runtimes/{rid}/nativeassets/{tfm}/</c> folders (UWP and WebAssembly only) are not used.
/// </remarks>
internal static class AssetSelector
{
    private static readonly string[] AssemblyExtensions = [".dll", ".exe"];

    /// <summary>Selects the assets of a package from the list of its files.</summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    /// <param name="compilerVersion">The version of Roslyn that runs the analyzers; <see langword="null"/> for no analyzers.</param>
    /// <param name="runtimeIdentifiers">
    /// The RIDs the device can use, nearest first, as <see cref="RuntimeGraph.Expand"/> lists them; <see langword="null"/>
    /// or empty to ignore <c>runtimes/</c>.
    /// </param>
    public static PackageAssets Select(
        IEnumerable<string> relativePaths,
        NuGetFramework target,
        IReadOnlyList<NuGetFramework> fallback,
        Version? compilerVersion = null,
        IReadOnlyList<string>? runtimeIdentifiers = null)
    {
        var paths = relativePaths.ToList();
        var lib = GroupByFramework(paths, "lib");
        var reference = GroupByFramework(paths, "ref");
        var rids = runtimeIdentifiers ?? [];
        var runtimeSpecific = GroupRuntimeSpecific(paths, rids);

        var (compile, runtime) = Choose(reference, lib, runtimeSpecific, rids, target);
        NuGetFramework? usedFallback = null;
        if (compile is null && runtime is null)
        {
            foreach (var framework in fallback)
            {
                (compile, runtime) = Choose(reference, lib, runtimeSpecific, rids, framework);
                if (compile is not null || runtime is not null)
                {
                    usedFallback = framework;
                    break;
                }
            }
        }

        var supported = lib.Keys.Concat(reference.Keys).Distinct().ToList();
        var compatible = supported.Count == 0 || compile is not null || runtime is not null;
        Version? needsCompiler = null;
        var analyzers = compilerVersion is null ? [] : SelectAnalyzers(paths, compilerVersion, out needsCompiler);
        return new PackageAssets(Assemblies(compile), Assemblies(runtime), compatible, usedFallback, supported)
        {
            Analyzers = analyzers,
            AnalyzersNeedCompiler = needsCompiler,
            NativeLibraries = rids.Count == 0 ? [] : SelectNativeLibraries(paths, rids),
        };
    }

    /// <summary>
    /// Selects the native libraries of a package: every file under <c>runtimes/{rid}/native/</c> for the nearest RID
    /// that has such a folder, as NuGet's <c>runtimeTargets</c> and the .NET host pick them.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="runtimeIdentifiers">The RIDs the device can use, nearest first.</param>
    /// <returns>The files, or none when no RID has native files or the nearest folder holds only <c>_._</c>.</returns>
    public static IReadOnlyList<string> SelectNativeLibraries(IEnumerable<string> relativePaths, IReadOnlyList<string> runtimeIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(runtimeIdentifiers);
        var byRid = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in relativePaths)
        {
            var parts = path.Split('/');
            if (parts.Length < 4 || !parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase)
                || !parts[2].Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!byRid.TryGetValue(parts[1], out var files))
            {
                files = [];
                byRid[parts[1]] = files;
            }

            files.Add(path);
        }

        foreach (var rid in runtimeIdentifiers)
        {
            if (byRid.TryGetValue(rid, out var files))
            {
                return [.. files.Where(f => !IsPlaceholder(f)).Order(StringComparer.Ordinal)];
            }
        }

        return [];
    }

    /// <summary>
    /// Selects the C# analyzer assemblies of a package as the .NET SDK does (<c>ResolvePackageAssets</c>): every
    /// <c>.dll</c> under <c>analyzers/</c> except satellite resources and Visual Basic ones (a <c>/vb/</c> folder without
    /// a <c>/cs/</c> one). A folder named <c>roslyn{version}</c> anywhere in the path ties a file to a compiler version:
    /// of those, only the files of the highest version that is not newer than the compiler are kept, across the whole
    /// package, so <c>analyzers/dotnet/roslyn4.12/cs/</c> (CommunityToolkit.Mvvm) and
    /// <c>analyzers/roslyn5.0/dotnet/cs/</c> (Riok.Mapperly) are both understood.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="compilerVersion">The version of Roslyn that runs the analyzers.</param>
    public static IReadOnlyList<string> SelectAnalyzers(IEnumerable<string> relativePaths, Version compilerVersion) =>
        SelectAnalyzers(relativePaths, compilerVersion, out _);

    /// <summary>
    /// Selects the C# analyzer assemblies of a package (see <see cref="SelectAnalyzers(IEnumerable{string}, Version)"/>),
    /// and says when a package has analyzers only for compilers newer than this one, whose generated code is then
    /// missing.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="compilerVersion">The version of Roslyn that runs the analyzers.</param>
    /// <param name="onlyNewer">
    /// The lowest Roslyn version the package's versioned analyzers need, when every one of them needs a newer compiler
    /// than <paramref name="compilerVersion"/>; otherwise <see langword="null"/>.
    /// </param>
    public static IReadOnlyList<string> SelectAnalyzers(IEnumerable<string> relativePaths, Version compilerVersion, out Version? onlyNewer)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(compilerVersion);
        var unversioned = new List<string>();
        var versioned = new Dictionary<Version, List<string>>();
        Version? lowestNewer = null;
        foreach (var path in relativePaths)
        {
            if (!IsCSharpAnalyzer(path))
            {
                continue;
            }

            var roslyn = RoslynFolderVersion(path);
            if (roslyn is null)
            {
                unversioned.Add(path);
            }
            else if (roslyn > compilerVersion)
            {
                lowestNewer = lowestNewer is null || roslyn < lowestNewer ? roslyn : lowestNewer;
            }
            else
            {
                if (!versioned.TryGetValue(roslyn, out var files))
                {
                    files = [];
                    versioned[roslyn] = files;
                }

                files.Add(path);
            }
        }

        onlyNewer = versioned.Count == 0 ? lowestNewer : null;
        List<string> best = versioned.Count == 0 ? [] : versioned[versioned.Keys.Max()!];
        return [.. unversioned.Concat(best).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Selects the MSBuild files a project imports from a package, as NuGet writes them into <c>nuget.g.props</c> and
    /// <c>nuget.g.targets</c> (measured with SDK 10.0.113): the package's <c>{id}.props</c> and <c>{id}.targets</c> in
    /// <c>build/</c> when the project gets its <c>build</c> assets, and in <c>buildTransitive/</c> when it gets its
    /// <c>buildTransitive</c> ones, each from the nearest framework folder or the folder itself; a file of the same name
    /// in <c>buildTransitive/</c> replaces the one in <c>build/</c>. Props come before targets.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="packageId">The package id, which the files are named after.</param>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    /// <param name="assets">The assets the project gets from the package.</param>
    public static IReadOnlyList<string> SelectBuildFiles(
        IEnumerable<string> relativePaths,
        string packageId,
        NuGetFramework target,
        IReadOnlyList<NuGetFramework> fallback,
        AssetFlags assets)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        var paths = relativePaths.ToList();
        var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (assets.HasFlag(AssetFlags.Build))
        {
            foreach (var file in BuildFolder(paths, "build", packageId, target, fallback))
            {
                chosen[file[(file.LastIndexOf('/') + 1)..]] = file;
            }
        }

        if (assets.HasFlag(AssetFlags.BuildTransitive))
        {
            foreach (var file in BuildFolder(paths, "buildTransitive", packageId, target, fallback))
            {
                chosen[file[(file.LastIndexOf('/') + 1)..]] = file;
            }
        }

        return [.. chosen.Values
            .OrderBy(file => file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(file => file, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The C# sources of a package's <c>contentFiles/</c> a project compiles, as NuGet adds them to its <c>Compile</c>
    /// items: the <c>.cs</c> files under <c>contentFiles/cs/</c> or <c>contentFiles/any/</c>, from the nearest framework
    /// folder, or from <c>any/</c> when none is compatible, when the project gets the package's content files. A
    /// source-only package such as <c>Wcwidth.Sources</c> is nothing else.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    /// <param name="assets">The assets the project gets from the package.</param>
    public static IReadOnlyList<string> SelectContentSources(
        IEnumerable<string> relativePaths,
        NuGetFramework target,
        IReadOnlyList<NuGetFramework> fallback,
        AssetFlags assets)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        if (!assets.HasFlag(AssetFlags.ContentFiles))
        {
            return [];
        }

        var any = new List<string>();
        var byFramework = new Dictionary<NuGetFramework, List<string>>();
        foreach (var path in relativePaths)
        {
            var parts = path.Split('/');
            if (parts.Length < 4
                || !parts[0].Equals("contentFiles", StringComparison.OrdinalIgnoreCase)
                || !(parts[1].Equals("cs", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("any", StringComparison.OrdinalIgnoreCase))
                || !parts[^1].EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (parts[2].Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                any.Add(path);
                continue;
            }

            if (FrameworkSelection.ParseFolder(parts[2]) is not { } framework)
            {
                continue;
            }

            if (!byFramework.TryGetValue(framework, out var files))
            {
                files = [];
                byFramework[framework] = files;
            }

            files.Add(path);
        }

        var chosen = byFramework.Count > 0 && FrameworkSelection.GetNearest(target, fallback, byFramework.Keys, out _) is { } nearest
            ? byFramework[nearest]
            : any;
        return [.. chosen.Order(StringComparer.Ordinal)];
    }

    /// <summary>The <c>{id}.props</c> and <c>{id}.targets</c> of one build folder, for the nearest framework.</summary>
    private static List<string> BuildFolder(List<string> paths, string folder, string packageId, NuGetFramework target, IReadOnlyList<NuGetFramework> fallback)
    {
        var any = new List<string>();
        var anyExists = false;
        var byFramework = new Dictionary<NuGetFramework, List<string>>();
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            if (parts.Length is < 2 or > 3 || !parts[0].Equals(folder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = parts[^1];
            var isPlaceholder = name == "_._";
            var isImported = name.Equals(packageId + ".props", StringComparison.OrdinalIgnoreCase)
                || name.Equals(packageId + ".targets", StringComparison.OrdinalIgnoreCase);
            if (!isImported && !isPlaceholder)
            {
                continue;
            }

            if (parts.Length == 2)
            {
                anyExists = true;
                if (isImported)
                {
                    any.Add(path);
                }

                continue;
            }

            if (FrameworkSelection.ParseFolder(parts[1]) is not { } framework)
            {
                continue;
            }

            if (!byFramework.TryGetValue(framework, out var files))
            {
                files = [];
                byFramework[framework] = files;
            }

            if (isImported)
            {
                files.Add(path);
            }
        }

        if (byFramework.Count > 0 && FrameworkSelection.GetNearest(target, fallback, byFramework.Keys, out _) is { } nearest)
        {
            return byFramework[nearest];
        }

        return anyExists ? any : [];
    }

    /// <summary>
    /// Whether a package file is an analyzer a C# project loads, by the SDK's rule: a <c>.dll</c> under
    /// <c>analyzers/</c>, not a <c>.resources.dll</c>, and in a <c>/cs/</c> folder or in none for another language.
    /// </summary>
    private static bool IsCSharpAnalyzer(string path)
    {
        if (!path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var folders = path.Split('/')[..^1];
        return folders.Any(f => f.Equals("cs", StringComparison.OrdinalIgnoreCase))
            || !folders.Any(f => f.Equals("vb", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The version in a <c>roslyn{version}</c> folder of a path, such as 4.12 for <c>analyzers/dotnet/roslyn4.12/cs/X.dll</c>.</summary>
    private static Version? RoslynFolderVersion(string path)
    {
        foreach (var folder in path.Split('/')[..^1])
        {
            if (folder.Length > "roslyn".Length
                && folder.StartsWith("roslyn", StringComparison.OrdinalIgnoreCase)
                && Version.TryParse(folder["roslyn".Length..], out var version))
            {
                return version;
            }
        }

        return null;
    }

    private static (List<string>? Compile, List<string>? Runtime) Choose(
        Dictionary<NuGetFramework, List<string>> reference,
        Dictionary<NuGetFramework, List<string>> lib,
        Dictionary<NuGetFramework, Dictionary<string, List<string>>> runtimeSpecific,
        IReadOnlyList<string> runtimeIdentifiers,
        NuGetFramework framework)
    {
        IReadOnlyList<NuGetFramework> none = [];
        var libFramework = FrameworkSelection.GetNearest(framework, none, lib.Keys, out _);
        var refFramework = FrameworkSelection.GetNearest(framework, none, reference.Keys, out _);

        var libRuntime = libFramework is null ? null : lib[libFramework];
        var compile = refFramework is null ? libRuntime : reference[refFramework];

        // NuGet tries the runtime-specific groups first, comparing the framework and then the RID, and uses lib/ only
        // when none fits. The whole group replaces lib/, as a deps.json's runtimeTargets do.
        var runtime = libRuntime;
        var ridFramework = FrameworkSelection.GetNearest(framework, none, runtimeSpecific.Keys, out _);
        if (ridFramework is not null)
        {
            var byRid = runtimeSpecific[ridFramework];
            if (runtimeIdentifiers.FirstOrDefault(byRid.ContainsKey) is { } rid)
            {
                runtime = byRid[rid];
            }
        }

        return (compile, runtime);
    }

    /// <summary>
    /// Groups the files directly inside <c>runtimes/{rid}/lib/{tfm}/</c> folders by framework, then by RID, for the
    /// RIDs the device can use.
    /// </summary>
    private static Dictionary<NuGetFramework, Dictionary<string, List<string>>> GroupRuntimeSpecific(List<string> paths, IReadOnlyList<string> runtimeIdentifiers)
    {
        var usable = runtimeIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<NuGetFramework, Dictionary<string, List<string>>>();
        foreach (var path in paths)
        {
            // runtimes/unix/lib/net8.0/Microsoft.Data.SqlClient.dll
            var parts = path.Split('/');
            if (parts.Length < 5 || !parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase)
                || !parts[2].Equals("lib", StringComparison.OrdinalIgnoreCase) || !usable.Contains(parts[1]))
            {
                continue;
            }

            if (FrameworkSelection.ParseFolder(parts[3]) is not { } framework)
            {
                continue;
            }

            if (!groups.TryGetValue(framework, out var byRid))
            {
                byRid = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                groups[framework] = byRid;
            }

            if (!byRid.TryGetValue(parts[1], out var files))
            {
                files = [];
                byRid[parts[1]] = files;
            }

            if (parts.Length == 5)
            {
                files.Add(path);
            }
        }

        return groups;
    }

    private static bool IsPlaceholder(string path) => Path.GetFileName(path) == "_._";

    /// <summary>Groups the files of a top-level folder (<c>lib</c> or <c>ref</c>) by the framework folder they are in.</summary>
    /// <returns>Every framework folder, with the files directly inside it. A folder with only subfolders has an empty list.</returns>
    private static Dictionary<NuGetFramework, List<string>> GroupByFramework(List<string> paths, string folder)
    {
        var groups = new Dictionary<NuGetFramework, List<string>>();
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            if (parts.Length < 3 || !parts[0].Equals(folder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (FrameworkSelection.ParseFolder(parts[1]) is not { } framework)
            {
                continue;
            }

            if (!groups.TryGetValue(framework, out var files))
            {
                files = [];
                groups[framework] = files;
            }

            if (parts.Length == 3)
            {
                files.Add(path);
            }
        }

        return groups;
    }

    private static List<string> Assemblies(List<string>? files) =>
        files is null
            ? []
            : files.Where(f => AssemblyExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToList();
}
