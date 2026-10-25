using System.Globalization;

namespace TabStudio.Extensions.Manifests;

/// <summary>
/// Compares extension versions as semantic versions (<c>1.10.0</c> is newer than <c>1.9.2</c>; <c>2.0.0-beta</c> is older
/// than <c>2.0.0</c>), the way VS Code decides that an update is available.
/// </summary>
public static class ExtensionVersions
{
    /// <summary>Compares two versions.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    /// <returns>Less than zero when <paramref name="left"/> is older, zero when they are the same, more when it is newer.</returns>
    public static int Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);

        var leftParts = leftCore.Split('.');
        var rightParts = rightCore.Split('.');
        for (var i = 0; i < Math.Max(leftParts.Length, rightParts.Length); i++)
        {
            var compared = CompareIdentifier(i < leftParts.Length ? leftParts[i] : "0", i < rightParts.Length ? rightParts[i] : "0");
            if (compared != 0)
            {
                return compared;
            }
        }

        // A release is newer than its pre-releases.
        if (leftPre is null || rightPre is null)
        {
            return leftPre is null ? (rightPre is null ? 0 : 1) : -1;
        }

        var leftIds = leftPre.Split('.');
        var rightIds = rightPre.Split('.');
        for (var i = 0; i < Math.Min(leftIds.Length, rightIds.Length); i++)
        {
            var compared = CompareIdentifier(leftIds[i], rightIds[i]);
            if (compared != 0)
            {
                return compared;
            }
        }

        return leftIds.Length.CompareTo(rightIds.Length);
    }

    /// <summary>Whether a version is newer than another.</summary>
    /// <param name="candidate">The version that may be newer, such as the registry's latest.</param>
    /// <param name="current">The version to compare with, such as the installed one.</param>
    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    private static (string Core, string? PreRelease) Split(string version)
    {
        var text = version.Trim().TrimStart('v', 'V');
        var build = text.IndexOf('+', StringComparison.Ordinal);
        if (build >= 0)
        {
            text = text[..build];
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);
        return dash >= 0 ? (text[..dash], text[(dash + 1)..]) : (text, null);
    }

    /// <summary>Numbers compare as numbers and before words; words compare by character.</summary>
    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumber = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => leftNumber.CompareTo(rightNumber),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }
}
