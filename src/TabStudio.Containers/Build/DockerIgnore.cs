using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.Containers.Build;

/// <summary>
/// A <c>.dockerignore</c> file: Go <c>filepath.Match</c> patterns with <c>**</c>, <c>!</c> exceptions, and the
/// last matching line deciding, as Docker reads it. A folder that is excluded excludes what it holds unless an
/// exception names something inside it.
/// </summary>
public sealed class DockerIgnore
{
    private readonly List<(Regex Pattern, bool Exclude, string Text)> _rules = [];

    /// <summary>Reads the rules.</summary>
    public DockerIgnore(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var exclude = !line.StartsWith('!');
            if (!exclude) line = line[1..].Trim();
            line = string.Join('/', line.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != "."));
            if (line.Length == 0) continue;
            _rules.Add((new Regex("^" + Translate(line) + "(/.*)?$", RegexOptions.CultureInvariant), exclude, line));
        }
    }

    /// <summary>Reads <c>.dockerignore</c> (or <c>&lt;Dockerfile&gt;.dockerignore</c>) of a build context.</summary>
    public static DockerIgnore ForContext(string context, string? dockerfile)
    {
        var specific = dockerfile is null ? null : dockerfile + ".dockerignore";
        var file = specific is not null && File.Exists(specific) ? specific : Path.Combine(context, ".dockerignore");
        return new DockerIgnore(File.Exists(file) ? File.ReadAllLines(file) : []);
    }

    /// <summary>Whether a path relative to the context is left out.</summary>
    public bool IsExcluded(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/').Trim('/');
        var excluded = false;
        foreach (var (pattern, exclude, _) in _rules)
        {
            if (pattern.IsMatch(relativePath)) excluded = exclude;
        }

        return excluded;
    }

    /// <summary>Whether a folder must still be walked because an exception may name something in it.</summary>
    public bool MayIncludeInside(string relativeFolder)
    {
        relativeFolder = relativeFolder.Replace('\\', '/').Trim('/');
        return _rules.Any(r => !r.Exclude && (r.Text.StartsWith(relativeFolder + "/", StringComparison.Ordinal) || r.Text.Contains("**", StringComparison.Ordinal)));
    }

    /// <summary>The files of a context that are sent to the build, relative, sorted.</summary>
    public IEnumerable<string> Files(string context)
    {
        var pending = new Stack<string>();
        pending.Push("");
        var result = new List<string>();
        while (pending.TryPop(out var relative))
        {
            var folder = relative.Length == 0 ? context : Path.Combine(context, relative);
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                var path = relative.Length == 0 ? entry.Name : relative + "/" + entry.Name;
                var excluded = IsExcluded(path);
                if (entry is DirectoryInfo && entry.LinkTarget is null)
                {
                    if (!excluded || MayIncludeInside(path)) pending.Push(path);
                    if (!excluded) result.Add(path + "/");
                }
                else if (!excluded)
                {
                    result.Add(path);
                }
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Translates a Go <c>filepath.Match</c> pattern with <c>**</c> into a regular expression.</summary>
    internal static string Translate(string pattern)
    {
        var result = new StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '*' when i + 1 < pattern.Length && pattern[i + 1] == '*':
                    i++;
                    if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                    {
                        i++;
                        result.Append("(.*/)?");
                    }
                    else
                    {
                        result.Append(".*");
                    }

                    break;
                case '*':
                    result.Append("[^/]*");
                    break;
                case '?':
                    result.Append("[^/]");
                    break;
                case '[':
                    var close = pattern.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        result.Append("\\[");
                        break;
                    }

                    var set = pattern[(i + 1)..close];
                    result.Append('[').Append(set.StartsWith('^') || set.StartsWith('!') ? "^" + set[1..].Replace("\\", "\\\\", StringComparison.Ordinal) : set.Replace("\\", "\\\\", StringComparison.Ordinal)).Append(']');
                    i = close;
                    break;
                case '\\' when i + 1 < pattern.Length:
                    result.Append(Regex.Escape(pattern[++i].ToString()));
                    break;
                default:
                    result.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        return result.ToString();
    }
}
