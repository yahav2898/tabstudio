namespace TabStudio.Core.Workspaces;

/// <summary>
/// Path helpers for files written on one operating system and read on another.
/// </summary>
/// <remarks>
/// Solution and project files written on Windows use backslashes. On Android and Linux a backslash
/// is a valid file name character, so every relative path read from those files goes through here.
/// </remarks>
public static class PathUtilities
{
    /// <summary>Directory names that never belong in the Solution Explorer or in a search.</summary>
    public static IReadOnlySet<string> IgnoredDirectoryNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", ".vscode", "node_modules", "TestResults", ".gradle", "target", "__pycache__",
    };

    /// <summary>Converts both <c>/</c> and <c>\</c> to the separator of the current platform.</summary>
    /// <param name="path">A path from a solution or project file.</param>
    public static string NormalizeSeparators(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>Resolves a path written relative to <paramref name="baseDirectory"/> into a full path.</summary>
    /// <param name="baseDirectory">The directory the path is relative to.</param>
    /// <param name="relativePath">The relative path, with either kind of separator.</param>
    public static string ResolveRelative(string baseDirectory, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(relativePath);
        return Path.GetFullPath(Path.Combine(baseDirectory, NormalizeSeparators(relativePath.Trim())));
    }

    /// <summary>
    /// The file a path names, found as Windows finds it: the path itself when it exists, otherwise the one existing
    /// file whose path differs only in letter case, as when a project written on Windows lists <c>mainwindow.xaml.cs</c>
    /// for <c>MainWindow.xaml.cs</c>. On Android and Linux file names are case-sensitive; MSBuild on Windows is not.
    /// </summary>
    /// <param name="fullPath">A full path.</param>
    /// <returns>The path of the existing file, or <see langword="null"/> when there is none.</returns>
    public static string? FindFile(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var current = root;
        var parts = fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var isLast = i == parts.Length - 1;
            var exact = Path.Combine(current, parts[i]);
            if (isLast ? File.Exists(exact) : Directory.Exists(exact))
            {
                current = exact;
                continue;
            }

            string[] candidates;
            try
            {
                candidates = isLast ? Directory.GetFiles(current) : Directory.GetDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            var matches = candidates.Where(c => string.Equals(Path.GetFileName(c), parts[i], StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
            {
                return null;
            }

            current = matches[0];
        }

        return current;
    }

    /// <summary>Whether a directory should be skipped when walking a workspace.</summary>
    /// <param name="directoryName">The directory name, without its parent path.</param>
    public static bool IsIgnoredDirectory(string directoryName) =>
        IgnoredDirectoryNames.Contains(directoryName) ||
        (directoryName.StartsWith('.') && directoryName is not ".github" and not ".devcontainer");

    /// <summary>
    /// The folder's path with every symbolic link in it resolved, so two paths to one folder compare equal; the path
    /// itself when it cannot be resolved.
    /// </summary>
    /// <param name="directory">A folder.</param>
    public static string RealDirectoryPath(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        try
        {
            var full = Path.GetFullPath(directory);
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var current = root;
            foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    current = Path.GetFullPath(target.FullName);
                }
            }

            return Path.TrimEndingDirectorySeparator(current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Path.TrimEndingDirectorySeparator(directory);
        }
    }

    /// <summary>
    /// Whether a walk that has already been through <paramref name="visited"/> (real paths, see
    /// <see cref="RealDirectoryPath"/>) should go into <paramref name="subdirectory"/>, adding it when it should.
    /// </summary>
    /// <remarks>
    /// A symbolic link back to a folder the walk is in (<c>self -&gt; .</c>) made walks repeat every file up to the
    /// system's limit of 40 links, doubling with every such link; a link to a folder outside is still followed, once.
    /// </remarks>
    /// <param name="subdirectory">The folder found.</param>
    /// <param name="visited">The real paths of the folders the walk has been through.</param>
    public static bool TryVisitDirectory(string subdirectory, ISet<string> visited)
    {
        ArgumentNullException.ThrowIfNull(subdirectory);
        ArgumentNullException.ThrowIfNull(visited);
        bool isLink;
        try
        {
            isLink = new DirectoryInfo(subdirectory).LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (!isLink)
        {
            return visited.Add(Path.TrimEndingDirectorySeparator(subdirectory));
        }

        // A link to a folder above one the walk went through leads back into the walk.
        var real = RealDirectoryPath(subdirectory);
        var under = real + Path.DirectorySeparatorChar;
        return !visited.Any(v => v.StartsWith(under, StringComparison.Ordinal)) && visited.Add(real);
    }

    /// <summary>The most a project, solution or source file read from a workspace may hold: 16 MB.</summary>
    public const long MaximumWorkspaceFileBytes = 16L * 1024 * 1024;

    /// <summary>
    /// Reads a text file as <see cref="File.ReadAllText(string)"/> does, refusing one larger than
    /// <paramref name="maximumBytes"/>.
    /// </summary>
    /// <remarks>
    /// A repository can name any path, such as <c>&lt;Import Project="/dev/zero" /&gt;</c> or a link to a device: read
    /// whole, it took the app's memory until Android ended it.
    /// </remarks>
    /// <param name="path">The file.</param>
    /// <param name="maximumBytes">The largest size read.</param>
    /// <exception cref="IOException">The file cannot be read or is too large.</exception>
    public static string ReadAllTextBounded(string path, long maximumBytes = MaximumWorkspaceFileBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                throw new IOException($"'{Path.GetFileName(path)}' is larger than {maximumBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
