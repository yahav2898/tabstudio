using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace TabStudio.Packages;

/// <summary>The lower-case spellings NuGet uses for package ids and versions in URLs and folder names.</summary>
internal static class NuGetNames
{
    /// <summary>Lower-cases an id or a version string, as the V3 flat container and the global packages folder do.</summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "NuGet feeds and package folders are lower case by specification.")]
    public static string Lower(string value) => value.ToLowerInvariant();

    /// <summary>The normalized, lower-case form of a version, such as <c>1.0.0-beta.1</c>.</summary>
    public static string Lower(NuGetVersion version) => Lower(version.ToNormalizedString());

    /// <summary>Whether two package ids are the same. Ids are case-insensitive.</summary>
    public static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether an id is one NuGet accepts: letters, digits and <c>_</c>, joined by <c>.</c> or <c>-</c>, up to 100
    /// characters. An id becomes a folder name, so "../x" or "/data/x" must never reach the packages folder.
    /// </summary>
    public static bool IsValidId([NotNullWhen(true)] string? id) =>
        id is { Length: > 0 and <= 100 } && System.Text.RegularExpressions.Regex.IsMatch(id, @"^\w+([.-]\w+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
