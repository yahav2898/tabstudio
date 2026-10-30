namespace TabStudio.SourceControl;

/// <summary>How a file differs from the last synced commit.</summary>
public enum ChangeKind
{
    /// <summary>The file is new.</summary>
    Added,

    /// <summary>The file's content changed.</summary>
    Modified,

    /// <summary>The file was deleted.</summary>
    Deleted,
}

/// <summary>A changed file.</summary>
/// <param name="Path">The path relative to the root, with <c>/</c> separators.</param>
/// <param name="Kind">How it changed.</param>
public sealed record FileChange(string Path, ChangeKind Kind);

/// <summary>Compares the files in a folder with the files of the last synced commit.</summary>
public static class WorkingTree
{
    /// <summary>Folders whose untracked files are never part of a commit, whatever the <c>.gitignore</c> says.</summary>
    private static readonly HashSet<string> AlwaysIgnored = new(StringComparer.Ordinal) { ".git", SyncMetadata.FolderName };

    /// <summary>Lists the files of a folder that a commit would include, respecting <c>.gitignore</c> files.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="tracked">Files tracked by the last commit; they are listed even when a rule ignores them, as in Git.</param>
    /// <returns>Paths relative to the root, with <c>/</c> separators, sorted.</returns>
    public static IReadOnlyList<string> EnumerateFiles(string root, IReadOnlyDictionary<string, TrackedFile>? tracked = null)
    {
        var ignore = new GitIgnore();
        var files = new List<string>();
        Walk(root, string.Empty, ignore, tracked, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>Finds what changed since the last synced commit.</summary>
    /// <param name="root">The linked folder.</param>
    /// <param name="tracked">The files of the last synced commit.</param>
    /// <param name="hashes">Known hashes of unchanged files; files are hashed when it is <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The changes, sorted by path.</returns>
    public static IReadOnlyList<FileChange> GetChanges(string root, IReadOnlyDictionary<string, TrackedFile> tracked, HashCache? hashes = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tracked);
        hashes ??= HashCache.Empty();

        var changes = new List<FileChange>();
        var present = EnumerateFiles(root, tracked);
        var presentSet = new HashSet<string>(present, StringComparer.Ordinal);

        foreach (var path in present)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!tracked.TryGetValue(path, out var file))
            {
                changes.Add(new FileChange(path, ChangeKind.Added));
            }
            else if (!string.Equals(hashes.GetHash(root, path), file.Sha, StringComparison.Ordinal))
            {
                changes.Add(new FileChange(path, ChangeKind.Modified));
            }
        }

        changes.AddRange(tracked.Keys.Where(path => !presentSet.Contains(path)).Select(path => new FileChange(path, ChangeKind.Deleted)));
        changes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return changes;
    }

    /// <summary>The Git blob hash of a file in the folder.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    public static string HashFile(string root, string relativePath) => GitObjects.BlobHash(File.ReadAllBytes(FullPath(root, relativePath)));

    /// <summary>Converts a repository path to a full path on this device.</summary>
    /// <param name="root">The folder.</param>
    /// <param name="relativePath">The path relative to the root, with <c>/</c> separators.</param>
    /// <exception cref="InvalidOperationException">The path would leave the folder.</exception>
    public static string FullPath(string root, string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal)
            ? full
            : throw new InvalidOperationException($"The path '{relativePath}' is outside the repository.");
    }

    private static void Walk(string root, string relativeFolder, GitIgnore ignore, IReadOnlyDictionary<string, TrackedFile>? tracked, List<string> files, bool trackedOnly = false)
    {
        var folder = relativeFolder.Length == 0 ? root : FullPath(root, relativeFolder);
        var gitignore = Path.Combine(folder, ".gitignore");
        if (File.Exists(gitignore))
        {
            ignore.Add(File.ReadAllText(gitignore), relativeFolder);
        }

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var relative = Combine(relativeFolder, Path.GetFileName(file));
            if (tracked?.ContainsKey(relative) == true || (!trackedOnly && !ignore.IsIgnored(relative, isDirectory: false)))
            {
                files.Add(relative);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(directory);
            var relative = Combine(relativeFolder, name);
            if (IsSymbolicLink(directory))
            {
                continue;
            }

            // TabStudio's own folder holds its sync state beside files a repository shares, such as launch.json:
            // the files the last commit tracks there are listed, so they are neither reported deleted nor deleted
            // on GitHub by a commit of everything, and the rest is never committed.
            if (AlwaysIgnored.Contains(name))
            {
                if (HoldsTracked(tracked, relative))
                {
                    Walk(root, relative, ignore, tracked, files, trackedOnly: true);
                }

                continue;
            }

            // An ignored folder is still visited when it holds tracked files.
            if ((trackedOnly || ignore.IsIgnored(relative, isDirectory: true)) && !HoldsTracked(tracked, relative))
            {
                continue;
            }

            Walk(root, relative, ignore, tracked, files, trackedOnly);
        }
    }

    /// <summary>Whether the last commit tracks a file inside a folder; asked only of folders that would be skipped.</summary>
    private static bool HoldsTracked(IReadOnlyDictionary<string, TrackedFile>? tracked, string folder)
    {
        if (tracked is null || tracked.Count == 0)
        {
            return false;
        }

        var prefix = folder + "/";
        return tracked.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsSymbolicLink(string path) => new DirectoryInfo(path).LinkTarget is not null;

    private static string Combine(string folder, string name) => folder.Length == 0 ? name : folder + "/" + name;
}
