using NuGet.Frameworks;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Assets;

/// <summary>
/// Picks the best of several framework-specific choices for a target framework, as NuGet does: the nearest
/// compatible framework wins, and the <c>AssetTargetFallback</c> frameworks are tried, in order, only when nothing
/// is compatible with the target itself.
/// </summary>
internal static class FrameworkSelection
{
    /// <summary>Returns the nearest framework among <paramref name="candidates"/> that <paramref name="target"/> can use.</summary>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">Frameworks to try, in order, when nothing is compatible with <paramref name="target"/>.</param>
    /// <param name="candidates">The frameworks to choose from.</param>
    /// <param name="usedFallback">The fallback framework that matched, or <see langword="null"/> when the target itself did.</param>
    /// <returns>The chosen framework, or <see langword="null"/> when none is compatible.</returns>
    public static NuGetFramework? GetNearest(
        NuGetFramework target,
        IReadOnlyList<NuGetFramework> fallback,
        IReadOnlyCollection<NuGetFramework> candidates,
        out NuGetFramework? usedFallback)
    {
        usedFallback = null;
        if (candidates.Count == 0)
        {
            return null;
        }

        var reducer = new FrameworkReducer();
        var nearest = reducer.GetNearest(target, candidates);
        if (nearest is not null)
        {
            return nearest;
        }

        foreach (var framework in fallback)
        {
            nearest = reducer.GetNearest(framework, candidates);
            if (nearest is not null)
            {
                usedFallback = framework;
                return nearest;
            }
        }

        return null;
    }

    /// <summary>Selects the dependency group of a package that applies to a target framework.</summary>
    /// <param name="groups">The package's dependency groups.</param>
    /// <param name="target">The project's framework.</param>
    /// <param name="fallback">The <c>AssetTargetFallback</c> frameworks.</param>
    /// <returns>The group, or <see langword="null"/> when no group applies, in which case the package has no dependencies.</returns>
    public static PackageDependencyGroup? SelectDependencyGroup(
        IReadOnlyList<PackageDependencyGroup> groups,
        NuGetFramework target,
        IReadOnlyList<NuGetFramework> fallback)
    {
        var frameworks = groups.Select(g => g.TargetFramework).Distinct().ToList();
        var nearest = GetNearest(target, fallback, frameworks, out _);
        return nearest is null
            ? null
            : groups.FirstOrDefault(g => NuGetFramework.Comparer.Equals(g.TargetFramework, nearest));
    }

    /// <summary>Parses a package folder name such as <c>netstandard2.0</c>, or returns <see langword="null"/> when it is not a framework.</summary>
    public static NuGetFramework? ParseFolder(string folderName)
    {
        try
        {
            var framework = NuGetFramework.ParseFolder(folderName);
            return framework.IsUnsupported ? null : framework;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FrameworkException)
        {
            return null;
        }
    }
}
