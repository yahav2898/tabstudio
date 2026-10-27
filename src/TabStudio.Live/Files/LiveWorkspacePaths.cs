using TabStudio.Core.Workspaces;

namespace TabStudio.Live.Files;

/// <summary>
/// The paths of a shared workspace as they travel: relative to the workspace's folder, with '/' between folders. Every
/// path a guest sends is checked here before the host touches its disk, so a guest can never reach a file outside the
/// shared folder, in a folder the workspace leaves out (build output, <c>.git</c>), or behind a symbolic link.
/// </summary>
public static class LiveWorkspacePaths
{
    /// <summary>The longest relative path accepted.</summary>
    public const int MaxPathLength = 1024;

    /// <summary>
    /// A relative path in its one accepted form, or <see langword="null"/> when it is not a safe path inside a workspace:
    /// empty, absolute, with a drive, a backslash, a control character, an empty, <c>.</c> or <c>..</c> segment, or
    /// a folder the workspace leaves out.
    /// </summary>
    /// <param name="relativePath">The path as a device sent it.</param>
    public static string? Normalize(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath.Length > MaxPathLength)
        {
            return null;
        }

        foreach (var c in relativePath)
        {
            if (c < ' ' || c is '\\' or ':' or '\u007f')
            {
                return null;
            }
        }

        var segments = relativePath.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0 || segment is "." or ".." || segment.Trim().Length == 0)
            {
                return null;
            }

            // Folders a workspace leaves out are not shared, so nothing in them can be asked for.
            if (i < segments.Length - 1 && PathUtilities.IsIgnoredDirectory(segment))
            {
                return null;
            }
        }

        return relativePath;
    }

    /// <summary>
    /// The full path on this device of a relative path in the workspace, or <see langword="null"/> when the path is
    /// unsafe (<see cref="Normalize"/>) or leads out of the folder, such as through a symbolic link.
    /// </summary>
    /// <param name="root">The workspace's folder on this device.</param>
    /// <param name="relativePath">The relative path.</param>
    public static string? ToFullPath(string root, string? relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        if (Normalize(relativePath) is not { } normalized)
        {
            return null;
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine([fullRoot, .. normalized.Split('/')]));
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        // No folder or file on the way may be a link, which could point anywhere on the device.
        var current = fullRoot;
        foreach (var segment in normalized.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (IsLink(current))
            {
                return null;
            }
        }

        return full;
    }

    /// <summary>
    /// The relative path of a full path on this device, or <see langword="null"/> when it is not inside the workspace's
    /// folder or not a path that would be shared.
    /// </summary>
    /// <param name="root">The workspace's folder on this device.</param>
    /// <param name="fullPath">The full path.</param>
    public static string? ToRelativePath(string root, string fullPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(fullPath);
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var relative = full[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/');
        return Normalize(relative);
    }

    private static bool IsLink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                return info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }

            var directory = new DirectoryInfo(path);
            return directory.Exists && (directory.LinkTarget is not null || directory.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
