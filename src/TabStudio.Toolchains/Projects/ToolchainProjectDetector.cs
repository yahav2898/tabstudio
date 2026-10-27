using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Microchip;

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
        var embeddedRoots = new HashSet<string>(StringComparer.Ordinal);
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

            var androidRoot = GradleAndroidProject.IsProject(folder);
            var mobileRoot = androidRoot || GradleAndroidProject.IsCapacitor(folder);
            var owner = result.Where(p => IsInside(folder, p.Directory)
                    && (p.Toolchain.Id is "android" or "java"
                        || p.Toolchain.Id == "node" && GradleAndroidProject.IsCapacitor(p.Directory)))
                .MaxBy(p => p.Directory.Length);
            var insideMobile = owner is not null && owner.Toolchain.Id != "java";
            var independentJavaRoot = files.Contains("pom.xml", StringComparer.OrdinalIgnoreCase)
                || files.Contains("settings.gradle", StringComparer.OrdinalIgnoreCase)
                || files.Contains("settings.gradle.kts", StringComparer.OrdinalIgnoreCase);

            var inMplabProject = File.Exists(Path.Combine(folder, MplabProject.Marker))
                || result.Any(p => MicrochipInstallation.IsCompiler(p.Toolchain.Id) && IsInside(folder, p.Directory));
            EmbeddedProject? embedded;
            try
            {
                embedded = EmbeddedProjectDetector.Read(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                embedded = null;
            }
            var embeddedId = embedded is null ? null : EmbeddedBuildPlanner.ToolchainId(embedded.Kind);
            if (embedded is not null)
            {
                embeddedRoots.Add(folder);
            }
            if (embedded is not null && toolchains.FirstOrDefault(t => t.Id == embeddedId) is { } embeddedToolchain)
            {
                result.Add(new ToolchainProject(embeddedToolchain, folder, embedded.Marker));
            }
            var insideEmbedded = embeddedRoots.Any(r => r == folder || IsInside(folder, r));
            foreach (var toolchain in toolchains)
            {
                if (toolchain.Auxiliary) continue;
                // An embedded build system owns its C/C++/Rust/Python source tree. Never run its cross-target
                // firmware as a host executable or rediscover each source directory as a desktop project.
                if (toolchain.Id == embeddedId || insideEmbedded && toolchain.Id is "cpp" or "python" or "rust")
                {
                    continue;
                }
                // MPLAB owns its Makefiles and C sources, including source-only child folders.
                if (inMplabProject && toolchain.Id == "cpp")
                {
                    continue;
                }

                if (toolchain.Id == "android" && !androidRoot)
                {
                    continue;
                }

                // Android settings and module build files also match Java's Gradle markers. A module and its
                // source folders belong to the mobile root; an explicit Maven or independent Gradle root can
                // start a separate JVM project, whose own descendants then follow the usual nesting rules.
                if (toolchain.Id == "java" && (mobileRoot || insideMobile && !independentJavaRoot))
                {
                    continue;
                }

                var marker = toolchain.ProjectFiles.FirstOrDefault(f => files.Contains(f, StringComparer.OrdinalIgnoreCase)
                    || (f.Contains('/') && File.Exists(Path.Combine(folder, f))));
                if (marker is not null && !marker.Contains('/'))
                {
                    marker = files.First(f => string.Equals(f, marker, StringComparison.OrdinalIgnoreCase));
                }

                // A Makefile that only runs cargo, npm, go or dotnet is a shortcut for the folder's real project,
                // which that toolchain detects from its own manifest; it is not a C or C++ program.
                if (toolchain.Id == "cpp" && marker is not null && IsMakefile(marker) && MakefileWrapper.WrappedTool(Path.Combine(folder, marker)) is not null)
                {
                    continue;
                }
                if (marker is not null && MicrochipInstallation.IsCompiler(toolchain.Id))
                {
                    try
                    {
                        var mplab = MplabProject.Load(Path.Combine(folder, marker));
                        var configuration = mplab.Configurations.FirstOrDefault(c => c.Name == "default") ?? mplab.Configurations[0];
                        if (!configuration.Compiler.Equals(toolchain.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
                    {
                        continue;
                    }
                }

                var insideProject = result.Any(p => p.Toolchain.Id == toolchain.Id && IsInside(folder, p.Directory));
                if (insideProject && marker is null && !toolchain.AllowNestedProjects)
                {
                    continue;
                }

                var isSourceFolder = !insideProject && marker is null && toolchain.SourceFolderIsProject && files.Any(toolchain.RunsFile);
                if (marker is not null || isSourceFolder)
                {
                    result.Add(new ToolchainProject(toolchain, folder, marker));
                }
            }

            PreferLanguageManifests(result, folder);

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

    /// <summary>Whether a project file is a Makefile, rather than a language's own manifest or CMakeLists.txt.</summary>
    /// <param name="marker">The project file's name.</param>
    public static bool IsMakefile(string marker) => marker is "Makefile" or "makefile" or "GNUmakefile";

    /// <summary>
    /// In one folder, a language's own manifest (<c>Cargo.toml</c>, <c>go.mod</c>, <c>package.json</c>,
    /// <c>pom.xml</c>) comes before a Makefile, so that the folder's first project, the default startup project,
    /// is the one the Makefile usually drives.
    /// </summary>
    private static void PreferLanguageManifests(List<ToolchainProject> result, string folder)
    {
        var first = result.FindIndex(p => p.Directory == folder);
        if (first < 0)
        {
            return;
        }

        var inFolder = result.Skip(first).ToList();
        if (inFolder.Count < 2 || !inFolder.Any(p => p.Toolchain.Id == "cpp" && p.Marker is { } m && IsMakefile(m)))
        {
            return;
        }

        result.RemoveRange(first, inFolder.Count);
        result.AddRange(inFolder.OrderBy(p => p.Toolchain.Id == "cpp" && p.Marker is { } marker && IsMakefile(marker) ? 1 : 0));
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
