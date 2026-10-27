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
/// Analyzers and source generators come from <c>analyzers/dotnet/cs/</c> and <c>analyzers/dotnet/</c>, and from the
/// highest <c>analyzers/dotnet/roslyn{version}/cs/</c> folder the in-app compiler supports, as the .NET SDK picks them.
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
        var analyzers = compilerVersion is null ? [] : SelectAnalyzers(paths, compilerVersion);
        return new PackageAssets(Assemblies(compile), Assemblies(runtime), compatible, usedFallback, supported)
        {
            Analyzers = analyzers,
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
    /// Selects the C# analyzer assemblies of a package: those for any Roslyn version, plus the folder of the highest
    /// Roslyn version that is not newer than the compiler.
    /// </summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="compilerVersion">The version of Roslyn that runs the analyzers.</param>
    public static IReadOnlyList<string> SelectAnalyzers(IEnumerable<string> relativePaths, Version compilerVersion)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(compilerVersion);
        var unversioned = new List<string>();
        var versioned = new Dictionary<Version, List<string>>();
        foreach (var path in relativePaths)
        {
            var parts = path.Split('/');
            if (parts.Length < 3 || !parts[0].Equals("analyzers", StringComparison.OrdinalIgnoreCase)
                || !parts[1].Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                || !Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // analyzers/dotnet/X.dll, analyzers/dotnet/cs/X.dll, analyzers/dotnet/roslyn4.4/cs/X.dll
            var rest = parts[2..];
            Version? roslyn = null;
            if (rest.Length > 1 && rest[0].StartsWith("roslyn", StringComparison.OrdinalIgnoreCase))
            {
                if (!Version.TryParse(rest[0]["roslyn".Length..], out roslyn) || roslyn > compilerVersion)
                {
                    continue;
                }

                rest = rest[1..];
            }

            var isCSharp = rest.Length == 1 || (rest.Length == 2 && rest[0].Equals("cs", StringComparison.OrdinalIgnoreCase));
            if (!isCSharp)
            {
                continue;
            }

            if (roslyn is null)
            {
                unversioned.Add(path);
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

        List<string> best = versioned.Count == 0 ? [] : versioned[versioned.Keys.Max()!];
        return [.. unversioned.Concat(best).Order(StringComparer.Ordinal)];
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
