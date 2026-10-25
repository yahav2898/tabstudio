using System.Collections.Concurrent;
using NuGet.Frameworks;
using NuGet.Versioning;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Assets;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Resolution;

/// <summary>
/// Finds the package version a dependency resolves to, with its dependencies, for one restore.
/// </summary>
/// <remarks>
/// <para>
/// The version is NuGet's "best match" for the range (<see cref="VersionRange.FindBestMatch"/>): the lowest version
/// that satisfies it, or the highest one for a floating range such as <c>1.*</c>, never a prerelease unless the range
/// mentions one. Every feed is asked, at the same time, and their versions are combined.
/// </para>
/// <para>
/// The global packages folder is consulted first: a range whose lower bound is installed (the usual case for
/// <c>Version="13.0.3"</c>) resolves without the network. When the sources cannot be reached, the best installed
/// version is used instead, so a device that restored a project once can restore it again offline.
/// </para>
/// <para>Lookups are cached for the lifetime of the finder, and source failures are collected, one per source.</para>
/// </remarks>
internal sealed class PackageFinder
{
    private readonly IReadOnlyList<IPackageFeed> _feeds;
    private readonly GlobalPackagesFolder _packages;
    private readonly NuGetFramework _framework;
    private readonly IReadOnlyList<NuGetFramework> _fallback;
    private readonly FrameworkPackages _frameworkPackages;
    private readonly IProgress<RestoreProgress>? _progress;
    private readonly ConcurrentDictionary<string, Lazy<Task<AvailableVersions>>> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<PackageMatch>>> _matches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PackageSourceException> _failures = new(StringComparer.OrdinalIgnoreCase);
    private int _lookups;

    /// <summary>Creates a finder for one restore.</summary>
    /// <param name="feeds">The feeds to search, in source order.</param>
    /// <param name="packages">The global packages folder.</param>
    /// <param name="framework">The target framework, which selects each package's dependency group.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    /// <param name="frameworkPackages">Packages the framework provides, which are pruned from every package's dependencies.</param>
    /// <param name="progress">Receives a report for every lookup.</param>
    public PackageFinder(
        IReadOnlyList<IPackageFeed> feeds,
        GlobalPackagesFolder packages,
        NuGetFramework framework,
        IReadOnlyList<NuGetFramework> fallback,
        FrameworkPackages frameworkPackages,
        IProgress<RestoreProgress>? progress)
    {
        _feeds = feeds;
        _packages = packages;
        _framework = framework;
        _fallback = fallback;
        _frameworkPackages = frameworkPackages;
        _progress = progress;
    }

    /// <summary>The source failures seen so far, at most one per source.</summary>
    public IReadOnlyList<PackageSourceException> Failures => _failures.Values.ToList();

    /// <summary>Resolves a dependency to a package version.</summary>
    /// <param name="dependency">The dependency.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The match, which is <see cref="PackageMatch.IsUnresolved"/> when no version fits.</returns>
    public Task<PackageMatch> FindAsync(PackageDependency dependency, CancellationToken cancellationToken)
    {
        var key = $"{dependency.Id}|{dependency.Range.ToNormalizedString()}";
        return _matches.GetOrAdd(key, _ => new Lazy<Task<PackageMatch>>(() => FindCoreAsync(dependency, cancellationToken))).Value;
    }

    /// <summary>Returns the versions every feed has of a package, asking each feed once per restore.</summary>
    /// <param name="id">The package id.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public Task<AvailableVersions> GetAvailableVersionsAsync(string id, CancellationToken cancellationToken) =>
        _versions.GetOrAdd(id, key => new Lazy<Task<AvailableVersions>>(() => LoadAvailableVersionsAsync(key, cancellationToken))).Value;

    /// <summary>Explains why a dependency could not be resolved, with NuGet's NU1101, NU1102 or NU1103.</summary>
    /// <param name="node">The unresolved dependency.</param>
    /// <param name="projectName">The project being restored.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The diagnostic, or <see langword="null"/> when a source failure explains it instead: the source could not be
    /// read, or failed while the package was downloaded.
    /// </returns>
    public async Task<BuildDiagnostic?> DescribeUnresolvedAsync(GraphNode node, string projectName, CancellationToken cancellationToken)
    {
        var available = await GetAvailableVersionsAsync(node.Id, cancellationToken).ConfigureAwait(false);
        if (node.Range.FindBestMatch(available.All) is not null || (available.AnySourceFailed && available.All.Count == 0))
        {
            return null;
        }

        var sources = string.Join(", ", _feeds.Select(f => f.Source.Name));
        var path = node.Parent is { IsRoot: false } ? $" ({node.DescribePath()})" : string.Empty;
        if (available.All.Count == 0)
        {
            return PackageDiagnostics.Error(
                PackageDiagnosticIds.PackageNotFound,
                $"Unable to find package {node.Id}. No packages exist with this id in {sources}{path}.",
                projectName);
        }

        var range = node.Range.PrettyPrint();
        var prerelease = available.All.Where(v => v.IsPrerelease && node.Range.Satisfies(v)).Order().ToList();
        if (!AllowsPrerelease(node.Range) && prerelease.Count > 0)
        {
            return PackageDiagnostics.Error(
                PackageDiagnosticIds.OnlyPrereleaseVersions,
                $"Unable to find a stable package {node.Id} with version {range}{path}. Only prerelease versions match, up to {prerelease[^1].ToNormalizedString()}; reference one of them by its full version to use it.",
                projectName);
        }

        var ordered = available.All.Order().ToList();
        return PackageDiagnostics.Error(
            PackageDiagnosticIds.VersionNotFound,
            $"Unable to find package {node.Id} with version {range}{path}. Found {ordered.Count} {(ordered.Count == 1 ? "version" : "versions")} in {sources}, from {ordered[0].ToNormalizedString()} to {ordered[^1].ToNormalizedString()}.",
            projectName);
    }

    private async Task<PackageMatch> FindCoreAsync(PackageDependency dependency, CancellationToken cancellationToken)
    {
        var (id, range) = (dependency.Id, dependency.Range);
        var count = Interlocked.Increment(ref _lookups);
        _progress?.Report(new RestoreProgress(RestoreStage.Resolving, $"Resolving {id} {range.PrettyPrint()}", count, 0));

        // An installed exact lower bound is the answer NuGet would pick, and needs no network.
        if (!range.IsFloating && range.HasLowerBound && range.IsMinInclusive && range.MinVersion is { } lowest &&
            FromInstalled(id, lowest) is { } installed)
        {
            return installed;
        }

        var available = await GetAvailableVersionsAsync(id, cancellationToken).ConfigureAwait(false);
        var best = range.FindBestMatch(available.All);
        if (best is null)
        {
            // Offline, or the sources lost the package: what is already installed still works.
            if (available.AnySourceFailed &&
                range.FindBestMatch(_packages.GetInstalledVersions(id)) is { } offline &&
                FromInstalled(id, offline) is { } offlineMatch)
            {
                return offlineMatch;
            }

            return PackageMatch.Unresolved(id, range);
        }

        if (FromInstalled(id, best) is { } local)
        {
            return local;
        }

        if (available.FeedFor(best) is not { } feed)
        {
            return PackageMatch.Unresolved(id, range);
        }

        try
        {
            var manifest = await feed.GetManifestAsync(id, best, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                RecordFailure(new PackageSourceException(feed.Source, $"{feed.Source.Name} lists {id} {best.ToNormalizedString()} but has no manifest for it.", isUnreachable: false));
                return PackageMatch.Unresolved(id, range);
            }

            return CreateMatch(manifest, feed);
        }
        catch (PackageSourceException ex)
        {
            RecordFailure(ex);
            return PackageMatch.Unresolved(id, range);
        }
    }

    private async Task<AvailableVersions> LoadAvailableVersionsAsync(string id, CancellationToken cancellationToken)
    {
        var lookups = _feeds.Select(async feed =>
        {
            try
            {
                return (Versions: new FeedVersions(feed, await feed.GetVersionsAsync(id, cancellationToken).ConfigureAwait(false)), Failed: false);
            }
            catch (PackageSourceException ex)
            {
                RecordFailure(ex);
                return (Versions: new FeedVersions(feed, []), Failed: true);
            }
        });

        var results = await Task.WhenAll(lookups).ConfigureAwait(false);
        return new AvailableVersions(results.Select(r => r.Versions).ToList(), results.Any(r => r.Failed));
    }

    private PackageMatch? FromInstalled(string id, NuGetVersion version) =>
        _packages.ReadManifest(id, version) is { } manifest ? CreateMatch(manifest, feed: null) : null;

    /// <summary>Selects the dependencies a package has for the target framework, without those the framework provides.</summary>
    private PackageMatch CreateMatch(PackageManifest manifest, IPackageFeed? feed)
    {
        var group = FrameworkSelection.SelectDependencyGroup(manifest.DependencyGroups, _framework, _fallback);
        var dependencies = (group?.Dependencies ?? [])
            .Where(d => !_frameworkPackages.Provides(d.Id, d.Range))
            .ToList();
        return new PackageMatch(manifest.Id, manifest.Version, dependencies, feed);
    }

    /// <summary>Whether a range lets NuGet pick prerelease versions: one of its bounds is a prerelease, or it floats over them.</summary>
    private static bool AllowsPrerelease(VersionRange range) =>
        range.MinVersion?.IsPrerelease == true ||
        range.MaxVersion?.IsPrerelease == true ||
        range.Float?.FloatBehavior is NuGetVersionFloatBehavior.Prerelease or NuGetVersionFloatBehavior.PrereleaseRevision or
            NuGetVersionFloatBehavior.PrereleasePatch or NuGetVersionFloatBehavior.PrereleaseMinor or
            NuGetVersionFloatBehavior.PrereleaseMajor or NuGetVersionFloatBehavior.AbsoluteLatest;

    private void RecordFailure(PackageSourceException failure) =>
        _failures.TryAdd(failure.PackageSource?.Name ?? string.Empty, failure);
}
