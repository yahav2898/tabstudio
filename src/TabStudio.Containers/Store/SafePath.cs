namespace TabStudio.Containers.Store;

/// <summary>
/// Paths inside a root file system that is not the device's: a path from a layer, a Dockerfile or a container is
/// resolved as if the root were <c>/</c>, following symbolic links in the root, so nothing it names can be outside.
/// </summary>
public static class SafePath
{
    private const int MaxLinks = 40;

    /// <summary>
    /// The device path of <paramref name="path"/> in <paramref name="root"/>: every folder on the way is resolved,
    /// following links as the container would, but the last component is not, so it can be created or replaced.
    /// </summary>
    /// <param name="root">The root file system.</param>
    /// <param name="path">A path in it, absolute or relative to its root.</param>
    /// <returns>A path under <paramref name="root"/>, or <paramref name="root"/> itself for <c>/</c>.</returns>
    public static string Resolve(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd('/');
        var parts = Split(path);
        if (parts.Count == 0)
        {
            return root;
        }

        var last = parts[^1];
        var parent = ResolveFolder(root, parts.GetRange(0, parts.Count - 1));
        return last == ".." ? Parent(root, parent) : Path.Combine(parent, last);
    }

    /// <summary>Like <see cref="Resolve"/>, but follows a link in the last component too, as reading a file does.</summary>
    public static string ResolveFollowing(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd('/');
        return ResolveFolder(root, Split(path));
    }

    /// <summary>The path of a device file relative to the root, as the container sees it: <c>/etc/hosts</c>.</summary>
    public static string ToGuest(string root, string devicePath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(devicePath)).Replace('\\', '/');
        return relative == "." ? "/" : "/" + relative;
    }

    /// <summary>A layer or archive entry's name as clean relative parts; <c>..</c> above the root stays at the root.</summary>
    public static List<string> Split(string path)
    {
        var result = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (result.Count > 0 && result[^1] != "..")
                {
                    result.RemoveAt(result.Count - 1);
                }

                continue;
            }

            result.Add(part);
        }

        return result;
    }

    private static string ResolveFolder(string root, List<string> parts)
    {
        var current = root;
        var queue = new LinkedList<string>(parts);
        var links = 0;
        while (queue.First is { } node)
        {
            var part = node.Value;
            queue.RemoveFirst();
            if (part == "..")
            {
                current = Parent(root, current);
                continue;
            }

            var next = Path.Combine(current, part);
            string? target = null;
            try
            {
                target = new FileInfo(next).LinkTarget;
            }
            catch (IOException)
            {
            }

            if (target is null)
            {
                current = next;
                continue;
            }

            if (++links > MaxLinks)
            {
                throw new IOException($"Too many levels of symbolic links at {ToGuest(root, next)}.");
            }

            if (target.StartsWith('/'))
            {
                current = root;
            }

            var linkParts = target.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != ".").ToList();
            for (var i = linkParts.Count - 1; i >= 0; i--)
            {
                queue.AddFirst(linkParts[i]);
            }
        }

        return current;
    }

    private static string Parent(string root, string current) =>
        current.Length <= root.Length ? root : Path.GetDirectoryName(current) is { } parent && parent.Length >= root.Length ? parent : root;
}
