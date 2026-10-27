using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Assets;

namespace TabStudio.Packages.Resolution;

/// <summary>A package in the final dependency graph.</summary>
/// <param name="Match">The selected version and where it comes from.</param>
/// <param name="IsDirect">Whether the project references the package itself.</param>
/// <param name="FlowsAnalyzers">
/// Whether the project reaches the package through packages only, not through another project: NuGet's default
/// <c>PrivateAssets</c> keeps a referenced project's analyzers and source generators to that project.
/// </param>
internal sealed record ResolvedPackage(PackageMatch Match, bool IsDirect, bool FlowsAnalyzers = true)
{
    /// <summary>The assets the project gets from the package, over every path that leads to it.</summary>
    public AssetFlags Assets { get; init; } = AssetFlags.All;

    /// <summary>How far the package is from the project along its shortest path: 1 for a direct reference.</summary>
    public int Depth { get; init; } = 1;
}

/// <summary>The outcome of resolving a project's dependency graph.</summary>
/// <param name="Packages">The selected packages, one version per id, ordered by id.</param>
/// <param name="Unresolved">Accepted dependencies no source could satisfy, one per id and range.</param>
/// <param name="Diagnostics">Cycles, conflicts, downgrades and other findings about the graph.</param>
internal sealed record ResolutionResult(
    IReadOnlyList<ResolvedPackage> Packages,
    IReadOnlyList<GraphNode> Unresolved,
    IReadOnlyList<BuildDiagnostic> Diagnostics);
