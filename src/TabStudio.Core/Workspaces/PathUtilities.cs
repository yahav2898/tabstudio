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
}
