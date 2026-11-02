using System.Text.RegularExpressions;

namespace TabStudio.SourceControl;

/// <summary>Reads a repository's <c>.gitmodules</c> and finds which of its submodules are on GitHub.</summary>
internal static partial class Submodules
{
    /// <summary>The URL of each submodule, by its path, as <c>.gitmodules</c> gives them.</summary>
    /// <param name="text">The content of <c>.gitmodules</c>.</param>
    public static Dictionary<string, string> ReadUrls(string text)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        string? path = null;
        string? url = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                Add();
                path = url = null;
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim().Trim('"');
            if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
            {
                path = value.Trim('/');
            }
            else if (string.Equals(key, "url", StringComparison.OrdinalIgnoreCase))
            {
                url = value;
            }
        }

        Add();
        return urls;

        void Add()
        {
            if (path is { Length: > 0 } && url is { Length: > 0 })
            {
                urls[path] = url;
            }
        }
    }

    /// <summary>
    /// The GitHub repository a submodule URL names: an HTTPS or SSH address on github.com, or one relative to the
    /// repository's own, such as <c>../lib.git</c>; <see langword="null"/> for any other host.
    /// </summary>
    /// <param name="url">The submodule's URL.</param>
    /// <param name="owner">The owner of the repository that has the submodule.</param>
    /// <param name="repository">Its name.</param>
    public static (string Owner, string Repository)? ResolveGitHub(string url, string owner, string repository)
    {
        if (url.StartsWith("./", StringComparison.Ordinal) || url.StartsWith("../", StringComparison.Ordinal))
        {
            var parts = new List<string> { owner, repository };
            foreach (var part in url.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == "..")
                {
                    if (parts.Count == 0)
                    {
                        return null;
                    }

                    parts.RemoveAt(parts.Count - 1);
                }
                else if (part != ".")
                {
                    parts.Add(part);
                }
            }

            return parts.Count == 2 ? (parts[0], TrimGit(parts[1])) : null;
        }

        var match = GitHubUrl().Match(url.Trim());
        return match.Success ? (match.Groups["owner"].Value, TrimGit(match.Groups["name"].Value)) : null;
    }

    private static string TrimGit(string name) => name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    [GeneratedRegex(@"^(?:(?:https?|git|ssh)://(?:[^@/]+@)?(?:www\.)?github\.com(?::\d+)?/|git@github\.com:)(?<owner>[A-Za-z0-9-]+)/(?<name>[A-Za-z0-9._-]+?)/?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GitHubUrl();
}
