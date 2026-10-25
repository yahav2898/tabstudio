using NuGet.Frameworks;
using NuGet.Versioning;

namespace TabStudio.Packages.Manifests;

/// <summary>A dependency on another package, as a project or a <c>.nuspec</c> declares it.</summary>
/// <param name="Id">The id of the package depended on.</param>
/// <param name="Range">The versions that satisfy the dependency.</param>
internal sealed record PackageDependency(string Id, VersionRange Range);

/// <summary>The dependencies a package has when it is used from one target framework.</summary>
/// <param name="TargetFramework">The framework of the group, or <see cref="NuGetFramework.AnyFramework"/> for every framework.</param>
/// <param name="Dependencies">The dependencies in the group.</param>
internal sealed record PackageDependencyGroup(NuGetFramework TargetFramework, IReadOnlyList<PackageDependency> Dependencies);

/// <summary>What TabStudio reads from a package's <c>.nuspec</c>: its identity and its dependencies.</summary>
/// <param name="Id">The package id, as the package spells it.</param>
/// <param name="Version">The package version.</param>
/// <param name="DependencyGroups">The dependency groups, one per target framework.</param>
internal sealed record PackageManifest(string Id, NuGetVersion Version, IReadOnlyList<PackageDependencyGroup> DependencyGroups);
