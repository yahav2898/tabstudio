using NuGet.Frameworks;
using NuGet.Versioning;

namespace TabStudio.Packages.Assets;

/// <summary>
/// Knows which packages and assemblies the target framework already provides, so they are not restored twice.
/// </summary>
/// <remarks>
/// <para>
/// Like the .NET SDK's package pruning, a transitive dependency on a package named after a framework assembly
/// (<c>System.Runtime</c>, <c>System.Text.Json</c>, …) is dropped from the graph when its version is not newer
/// than the framework, together with everything below it. The same goes for the metapackages that only exist to
/// describe the framework, such as <c>NETStandard.Library</c>. This keeps old <c>netstandard1.x</c> packages from
/// pulling in dozens of <c>System.*</c> packages.
/// </para>
/// <para>
/// "Not newer" compares major versions: a dependency is pruned when its lower bound's major version is at most the
/// framework's (4.3.0 or 10.0.1 for .NET 10, but not 11.0.0).
/// </para>
/// </remarks>
internal sealed class FrameworkPackages
{
    private static readonly HashSet<string> Metapackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "NETStandard.Library",
        "Microsoft.NETCore.App",
        "Microsoft.NETCore.Platforms",
        "Microsoft.NETCore.Targets",
    };

    private readonly HashSet<string> _assemblyNames;
    private readonly int _frameworkMajorVersion;

    /// <summary>Creates the set for one target framework.</summary>
    /// <param name="assemblyNames">The simple names of the framework's assemblies.</param>
    /// <param name="framework">The target framework.</param>
    public FrameworkPackages(IEnumerable<string> assemblyNames, NuGetFramework framework)
    {
        _assemblyNames = new HashSet<string>(assemblyNames, StringComparer.OrdinalIgnoreCase);
        _frameworkMajorVersion = framework.Version.Major;
    }

    /// <summary>Whether the framework has an assembly with this simple name.</summary>
    public bool IsFrameworkAssembly(string simpleName) => _assemblyNames.Contains(simpleName);

    /// <summary>Whether a dependency names a package the framework provides, so it is pruned rather than restored.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="range">The requested versions.</param>
    public bool Provides(string id, VersionRange range)
    {
        if (!Metapackages.Contains(id) && !_assemblyNames.Contains(id))
        {
            return false;
        }

        return range.MinVersion is not { } minimum || minimum.Major <= _frameworkMajorVersion;
    }
}
