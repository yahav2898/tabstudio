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
/// </list>
/// Runtime-specific assets (<c>runtimes/{rid}/</c>), native libraries, analyzers and MSBuild files are not used.
/// </remarks>
internal static class AssetSelector
{
    private static readonly string[] AssemblyExtensions = [".dll", ".exe"];

    /// <summary>Selects the assets of a package from the list of its files.</summary>
    /// <param name="relativePaths">Every file in the package, relative to its root, with forward slashes.</param>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    public static PackageAssets Select(IEnumerable<string> relativePaths, NuGetFramework target, IReadOnlyList<NuGetFramework> fallback)
    {
        var paths = relativePaths.ToList();
        var lib = GroupByFramework(paths, "lib");
        var reference = GroupByFramework(paths, "ref");

        var (compile, runtime) = Choose(reference, lib, target);
        NuGetFramework? usedFallback = null;
        if (compile is null && runtime is null)
        {
            foreach (var framework in fallback)
            {
                (compile, runtime) = Choose(reference, lib, framework);
                if (compile is not null || runtime is not null)
                {
                    usedFallback = framework;
                    break;
                }
            }
        }

        var supported = lib.Keys.Concat(reference.Keys).Distinct().ToList();
        var compatible = supported.Count == 0 || compile is not null || runtime is not null;
        return new PackageAssets(Assemblies(compile), Assemblies(runtime), compatible, usedFallback, supported);
    }

    private static (List<string>? Compile, List<string>? Runtime) Choose(
        Dictionary<NuGetFramework, List<string>> reference,
        Dictionary<NuGetFramework, List<string>> lib,
        NuGetFramework framework)
    {
        IReadOnlyList<NuGetFramework> none = [];
        var libFramework = FrameworkSelection.GetNearest(framework, none, lib.Keys, out _);
        var refFramework = FrameworkSelection.GetNearest(framework, none, reference.Keys, out _);

        var runtime = libFramework is null ? null : lib[libFramework];
        var compile = refFramework is null ? runtime : reference[refFramework];
        return (compile, runtime);
    }

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
