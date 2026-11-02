using System.Text.RegularExpressions;

namespace TabStudio.SourceControl;

/// <summary>
/// A folder that Git cloned from GitHub, read from its <c>.git</c> files without Git: the GitHub repository its remote
/// points at, the branch it is on and the commit it holds. Such a folder is linked with
/// <see cref="GitHubSync.LinkCheckoutAsync"/>, so its changes show without downloading it again.
/// </summary>
/// <param name="Root">The folder that holds <c>.git</c>.</param>
/// <param name="Owner">The GitHub repository's owner.</param>
/// <param name="Repository">The GitHub repository's name.</param>
/// <param name="Branch">The checked-out branch, or <see langword="null"/> when <c>HEAD</c> holds a commit.</param>
/// <param name="Commit">The commit <c>HEAD</c> points at, or <see langword="null"/> before the first commit.</param>
/// <param name="GitDirectory">The Git directory: <c>.git</c>, or where a <c>.git</c> file points.</param>
public sealed partial record GitCheckout(string Root, string Owner, string Repository, string? Branch, string? Commit, string GitDirectory)
{
    /// <summary>"owner/repository".</summary>
    public string FullName => $"{Owner}/{Repository}";

    /// <summary>
    /// Finds the Git clone of a GitHub repository that contains a path: the path itself or one of its parents.
    /// </summary>
    /// <param name="path">A file or folder.</param>
    /// <returns>The clone, or <see langword="null"/> when there is none or its remote is not on GitHub.</returns>
    public static GitCheckout? Find(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        for (var folder = Directory.Exists(path) ? Path.GetFullPath(path) : Path.GetDirectoryName(Path.GetFullPath(path)); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (Path.Exists(Path.Combine(folder, ".git")))
            {
                return Read(folder);
            }
        }

        return null;
    }

    /// <summary>Reads the clone in a folder that holds <c>.git</c>.</summary>
    /// <returns>The clone, or <see langword="null"/> when its files cannot be read or no remote is on GitHub.</returns>
    public static GitCheckout? Read(string root)
    {
        try
        {
            if (GitDirectoryOf(root) is not { } gitDirectory)
            {
                return null;
            }

            // A worktree keeps HEAD in its own folder and the config and refs in the main clone's.
            var common = File.Exists(Path.Combine(gitDirectory, "commondir"))
                ? Path.GetFullPath(Path.Combine(gitDirectory, File.ReadAllText(Path.Combine(gitDirectory, "commondir")).Trim()))
                : gitDirectory;
            var head = File.ReadAllText(Path.Combine(gitDirectory, "HEAD")).Trim();
            string? branch = null;
            string? commit;
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                var reference = head[4..].Trim();
                branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..] : null;
                commit = ResolveReference(gitDirectory, common, reference);
            }
            else
            {
                commit = IsCommitHash(head) ? head.ToLowerInvariant() : null;
            }

            var config = File.Exists(Path.Combine(common, "config")) ? File.ReadAllText(Path.Combine(common, "config")) : string.Empty;
            return RemoteRepository(config, branch) is { } remote
                ? new GitCheckout(Path.GetFullPath(root), remote.Owner, remote.Repository, branch, commit, gitDirectory)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The GitHub repository a remote's address names: <c>https://github.com/owner/repo(.git)</c>,
    /// <c>git@github.com:owner/repo.git</c> or <c>ssh://git@github.com/owner/repo.git</c>.
    /// </summary>
    /// <returns>The owner and name, or <see langword="null"/> when the address is not on GitHub.</returns>
    public static (string Owner, string Repository)? ParseRemote(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || RemotePattern().Match(address.Trim()) is not { Success: true } match)
        {
            return null;
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }

    /// <summary>The GitHub repository of the branch's remote, else <c>origin</c>, else the first remote on GitHub.</summary>
    internal static (string Owner, string Repository)? RemoteRepository(string config, string? branch)
    {
        var remotes = new Dictionary<string, string>(StringComparer.Ordinal);
        string? branchRemote = null;
        string? section = null;
        foreach (var raw in config.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (SectionPattern().Match(line) is { Success: true } header)
            {
                section = header.Groups["kind"].Value.ToLowerInvariant() + " " + header.Groups["name"].Value;
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (section is null || equals < 0)
            {
                continue;
            }

            var key = line[..equals].Trim().ToLowerInvariant();
            var value = line[(equals + 1)..].Trim().Trim('"');
            if (key == "url" && section.StartsWith("remote ", StringComparison.Ordinal))
            {
                remotes.TryAdd(section["remote ".Length..], value);
            }
            else if (key == "remote" && branch is not null && section == "branch " + branch)
            {
                branchRemote = value;
            }
        }

        foreach (var name in new[] { branchRemote, "origin" })
        {
            if (name is not null && remotes.TryGetValue(name, out var address) && ParseRemote(address) is { } repository)
            {
                return repository;
            }
        }

        return remotes.Values.Select(ParseRemote).FirstOrDefault(r => r is not null);
    }

    private static string? GitDirectoryOf(string root)
    {
        var dotGit = Path.Combine(root, ".git");
        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        // A submodule or a worktree: ".git" is a file with "gitdir: <path>".
        if (File.Exists(dotGit) && File.ReadAllText(dotGit).Trim() is { } text && text.StartsWith("gitdir:", StringComparison.Ordinal))
        {
            var target = Path.GetFullPath(Path.Combine(root, text["gitdir:".Length..].Trim()));
            return Directory.Exists(target) ? target : null;
        }

        return null;
    }

    private static string? ResolveReference(string gitDirectory, string common, string reference)
    {
        foreach (var folder in new[] { gitDirectory, common }.Distinct(StringComparer.Ordinal))
        {
            var loose = Path.Combine(folder, reference.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(loose) && File.ReadAllText(loose).Trim() is { } value && IsCommitHash(value))
            {
                return value.ToLowerInvariant();
            }
        }

        var packed = Path.Combine(common, "packed-refs");
        if (File.Exists(packed))
        {
            foreach (var line in File.ReadLines(packed))
            {
                var parts = line.Split(' ', 2);
                if (parts.Length == 2 && string.Equals(parts[1].Trim(), reference, StringComparison.Ordinal) && IsCommitHash(parts[0]))
                {
                    return parts[0].ToLowerInvariant();
                }
            }
        }

        return null;
    }

    private static bool IsCommitHash(string text) => text.Length == 40 && text.All(char.IsAsciiHexDigit);

    [GeneratedRegex(@"^(?:https?://(?:[^@/]+@)?github\.com/|git@github\.com:|ssh://git@github\.com(?::\d+)?/|git://github\.com/)(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex RemotePattern();

    [GeneratedRegex(@"^\[\s*(?<kind>[A-Za-z0-9.-]+)(?:\s+""(?<name>[^""]*)"")?\s*\]$")]
    private static partial Regex SectionPattern();
}
