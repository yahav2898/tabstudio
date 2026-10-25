using TabStudio.Core.Diagnostics;

namespace TabStudio.Packages.Resolution;

/// <summary>A package in the final dependency graph.</summary>
/// <param name="Match">The selected version and where it comes from.</param>
/// <param name="IsDirect">Whether the project references the package itself.</param>
internal sealed record ResolvedPackage(PackageMatch Match, bool IsDirect);

/// <summary>The outcome of resolving a project's dependency graph.</summary>
/// <param name="Packages">The selected packages, one version per id, ordered by id.</param>
/// <param name="Unresolved">Accepted dependencies no source could satisfy, one per id and range.</param>
/// <param name="Diagnostics">Cycles, conflicts, downgrades and other findings about the graph.</param>
internal sealed record ResolutionResult(
    IReadOnlyList<ResolvedPackage> Packages,
    IReadOnlyList<GraphNode> Unresolved,
    IReadOnlyList<BuildDiagnostic> Diagnostics);
