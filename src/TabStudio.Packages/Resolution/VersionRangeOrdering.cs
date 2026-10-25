using NuGet.Versioning;

namespace TabStudio.Packages.Resolution;

/// <summary>
/// Compares version ranges by the lowest version they ask for, which is how NuGet decides whether a nearer
/// dependency eclipses a farther one or downgrades it.
/// </summary>
internal static class VersionRangeOrdering
{
    /// <summary>
    /// Whether the nearer range asks for at least as much as the farther one. Floating ranges count as their highest
    /// possible match, and as greater than the same range without floating.
    /// </summary>
    /// <param name="near">The range of the dependency nearer to the project.</param>
    /// <param name="far">The range of the dependency farther from the project.</param>
    public static bool IsGreaterThanOrEqualTo(VersionRange near, VersionRange far)
    {
        if (!near.HasLowerBound)
        {
            return true;
        }

        if (!far.HasLowerBound)
        {
            return false;
        }

        var comparison = VersionComparer.Version.Compare(Floor(near), Floor(far));
        if (comparison != 0)
        {
            return comparison > 0;
        }

        if (near.IsFloating != far.IsFloating)
        {
            return near.IsFloating;
        }

        return VersionComparer.Default.Compare(near.MinVersion, far.MinVersion) >= 0;
    }

    /// <summary>The version a range is compared by: its lower bound, raised to the top of a floating range.</summary>
    private static NuGetVersion? Floor(VersionRange range)
    {
        var minimum = range.MinVersion;
        if (minimum is null || range.Float is not { } floating)
        {
            return minimum;
        }

        const int Max = int.MaxValue;
        return floating.FloatBehavior switch
        {
            NuGetVersionFloatBehavior.Major or NuGetVersionFloatBehavior.PrereleaseMajor or NuGetVersionFloatBehavior.AbsoluteLatest =>
                new NuGetVersion(Max, Max, Max),
            NuGetVersionFloatBehavior.Minor or NuGetVersionFloatBehavior.PrereleaseMinor =>
                new NuGetVersion(minimum.Major, Max, Max),
            NuGetVersionFloatBehavior.Patch or NuGetVersionFloatBehavior.PrereleasePatch =>
                new NuGetVersion(minimum.Major, minimum.Minor, Max),
            NuGetVersionFloatBehavior.Revision or NuGetVersionFloatBehavior.PrereleaseRevision =>
                new NuGetVersion(minimum.Major, minimum.Minor, minimum.Patch, Max),
            _ => minimum,
        };
    }
}
