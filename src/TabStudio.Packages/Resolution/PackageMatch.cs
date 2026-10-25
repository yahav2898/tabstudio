using NuGet.Versioning;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Resolution;

/// <summary>
/// The package version a dependency resolved to, with the dependencies it has for the target framework.
/// </summary>
/// <param name="Id">The package id, as the package spells it.</param>
/// <param name="Version">The selected version. For an unresolved dependency, the lower bound of its range.</param>
/// <param name="Dependencies">The package's dependencies for the target framework, without those the framework provides.</param>
/// <param name="Feed">The feed to download it from, or <see langword="null"/> when it is already installed (or unresolved).</param>
/// <param name="IsUnresolved">Whether no source has a version that satisfies the dependency.</param>
internal sealed record PackageMatch(
    string Id,
    NuGetVersion Version,
    IReadOnlyList<PackageDependency> Dependencies,
    IPackageFeed? Feed,
    bool IsUnresolved = false)
{
    /// <summary>A dependency that no source can satisfy.</summary>
    public static PackageMatch Unresolved(string id, VersionRange range) =>
        new(id, range.MinVersion ?? new NuGetVersion(0, 0, 0), [], null, IsUnresolved: true);
}
