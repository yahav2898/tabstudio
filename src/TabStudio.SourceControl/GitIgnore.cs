using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.SourceControl;

/// <summary>
/// The rules of <c>.gitignore</c> files: which untracked files are left out of a commit.
/// </summary>
/// <remarks>
/// Supports the common syntax: comments, blank lines, negation with <c>!</c>, directory-only patterns ending
/// in <c>/</c>, patterns anchored with a slash, and the wildcards <c>*</c>, <c>?</c> and <c>**</c>.
/// Rules from a <c>.gitignore</c> in a subfolder apply below that folder, and the last matching rule wins.
/// </remarks>
public sealed class GitIgnore
{
    private readonly List<Rule> _rules = [];

    /// <summary>Adds the rules of one <c>.gitignore</c> file.</summary>
    /// <param name="content">The file's content.</param>
    /// <param name="folder">The folder the file is in, relative to the repository root, with <c>/</c> separators; empty for the root.</param>
    public void Add(string content, string folder = "")
    {
        ArgumentNullException.ThrowIfNull(content);
        var prefix = folder.Trim('/');
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            line = TrimUnescapedTrailingSpaces(line);
            var negate = line.StartsWith('!');
            if (negate)
            {
                line = line[1..];
            }
            else if (line.StartsWith("\\!", StringComparison.Ordinal) || line.StartsWith("\\#", StringComparison.Ordinal))
            {
                line = line[1..];
            }

            var directoryOnly = line.EndsWith('/');
            line = line.TrimEnd('/');
            if (line.Length == 0)
            {
                continue;
            }

            // A pattern with a slash anywhere but the end is relative to the .gitignore's folder.
            var anchored = line.Contains('/', StringComparison.Ordinal);
            line = line.TrimStart('/');
            var body = ToRegex(line);
            var pattern = anchored
                ? $"^{Regex.Escape(prefix.Length == 0 ? string.Empty : prefix + "/")}{body}$"
                : $"^{(prefix.Length == 0 ? string.Empty : Regex.Escape(prefix + "/") + "(?:.*/)?")}(?:.*/)?{body}$";
            if (!anchored && prefix.Length == 0)
            {
                pattern = $"^(?:.*/)?{body}$";
            }

            _rules.Add(new Rule(new Regex(pattern, RegexOptions.CultureInvariant), negate, directoryOnly));
        }
    }

    /// <summary>Whether a path is ignored.</summary>
    /// <param name="relativePath">The path relative to the repository root, with <c>/</c> separators.</param>
    /// <param name="isDirectory">Whether the path is a folder.</param>
    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var ignored = false;
        foreach (var rule in _rules)
        {
            if (rule.DirectoryOnly && !isDirectory)
            {
                continue;
            }

            if (rule.Pattern.IsMatch(relativePath))
            {
                ignored = !rule.Negate;
            }
        }

        return ignored;
    }

    private static string ToRegex(string glob)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                // "**/" matches any number of folders, "/**" anything below, a lone "**" anything at all.
                var slashAfter = i + 2 < glob.Length && glob[i + 2] == '/';
                builder.Append(slashAfter ? "(?:.*/)?" : ".*");
                i += slashAfter ? 2 : 1;
            }
            else if (c == '*')
            {
                builder.Append("[^/]*");
            }
            else if (c == '?')
            {
                builder.Append("[^/]");
            }
            else if (c == '[')
            {
                var close = glob.IndexOf(']', i + 1);
                if (close > i)
                {
                    var set = glob[(i + 1)..close].Replace("\\", "\\\\", StringComparison.Ordinal);
                    builder.Append('[').Append(set.StartsWith('!') ? "^" + set[1..] : set).Append(']');
                    i = close;
                }
                else
                {
                    builder.Append("\\[");
                }
            }
            else if (c == '\\' && i + 1 < glob.Length)
            {
                builder.Append(Regex.Escape(glob[++i].ToString()));
            }
            else
            {
                builder.Append(Regex.Escape(c.ToString()));
            }
        }

        return builder.ToString();
    }

    private static string TrimUnescapedTrailingSpaces(string line)
    {
        var end = line.Length;
        while (end > 0 && line[end - 1] == ' ' && !(end > 1 && line[end - 2] == '\\'))
        {
            end--;
        }

        return line[..end];
    }

    private sealed record Rule(Regex Pattern, bool Negate, bool DirectoryOnly);
}
