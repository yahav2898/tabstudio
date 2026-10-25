using NuGet.Versioning;
using TabStudio.Packages.Feeds;

namespace TabStudio.Packages.Resolution;

/// <summary>The versions of a package one feed has.</summary>
/// <param name="Feed">The feed.</param>
/// <param name="Versions">Its versions of the package; empty when it has none or could not be read.</param>
internal sealed record FeedVersions(IPackageFeed Feed, IReadOnlyList<NuGetVersion> Versions);

/// <summary>The versions of a package across every feed of a restore.</summary>
/// <param name="PerFeed">The versions each feed has, in source order.</param>
/// <param name="AnySourceFailed">Whether at least one feed could not be read, so the list may be incomplete.</param>
internal sealed record AvailableVersions(IReadOnlyList<FeedVersions> PerFeed, bool AnySourceFailed)
{
    /// <summary>Every version any feed has, without duplicates.</summary>
    public IReadOnlyList<NuGetVersion> All { get; } = PerFeed.SelectMany(f => f.Versions).Distinct().ToList();

    /// <summary>The first feed, in source order, that has a version.</summary>
    public IPackageFeed? FeedFor(NuGetVersion version) =>
        PerFeed.FirstOrDefault(f => f.Versions.Contains(version))?.Feed;
}
