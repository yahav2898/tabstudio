using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Finds the toolchain projects in a workspace: folders with one of a toolchain's project files, or, for a
/// toolchain whose programs are single files (Python), a folder with its source files at the top.
/// </summary>
/// <remarks>
/// Only the Store's listed toolchains count, since only they run. Explicit manifests identify separate projects,
/// including nested packages in a monorepo. Source folders inside a project (a Python package, say) belong to it,
/// and dependency and build folders
/// (<c>node_modules</c>, <c>.venv</c>, <c>bin</c>, <c>obj</c>, dot folders) are never searched.
/// </remarks>
public static class ToolchainProjectDetector
{
    private const int MaxFolders = 2_000;

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "out", "target", "__pycache__", "venv", "env", "site-packages", "vendor",
    };

    /// <summary>Finds the projects under a folder, nearest the root first.</summary>
    /// <param name="root">The workspace folder.</param>
    /// <param name="catalog">The toolchain catalog.</param>
    /// <param name="maxDepth">How many folder levels below the root to look at.</param>
    public static IReadOnlyList<ToolchainProject> Detect(string root, ToolchainCatalog catalog, int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(catalog);
        var result = new List<ToolchainProject>();
        if (!Directory.Exists(root))
        {
            return result;
        }

        var toolchains = catalog.Listed.ToList();
        var queue = new Queue<(string Folder, int Depth)>();
        queue.Enqueue((Path.GetFullPath(root), 0));
        var visited = 0;
        while (queue.Count > 0 && visited++ < MaxFolders)
        {
            var (folder, depth) = queue.Dequeue();
            string[] files;
            string[] folders;
            try
            {
                files = [.. Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>()];
                folders = [.. Directory.EnumerateDirectories(folder).Order(StringComparer.OrdinalIgnoreCase)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var toolchain in toolchains)
            {
                var marker = toolchain.ProjectFiles.FirstOrDefault(f => files.Contains(f, StringComparer.OrdinalIgnoreCase));
                var insideProject = result.Any(p => p.Toolchain.Id == toolchain.Id && IsInside(folder, p.Directory));
                if (insideProject && marker is null && !toolchain.AllowNestedProjects)
                {
                    continue;
                }

                var isSourceFolder = !insideProject && marker is null && toolchain.SourceFolderIsProject && files.Any(toolchain.RunsFile);
                if (marker is not null || isSourceFolder)
                {
                    result.Add(new ToolchainProject(toolchain, folder, marker is null ? null : files.First(f => string.Equals(f, marker, StringComparison.OrdinalIgnoreCase))));
                }
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var child in folders)
            {
                var name = Path.GetFileName(child);
                if (!name.StartsWith('.') && !IgnoredFolders.Contains(name) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                {
                    queue.Enqueue((child, depth + 1));
                }
            }
        }

        return result;
    }

    private static bool IsInside(string folder, string ancestor) =>
        folder.StartsWith(ancestor.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>The project a file belongs to: the nearest one whose folder holds it and whose toolchain runs it.</summary>
    /// <param name="projects">The workspace's projects.</param>
    /// <param name="file">A file.</param>
    public static ToolchainProject? ForFile(IEnumerable<ToolchainProject> projects, string file)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(file);
        return projects.Where(p => p.Contains(file) && p.Toolchain.RunsFile(file)).MaxBy(p => p.Directory.Length);
    }
}
