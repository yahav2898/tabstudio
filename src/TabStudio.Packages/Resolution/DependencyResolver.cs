using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Assets;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Resolution;

/// <summary>
/// Resolves a project's package references to one version of every package in its dependency graph, with the
/// rules NuGet uses for <c>PackageReference</c> projects.
/// </summary>
/// <remarks>
/// <para>The rules, in NuGet's words (https://learn.microsoft.com/nuget/concepts/dependency-resolution):</para>
/// <list type="bullet">
///   <item><b>Lowest applicable version.</b> A range resolves to the lowest version that satisfies it (the highest
///   for a floating range). See <see cref="PackageFinder"/>.</item>
///   <item><b>Direct dependency wins, and nearest wins.</b> While walking, a dependency is left out when a nearer
///   node on its path (a sibling of it or of one of its ancestors) names the same package; when the nearer one asks
///   for less, the farther one is kept just long enough to report a downgrade (NU1605).</item>
///   <item><b>Cousin dependencies.</b> Different versions reached through different paths settle on the lowest
///   version that satisfies them all; when no version can, that is a conflict (NU1107).</item>
/// </list>
/// <para>The walk is in <see cref="ResolveAsync"/>; the analysis that follows is in <see cref="GraphAnalyzer"/>.</para>
/// </remarks>
internal sealed class DependencyResolver
{
    private readonly PackageFinder _finder;

    /// <summary>Creates a resolver.</summary>
    /// <param name="finder">Resolves each dependency to a package version.</param>
    public DependencyResolver(PackageFinder finder)
    {
        _finder = finder;
    }

    private enum EdgeKind
    {
        Acceptable,
        Eclipsed,
        PotentiallyDowngraded,
        Cycle,
    }

    /// <summary>Walks and analyzes the dependency graph of a project.</summary>
    /// <param name="projectName">The project, which is the root of the graph.</param>
    /// <param name="dependencies">
    /// The project's package references and project references. A referenced project is a node like a package, so
    /// its own references sit one level further from the project, as in NuGet.
    /// </param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public Task<ResolutionResult> ResolveAsync(string projectName, IReadOnlyList<PackageDependency> dependencies, CancellationToken cancellationToken) =>
        ResolveAsync(projectName, dependencies, [], cancellationToken);

    /// <summary>Walks and analyzes the dependency graph of a project, with central versions pinning transitive packages.</summary>
    /// <param name="projectName">The project, which is the root of the graph.</param>
    /// <param name="dependencies">The project's package references and project references.</param>
    /// <param name="pins">
    /// Central versions of packages the project does not reference (<c>CentralPackageTransitivePinningEnabled</c>): a
    /// package among them that the graph reaches is resolved as if the project referenced it at that version, as NuGet
    /// pins it, so the graph is walked again with it.
    /// </param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public async Task<ResolutionResult> ResolveAsync(string projectName, IReadOnlyList<PackageDependency> dependencies, IReadOnlyList<PackageDependency> pins, CancellationToken cancellationToken)
    {
        var root = await WalkRootAsync(projectName, dependencies, cancellationToken).ConfigureAwait(false);
        if (pins.Count > 0)
        {
            var reached = GraphAnalyzer.BreadthFirst(root).Where(n => !n.IsRoot).Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pinned = pins.Where(p => reached.Contains(p.Id)).ToList();
            if (pinned.Count > 0)
            {
                root = await WalkRootAsync(projectName, [.. dependencies, .. pinned], cancellationToken).ConfigureAwait(false);
            }
        }

        var cycles = new List<GraphNode>();
        var downgrades = GraphAnalyzer.RemoveCyclesAndDowngrades(root, cycles);
        var accepted = GraphAnalyzer.ResolveCousins(root);
        var conflicts = GraphAnalyzer.FindConflicts(root, accepted);

        var acceptedNodes = GraphAnalyzer.BreadthFirst(root)
            .Where(n => !n.IsRoot && n.Disposition == Disposition.Accepted)
            .ToList();

        var diagnostics = new List<BuildDiagnostic>();
        diagnostics.AddRange(cycles.Select(node => PackageDiagnostics.Error(
            PackageDiagnosticIds.DependencyCycle,
            $"A package depends on itself, so the cycle was left out:\n  {node.DescribePath()}",
            projectName)));
        diagnostics.AddRange(DescribeConflicts(conflicts, projectName));
        diagnostics.AddRange(GraphAnalyzer.RelevantDowngrades(downgrades).Select(d => DescribeDowngrade(d, projectName)));
        diagnostics.AddRange(DescribeApproximateMatches(acceptedNodes, projectName));
        diagnostics.AddRange(DescribeVersionsAboveUpperBounds(acceptedNodes, accepted, conflicts, projectName));

        var unresolved = acceptedNodes
            .Where(n => n.ResolvedMatch.IsUnresolved)
            .DistinctBy(n => (NuGetNames.Lower(n.Id), n.Range.ToNormalizedString()))
            .ToList();

        var packages = acceptedNodes
            .Where(n => n.ResolvedMatch is { IsUnresolved: false, IsProject: false })
            .GroupBy(n => n.ResolvedMatch.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ResolvedPackage(g.First().ResolvedMatch, g.Any(n => n.Parent is { IsRoot: true }), g.Any(ReachedThroughPackagesOnly))
            {
                // NuGet unites what every path to a package lets through.
                Assets = g.Aggregate(AssetFlags.None, (assets, node) => assets | node.PathAssets),
                Depth = g.Min(Depth),
            })
            .OrderBy(p => p.Match.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResolutionResult(packages, unresolved, diagnostics);
    }

    private async Task<GraphNode> WalkRootAsync(string projectName, IReadOnlyList<PackageDependency> dependencies, CancellationToken cancellationToken)
    {
        var root = new GraphNode(projectName, VersionRange.All, parent: null)
        {
            Match = new PackageMatch(projectName, new NuGetVersion(0, 0, 0), dependencies, Feed: null),
        };
        await WalkAsync(root, cancellationToken).ConfigureAwait(false);
        return root;
    }

    /// <summary>
    /// Builds the graph below a resolved node: classifies each of its dependencies, starts the lookups of the ones
    /// to follow at the same time, then walks into them one by one.
    /// </summary>
    private async Task WalkAsync(GraphNode node, CancellationToken cancellationToken)
    {
        var lookups = new List<(GraphNode Child, Task<PackageMatch> Lookup)>();
        foreach (var dependency in node.ResolvedMatch.Dependencies)
        {
            var kind = Classify(node, dependency);
            if (kind == EdgeKind.Eclipsed)
            {
                continue;
            }

            var child = new GraphNode(dependency.Id, dependency.Range, node) { Excluded = dependency.Excluded };
            node.Children.Add(child);
            switch (kind)
            {
                case EdgeKind.Cycle:
                    child.Disposition = Disposition.Cycle;
                    break;
                case EdgeKind.PotentiallyDowngraded:
                    child.Disposition = Disposition.PotentiallyDowngraded;
                    break;
                default:
                    lookups.Add((child, _finder.FindAsync(dependency, cancellationToken)));
                    break;
            }
        }

        foreach (var (child, lookup) in lookups)
        {
            child.Match = await lookup.ConfigureAwait(false);
            await WalkAsync(child, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Decides whether to follow a dependency of <paramref name="node"/>, by looking at what the nodes on its path
    /// declare: the same package nearer to the project eclipses it, or downgrades it when the nearer range is lower.
    /// </summary>
    private static EdgeKind Classify(GraphNode node, PackageDependency dependency)
    {
        if (NuGetNames.SameId(dependency.Id, node.Id) || NuGetNames.SameId(dependency.Id, node.ResolvedMatch.Id))
        {
            return EdgeKind.Cycle;
        }

        var child = node;
        for (var ancestor = node.Parent; ancestor is not null; child = ancestor, ancestor = ancestor.Parent)
        {
            var declared = ancestor.ResolvedMatch;
            if (NuGetNames.SameId(dependency.Id, ancestor.Id) || NuGetNames.SameId(dependency.Id, declared.Id))
            {
                return EdgeKind.Cycle;
            }

            foreach (var sibling in declared.Dependencies)
            {
                if (NuGetNames.SameId(sibling.Id, dependency.Id) && !NuGetNames.SameId(sibling.Id, child.Id))
                {
                    return VersionRangeOrdering.IsGreaterThanOrEqualTo(sibling.Range, dependency.Range)
                        ? EdgeKind.Eclipsed
                        : EdgeKind.PotentiallyDowngraded;
                }
            }
        }

        return EdgeKind.Acceptable;
    }

    private static IEnumerable<BuildDiagnostic> DescribeConflicts(List<VersionConflict> conflicts, string projectName) =>
        conflicts
            .GroupBy(c => c.Selected.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var conflict = g.First();
                var selected = conflict.Selected.ResolvedMatch;
                return PackageDiagnostics.Error(
                    PackageDiagnosticIds.VersionConflict,
                    $"Version conflict detected for {selected.Id}. Reference {selected.Id} {selected.Version.ToNormalizedString()} directly from {projectName} to resolve it.\n" +
                    $"  {conflict.Selected.DescribePath()}\n  {conflict.Conflicting.DescribePath()}",
                    projectName);
            });

    private static BuildDiagnostic DescribeDowngrade(Downgrade downgrade, string projectName)
    {
        var from = downgrade.DowngradedFrom.Range.MinVersion?.ToNormalizedString() ?? "0.0.0";
        var to = (downgrade.DowngradedBy.Match?.Version ?? downgrade.DowngradedBy.Range.MinVersion)?.ToNormalizedString() ?? "0.0.0";
        return PackageDiagnostics.Warning(
            PackageDiagnosticIds.Downgrade,
            $"Detected package downgrade: {downgrade.DowngradedFrom.Id} from {from} to {to}. Reference the package directly from the project to select a different version.\n" +
            $"  {downgrade.DowngradedFrom.DescribePath()}\n  {downgrade.DowngradedBy.DescribePath()}",
            projectName);
    }

    /// <summary>NU1603: the exact lower bound of a range does not exist, so a higher version was used.</summary>
    private static IEnumerable<BuildDiagnostic> DescribeApproximateMatches(List<GraphNode> acceptedNodes, string projectName) =>
        acceptedNodes
            .Where(n => n.ResolvedMatch is { IsUnresolved: false } match &&
                        !n.Range.IsFloating && n.Range.HasLowerBound && n.Range.IsMinInclusive &&
                        VersionComparer.Default.Compare(n.Range.MinVersion, match.Version) != 0)
            .DistinctBy(n => (NuGetNames.Lower(n.Id), n.Range.ToNormalizedString()))
            .Select(n => PackageDiagnostics.Warning(
                PackageDiagnosticIds.ApproximateMatch,
                $"{n.Parent?.DescribeVersion()} depends on {n.DescribeRange()} but {n.Id} {n.Range.MinVersion?.ToNormalizedString()} was not found. " +
                $"An approximate best match of {n.ResolvedMatch.Id} {n.ResolvedMatch.Version.ToNormalizedString()} was resolved.",
                projectName));

    /// <summary>NU1608: a nearer reference selected a version above what a package declares it supports.</summary>
    private static IEnumerable<BuildDiagnostic> DescribeVersionsAboveUpperBounds(
        List<GraphNode> acceptedNodes,
        Dictionary<string, GraphNode> accepted,
        List<VersionConflict> conflicts,
        string projectName)
    {
        var conflicted = conflicts.Select(c => c.Selected.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in acceptedNodes.Where(n => !n.ResolvedMatch.IsUnresolved).DistinctBy(n => NuGetNames.Lower(n.ResolvedMatch.Id)))
        {
            foreach (var dependency in node.ResolvedMatch.Dependencies)
            {
                if (dependency.Range.MaxVersion is not { } upper ||
                    conflicted.Contains(dependency.Id) ||
                    !accepted.TryGetValue(dependency.Id, out var selected) ||
                    selected.Match is not { IsUnresolved: false } match)
                {
                    continue;
                }

                var comparison = VersionComparer.VersionRelease.Compare(match.Version, upper);
                if (dependency.Range.IsMaxInclusive ? comparison > 0 : comparison >= 0)
                {
                    yield return PackageDiagnostics.Warning(
                        PackageDiagnosticIds.VersionAboveUpperBound,
                        $"Detected package version outside of dependency constraint: {node.DescribeVersion()} requires {dependency.Id} {dependency.Range.PrettyPrint()} but version {match.Id} {match.Version.ToNormalizedString()} was resolved.",
                        projectName);
                }
            }
        }
    }

    /// <summary>How many dependencies lie between the project and a node: 1 for a package the project references.</summary>
    private static int Depth(GraphNode node)
    {
        var depth = 0;
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>Whether no project lies between the project being restored and a node.</summary>
    private static bool ReachedThroughPackagesOnly(GraphNode node)
    {
        for (var parent = node.Parent; parent is { IsRoot: false }; parent = parent.Parent)
        {
            if (parent.Match is { IsProject: true })
            {
                return false;
            }
        }

        return true;
    }
}
