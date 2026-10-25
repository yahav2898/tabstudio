using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Extensions.Contributions;

/// <summary>
/// Matches file names against the glob patterns of <c>filenamePatterns</c>: <c>*</c>, <c>?</c>, <c>[abc]</c> and
/// <c>{a,b}</c>, ignoring case. A pattern with folders (<c>**/.vscode/*.json</c>) is matched against the end of the path.
/// </summary>
internal sealed class FileNameGlob
{
    private readonly Regex _regex;
    private readonly bool _matchesPath;

    private FileNameGlob(Regex regex, bool matchesPath)
    {
        _regex = regex;
        _matchesPath = matchesPath;
    }

    /// <summary>Compiles a pattern, or returns <see langword="null"/> when it is not a usable glob.</summary>
    public static FileNameGlob? TryCreate(string pattern)
    {
        var normalized = pattern.Replace('\\', '/');
        var matchesPath = normalized.Contains('/', StringComparison.Ordinal);
        var regex = new StringBuilder(matchesPath ? "(?:^|/)" : "^");
        var braces = 0;
        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            switch (c)
            {
                case '*' when i + 1 < normalized.Length && normalized[i + 1] == '*':
                    regex.Append(".*");
                    i++;
                    if (i + 1 < normalized.Length && normalized[i + 1] == '/')
                    {
                        regex.Append("/?");
                        i++;
                    }

                    break;
                case '*':
                    regex.Append("[^/]*");
                    break;
                case '?':
                    regex.Append("[^/]");
                    break;
                case '{':
                    braces++;
                    regex.Append("(?:");
                    break;
                case '}' when braces > 0:
                    braces--;
                    regex.Append(')');
                    break;
                case ',' when braces > 0:
                    regex.Append('|');
                    break;
                case '[':
                    var close = normalized.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        regex.Append("\\[");
                        break;
                    }

                    var set = normalized[(i + 1)..close].Replace("\\", "\\\\", StringComparison.Ordinal);
                    regex.Append('[').Append(set.StartsWith('!') ? "^" + set[1..] : set).Append(']');
                    i = close;
                    break;
                default:
                    regex.Append(Regex.Escape(char.ToString(c)));
                    break;
            }
        }

        if (braces != 0)
        {
            return null;
        }

        regex.Append('$');
        try
        {
            return new FileNameGlob(new Regex(regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), matchesPath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Whether a file matches.</summary>
    /// <param name="path">The file's path or name.</param>
    public bool IsMatch(string path)
    {
        var normalized = path.Replace('\\', '/');
        try
        {
            return _regex.IsMatch(_matchesPath ? normalized : normalized[(normalized.LastIndexOf('/') + 1)..]);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
