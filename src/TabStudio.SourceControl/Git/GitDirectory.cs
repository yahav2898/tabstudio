using System.Text;
using System.Text.RegularExpressions;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// A repository's Git directory (<c>.git</c>): its references, <c>HEAD</c>, configuration, shallow list and object
/// database, read and written as Git does, so that Git in the Terminal and TabStudio agree on what the folder holds.
/// </summary>
internal sealed partial class GitDirectory
{
    private GitDirectory(string path, string common)
    {
        Path = path;
        CommonPath = common;
        Objects = new ObjectStore(System.IO.Path.Combine(common, "objects"));
    }

    /// <summary>The Git directory.</summary>
    public string Path { get; }

    /// <summary>The directory that holds the configuration and references: the main clone's, for a worktree.</summary>
    public string CommonPath { get; }

    /// <summary>The object database.</summary>
    public ObjectStore Objects { get; }

    /// <summary>Opens an existing Git directory.</summary>
    public static GitDirectory Open(string gitDirectory)
    {
        var commondir = System.IO.Path.Combine(gitDirectory, "commondir");
        var common = File.Exists(commondir)
            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(gitDirectory, File.ReadAllText(commondir).Trim()))
            : gitDirectory;
        return new GitDirectory(System.IO.Path.GetFullPath(gitDirectory), common);
    }

    /// <summary>Creates the Git directory of a new clone, as <c>git clone</c> leaves it before checking out.</summary>
    /// <param name="gitDirectory">The <c>.git</c> folder to create.</param>
    /// <param name="remote">The remote's address, without credentials.</param>
    /// <param name="branch">The branch to follow, or <see langword="null"/> for a detached commit.</param>
    /// <param name="partial">Whether file contents are fetched on demand, as a partial clone's are.</param>
    public static GitDirectory Create(string gitDirectory, string remote, string? branch, bool partial)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(gitDirectory, "refs", "heads"));
        Directory.CreateDirectory(System.IO.Path.Combine(gitDirectory, "refs", "tags"));
        Directory.CreateDirectory(System.IO.Path.Combine(gitDirectory, "refs", "remotes", "origin"));
        Directory.CreateDirectory(System.IO.Path.Combine(gitDirectory, "info"));
        var config = new StringBuilder();
        config.Append("[core]\n\trepositoryformatversion = ").Append(partial ? '1' : '0').Append("\n\tfilemode = false\n\tbare = false\n\tlogallrefupdates = true\n");
        if (partial)
        {
            config.Append("[extensions]\n\tpartialclone = origin\n");
        }

        config.Append("[remote \"origin\"]\n\turl = ").Append(remote).Append("\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
        if (partial)
        {
            config.Append("\tpromisor = true\n\tpartialclonefilter = blob:none\n");
        }

        if (branch is not null)
        {
            config.Append("[branch \"").Append(branch).Append("\"]\n\tremote = origin\n\tmerge = refs/heads/").Append(branch).Append('\n');
        }

        File.WriteAllText(System.IO.Path.Combine(gitDirectory, "config"), config.ToString());
        File.WriteAllText(System.IO.Path.Combine(gitDirectory, "description"), "Unnamed repository; edit this file 'description' to name the repository.\n");
        File.WriteAllText(System.IO.Path.Combine(gitDirectory, "HEAD"), branch is null ? GitHash.Zero + "\n" : $"ref: refs/heads/{branch}\n");
        return Open(gitDirectory);
    }

    /// <summary>The branch <c>HEAD</c> names, such as <c>main</c>; <see langword="null"/> when it holds a commit.</summary>
    public string? CurrentBranch
    {
        get
        {
            var head = File.ReadAllText(System.IO.Path.Combine(Path, "HEAD")).Trim();
            return head.StartsWith("ref: refs/heads/", StringComparison.Ordinal) ? head["ref: refs/heads/".Length..] : null;
        }
    }

    /// <summary>The commit <c>HEAD</c> points at; <see langword="null"/> before the first commit.</summary>
    public string? Head
    {
        get
        {
            var head = File.ReadAllText(System.IO.Path.Combine(Path, "HEAD")).Trim();
            return head.StartsWith("ref:", StringComparison.Ordinal) ? ReadRef(head[4..].Trim()) : GitHash.IsHash(head) && head != GitHash.Zero ? head.ToLowerInvariant() : null;
        }
    }

    /// <summary>Points <c>HEAD</c> at a branch, or at a commit when <paramref name="branch"/> is <see langword="null"/>.</summary>
    public void SetHead(string? branch, string commit)
    {
        WriteAtomically(System.IO.Path.Combine(Path, "HEAD"), branch is null ? commit + "\n" : $"ref: refs/heads/{branch}\n");
    }

    /// <summary>Reads a reference, loose or packed; <see langword="null"/> when it does not exist.</summary>
    /// <param name="name">The full name, such as <c>refs/heads/main</c>.</param>
    public string? ReadRef(string name)
    {
        for (var depth = 0; depth < 5; depth++)
        {
            string? value = null;
            foreach (var folder in new[] { Path, CommonPath }.Distinct(StringComparer.Ordinal))
            {
                var loose = System.IO.Path.Combine(folder, name.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (File.Exists(loose))
                {
                    value = File.ReadAllText(loose).Trim();
                    break;
                }
            }

            value ??= PackedRefs().GetValueOrDefault(name);
            if (value is null)
            {
                return null;
            }

            if (value.StartsWith("ref:", StringComparison.Ordinal))
            {
                name = value[4..].Trim();
                continue;
            }

            return GitHash.IsHash(value) ? value.ToLowerInvariant() : null;
        }

        return null;
    }

    /// <summary>Writes a loose reference, and a reflog line where Git keeps reflogs.</summary>
    /// <param name="name">The full name, such as <c>refs/heads/main</c>.</param>
    /// <param name="id">The commit.</param>
    /// <param name="message">What the reflog says moved it.</param>
    public void WriteRef(string name, string id, string message)
    {
        var old = ReadRef(name) ?? GitHash.Zero;
        var path = System.IO.Path.Combine(CommonPath, name.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        WriteAtomically(path, id + "\n");
        AppendReflog(name, old, id, message);
        if (CurrentBranch is { } branch && name == "refs/heads/" + branch)
        {
            AppendReflog("HEAD", old, id, message);
        }
    }

    /// <summary>Records the commits a shallow fetch stopped at, as <c>.git/shallow</c> does.</summary>
    public void AddShallow(IEnumerable<string> commits)
    {
        var path = System.IO.Path.Combine(CommonPath, "shallow");
        var existing = File.Exists(path) ? File.ReadAllLines(path).Where(GitHash.IsHash) : Enumerable.Empty<string>();
        var all = existing.Concat(commits).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (all.Count > 0)
        {
            WriteAtomically(path, string.Join('\n', all) + "\n");
        }
    }

    /// <summary>The commits the history stops at because the clone is shallow.</summary>
    public IReadOnlySet<string> Shallow
    {
        get
        {
            var path = System.IO.Path.Combine(CommonPath, "shallow");
            return File.Exists(path)
                ? File.ReadAllLines(path).Where(GitHash.IsHash).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>The address of a remote, as the configuration gives it.</summary>
    public string? RemoteAddress(string remote = "origin") => ConfigValue($"remote \"{remote}\"", "url");

    /// <summary>A value of the repository's configuration; <see langword="null"/> when it is not set.</summary>
    /// <param name="section">The section, such as <c>user</c> or <c>remote "origin"</c>.</param>
    /// <param name="key">The key, such as <c>name</c>.</param>
    public string? ConfigValue(string section, string key) => ReadConfigValue(System.IO.Path.Combine(CommonPath, "config"), section, key);

    /// <summary>Reads a value from a Git configuration file.</summary>
    internal static string? ReadConfigValue(string file, string section, string key)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        string? current = null;
        string? value = null;
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (SectionPattern().Match(line) is { Success: true } header)
            {
                current = header.Groups["sub"].Success
                    ? $"{header.Groups["kind"].Value.ToLowerInvariant()} \"{header.Groups["sub"].Value}\""
                    : header.Groups["kind"].Value.ToLowerInvariant();
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (current is null || equals < 0)
            {
                continue;
            }

            var sectionName = section.Contains('"', StringComparison.Ordinal)
                ? section[..section.IndexOf(' ', StringComparison.Ordinal)].ToLowerInvariant() + section[section.IndexOf(' ', StringComparison.Ordinal)..]
                : section.ToLowerInvariant();
            if (current == sectionName && string.Equals(line[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                value = line[(equals + 1)..].Trim().Trim('"');
            }
        }

        return value;
    }

    /// <summary>Adds a pattern to <c>info/exclude</c>, unless it is there.</summary>
    public void Exclude(string pattern)
    {
        try
        {
            var info = System.IO.Path.Combine(CommonPath, "info");
            var exclude = System.IO.Path.Combine(info, "exclude");
            var text = File.Exists(exclude) ? File.ReadAllText(exclude) : string.Empty;
            var bare = pattern.Trim('/');
            if (text.Split('\n').Any(line => line.Trim() is var trimmed && (trimmed == pattern || trimmed == bare || trimmed == "/" + pattern || trimmed == "/" + bare)))
            {
                return;
            }

            Directory.CreateDirectory(info);
            File.AppendAllText(exclude, (text.Length > 0 && !text.EndsWith('\n') ? "\n" : string.Empty) + pattern + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Git then lists the folder as untracked; nothing else depends on it.
        }
    }

    private Dictionary<string, string> PackedRefs()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = System.IO.Path.Combine(CommonPath, "packed-refs");
        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith('^'))
            {
                continue;
            }

            var parts = line.Split(' ', 2);
            if (parts.Length == 2 && GitHash.IsHash(parts[0]))
            {
                result[parts[1].Trim()] = parts[0];
            }
        }

        return result;
    }

    private void AppendReflog(string name, string old, string id, string message)
    {
        try
        {
            var log = System.IO.Path.Combine(name == "HEAD" ? Path : CommonPath, "logs", name.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var person = GitIdentity.Committer(this);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{old} {id} {person.Format()}\t{message.Replace('\n', ' ')}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A reflog is history for people; the reference itself is what matters.
        }
    }

    private static void WriteAtomically(string path, string text)
    {
        var temporary = path + ".lock";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

    [GeneratedRegex(@"^\[\s*(?<kind>[A-Za-z0-9.-]+)(?:\s+""(?<sub>[^""]*)"")?\s*\]$")]
    private static partial Regex SectionPattern();
}
