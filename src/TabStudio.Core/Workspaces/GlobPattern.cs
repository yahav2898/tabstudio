using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Core.Workspaces;

/// <summary>
/// An MSBuild-style file pattern such as <c>**/*.cs</c> or <c>Generated\*.g.cs</c>.
/// </summary>
/// <remarks>
/// <c>**</c> matches any number of folders, <c>*</c> matches within one path segment and <c>?</c> matches one
/// character. Patterns are relative to a base directory and accept either kind of slash.
/// </remarks>
public sealed class GlobPattern
{
    private readonly Regex _regex;

    /// <summary>Creates a pattern relative to <paramref name="baseDirectory"/>.</summary>
    /// <param name="baseDirectory">The directory the pattern is relative to, usually the project directory.</param>
    /// <param name="pattern">The pattern, as written in the project file.</param>
    public GlobPattern(string baseDirectory, string pattern)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(pattern);

        Pattern = pattern;
        var normalized = PathUtilities.NormalizeSeparators(pattern.Trim());
        var rooted = Path.IsPathRooted(normalized) ? normalized : Path.Combine(baseDirectory, normalized);

        // Collapse "..", but keep wildcards intact: GetFullPath leaves '*' and '?' alone on Unix and Android.
        FullPattern = Path.GetFullPath(rooted);
        HasWildcards = FullPattern.IndexOfAny(['*', '?']) >= 0;
        _regex = new Regex(ToRegex(FullPattern), RegexOptions.CultureInvariant | (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None));
    }

    /// <summary>The pattern as written.</summary>
    public string Pattern { get; }

    /// <summary>The pattern resolved to a full path.</summary>
    public string FullPattern { get; }

    /// <summary>Whether the pattern contains wildcards, or names a single file.</summary>
    public bool HasWildcards { get; }

    /// <summary>The fixed directory at the start of the pattern, before the first wildcard.</summary>
    public string BaseDirectory
    {
        get
        {
            var firstWildcard = FullPattern.IndexOfAny(['*', '?']);
            var prefix = firstWildcard < 0 ? FullPattern : FullPattern[..firstWildcard];
            var lastSeparator = prefix.LastIndexOf(Path.DirectorySeparatorChar);
            return lastSeparator <= 0 ? prefix : prefix[..lastSeparator];
        }
    }

    /// <summary>Whether a full file path matches the pattern.</summary>
    /// <param name="fullPath">The file path to test.</param>
    public bool IsMatch(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        return _regex.IsMatch(fullPath);
    }

    private static string ToRegex(string pattern)
    {
        var separator = Regex.Escape(Path.DirectorySeparatorChar.ToString());
        var builder = new StringBuilder("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                // "**/" matches zero or more whole folders; a trailing "**" matches everything below.
                var followedBySeparator = i + 2 < pattern.Length && pattern[i + 2] == Path.DirectorySeparatorChar;
                builder.Append(followedBySeparator ? $"(?:.*{separator})?" : ".*");
                i += followedBySeparator ? 2 : 1;
            }
            else if (c == '*')
            {
                builder.Append($"[^{separator}]*");
            }
            else if (c == '?')
            {
                builder.Append($"[^{separator}]");
            }
            else
            {
                builder.Append(Regex.Escape(c.ToString()));
            }
        }

        return builder.Append('$').ToString();
    }
}
