using NuGet.Frameworks;
using NuGet.Versioning;
using TabStudio.Packages.Assets;

namespace TabStudio.Packages.Manifests;

/// <summary>A dependency on another package, as a project or a <c>.nuspec</c> declares it, or on a referenced project.</summary>
/// <param name="Id">The id of the package depended on, or the name of the referenced project.</param>
/// <param name="Range">The versions that satisfy the dependency; any version for a project.</param>
/// <param name="IsProject">Whether this is a <c>ProjectReference</c>, whose own package references flow to the referencing project.</param>
internal sealed record PackageDependency(string Id, VersionRange Range, bool IsProject = false)
{
    /// <summary>
    /// The assets of the dependency that do not reach the dependent, from a <c>.nuspec</c>'s <c>include</c> and
    /// <c>exclude</c> or a reference's <c>IncludeAssets</c>, <c>ExcludeAssets</c> and <c>PrivateAssets</c>.
    /// </summary>
    public AssetFlags Excluded { get; init; }
}

/// <summary>The dependencies a package has when it is used from one target framework.</summary>
/// <param name="TargetFramework">The framework of the group, or <see cref="NuGetFramework.AnyFramework"/> for every framework.</param>
/// <param name="Dependencies">The dependencies in the group.</param>
internal sealed record PackageDependencyGroup(NuGetFramework TargetFramework, IReadOnlyList<PackageDependency> Dependencies);

/// <summary>What TabStudio reads from a package's <c>.nuspec</c>: its identity and its dependencies.</summary>
/// <param name="Id">The package id, as the package spells it.</param>
/// <param name="Version">The package version.</param>
/// <param name="DependencyGroups">The dependency groups, one per target framework.</param>
internal sealed record PackageManifest(string Id, NuGetVersion Version, IReadOnlyList<PackageDependencyGroup> DependencyGroups);

/// <summary>
/// One <c>&lt;files&gt;</c> entry of a manifest's <c>&lt;contentFiles&gt;</c>: globs relative to the <c>contentFiles/</c>
/// folder, separated by semicolons, and the build action of the files they match (<see langword="null"/> to keep the
/// one an earlier entry or NuGet's default, <c>Compile</c>, gives).
/// </summary>
/// <param name="Include">The files the entry applies to.</param>
/// <param name="Exclude">Files it leaves out, or <see langword="null"/>.</param>
/// <param name="BuildAction">Their build action, such as <c>Compile</c> or <c>None</c>, or <see langword="null"/>.</param>
internal sealed record ContentFileEntry(string Include, string? Exclude, string? BuildAction);
