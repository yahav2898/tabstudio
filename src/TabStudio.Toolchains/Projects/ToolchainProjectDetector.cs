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
/// (<c>node_modules</c>, <c>.venv</c>, <c>bin</c>, <c>obj</c>, dot folders) are never searched. Below the workspace
/// folder, documentation (<c>docs</c>) and a web app's static files (<c>wwwroot</c>) are not searched either: a
/// Sphinx <c>Makefile</c> and <c>conf.py</c>, or a docs site's <c>package.json</c>, build documentation, and the
/// files under <c>wwwroot</c> are what the app around them serves.
/// </remarks>
public static class ToolchainProjectDetector
{
    private const int MaxFolders = 2_000;

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "out", "target", "__pycache__", "venv", "env", "site-packages", "vendor",
    };

    /// <summary>
    /// Folders that hold documentation or a web app's static files. Their build files are not projects of the
    /// workspace: setting one up ran <c>npm ci</c> for a docs site, and a Sphinx Makefile became a C/C++ project.
    /// Opened as the workspace folder itself, such a folder is searched as usual.
    /// </summary>
    private static readonly HashSet<string> ContentFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "docs", "doc", "documentation", "wwwroot",
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

                // A Gradle or Maven build that compiles Kotlin is Kotlin's project, one that does not is Java's, and
                // an Android app is the Android SDK's; loose .kt and .java files go by their extension.
                if (toolchain.Id is "java" or "kotlin" && IsJvmBuild(files) && KotlinProject.IsKotlinBuild(folder) != (toolchain.Id == "kotlin"))
                {
                    continue;
                }

                if (toolchain.Id == "kotlin" && (mobileRoot || insideMobile))
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
            PreferPhpApps(result, folder);

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var child in folders)
            {
                var name = Path.GetFileName(child);
                if (!name.StartsWith('.') && !IgnoredFolders.Contains(name) && !ContentFolders.Contains(name) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                {
                    queue.Enqueue((child, depth + 1));
                }
            }
        }

        DropNestedNativeBuilds(result);
        return OrderForStartup(result, Path.GetFullPath(root));
    }

    /// <summary>
    /// Folders whose programs serve the project rather than being it: scripts, tools, tests, examples, samples,
    /// benchmarks and contributed extras. Their projects come after the others, so none of them is the default
    /// startup project when the workspace has another (jq's <c>scripts</c> before its C program).
    /// </summary>
    private static readonly HashSet<string> AuxiliaryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "scripts", "script", "tools", "tool", "utils", "test", "tests", "testing", "example", "examples", "sample", "samples",
        "benchmark", "benchmarks", "bench", "contrib", "extras", "ci", "misc",
    };

    /// <summary>
    /// The projects in the order the default startup project is chosen from: those in auxiliary folders (see
    /// <see cref="AuxiliaryFolders"/>) last, and in one folder, a folder of loose source files (a <c>setup.py</c>-less
    /// Python script beside tinyxml2's CMakeLists.txt) after a project with a build file; otherwise nearest the root
    /// first, as found.
    /// </summary>
    private static List<ToolchainProject> OrderForStartup(List<ToolchainProject> projects, string root)
    {
        var withBuildFile = projects.Where(p => p.Marker is not null).Select(p => p.Directory).ToHashSet(StringComparer.Ordinal);
        return [.. projects
            .Select((project, index) => (Project: project, Index: index))
            .OrderBy(p => IsAuxiliary(p.Project.Directory, root))
            .ThenBy(p => p.Project.Marker is null && withBuildFile.Contains(p.Project.Directory))
            .ThenBy(p => p.Index)
            .Select(p => p.Project)];
    }

    private static bool IsAuxiliary(string directory, string root) =>
        directory.Length > root.Length && IsInside(directory, root)
        && Path.GetRelativePath(root, directory).Split(Path.DirectorySeparatorChar).Any(AuxiliaryFolders.Contains);

    /// <summary>
    /// Removes C and C++ build files inside another C or C++ project: a <c>CMakeLists.txt</c> that its parent adds
    /// with <c>add_subdirectory</c>, or the Makefiles of a Makefile project's sources and bundled dependencies (zlib's
    /// <c>contrib</c>, redis's <c>deps</c>), are part of the build around them, not projects to build and start on
    /// their own.
    /// </summary>
    private static void DropNestedNativeBuilds(List<ToolchainProject> projects)
    {
        var builds = projects.Where(p => p.Toolchain.Id == "cpp" && p.Marker is not null).ToList();
        projects.RemoveAll(p => p.Toolchain.Id == "cpp" && p.Marker is not null
            && builds.Exists(outer => !ReferenceEquals(outer, p) && IsInside(p.Directory, outer.Directory)));
    }

    /// <summary>
    /// Whether a project file is a GNU Autotools build (<c>configure.ac</c>, or a generated <c>configure</c>), which
    /// is configured and then built with make, as jq's README says.
    /// </summary>
    /// <param name="marker">The project file's name.</param>
    public static bool IsAutotools(string marker) => marker is "configure.ac" or "configure";

    /// <summary>Whether a folder's files include a Gradle or Maven build file.</summary>
    private static bool IsJvmBuild(IEnumerable<string> files) =>
        files.Any(f => f is "pom.xml" or "build.gradle" or "build.gradle.kts" or "settings.gradle" or "settings.gradle.kts");

    /// <summary>Whether a project file is a Makefile, rather than a language's own manifest or CMakeLists.txt.</summary>
    /// <param name="marker">The project file's name.</param>
    public static bool IsMakefile(string marker) => marker is "Makefile" or "makefile" or "GNUmakefile";

    /// <summary>
    /// In one folder, the project a person means to run comes first, so that it is the default startup project: a
    /// language's own manifest (<c>Cargo.toml</c>, <c>go.mod</c>, <c>package.json</c>, <c>pom.xml</c>) before a
    /// <c>package.json</c> that only holds tooling (linters, formatters: nothing to start, no <c>main</c> or
    /// <c>bin</c>), as mdBook's beside its <c>Cargo.toml</c>, and both before a Makefile, which usually drives the
    /// folder's real project.
    /// </summary>
    private static void PreferLanguageManifests(List<ToolchainProject> result, string folder)
    {
        var first = result.FindIndex(p => p.Directory == folder);
        if (first < 0)
        {
            return;
        }

        var inFolder = result.Skip(first).ToList();
        if (inFolder.Count < 2)
        {
            return;
        }

        var ranks = inFolder.ToDictionary(p => p, Rank);
        var named = inFolder.ToDictionary(p => p, p => NamesFolder(p, folder));
        if (ranks.Values.All(r => r == 0) && named.Values.All(n => n == named[inFolder[0]]))
        {
            return;
        }

        // Between equals, the package named after the folder is the folder's project: hyperfine's Cargo.toml, not the
        // pyproject.toml of its plotting scripts (hyperfine-scripts).
        result.RemoveRange(first, inFolder.Count);
        result.AddRange(inFolder.OrderBy(p => ranks[p]).ThenBy(p => !named[p]));
    }

    /// <summary>Whether a project's manifest names its package after its folder (<c>name = "hyperfine"</c> in hyperfine).</summary>
    private static bool NamesFolder(ToolchainProject project, string folder)
    {
        if (project.MarkerPath is not { } manifest || Path.GetFileName(manifest) is not ("Cargo.toml" or "pyproject.toml" or "package.json"))
        {
            return false;
        }

        try
        {
            var text = new FileInfo(manifest).Length <= 256 * 1024 ? File.ReadAllText(manifest) : string.Empty;
            var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            return text.Contains("name = \"" + name + "\"", StringComparison.OrdinalIgnoreCase)
                || text.Contains("\"name\": \"" + name + "\"", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static int Rank(ToolchainProject project) =>
        project.Toolchain.Id == "cpp" && project.Marker is { } marker && IsMakefile(marker) ? 2
        : project.Toolchain.Id == "node" && project.Marker == "package.json" && IsToolingOnly(Path.Combine(project.Directory, "package.json")) ? 1
        : 0;

    /// <summary>
    /// Whether a <c>package.json</c> only holds tooling: no <c>start</c>, <c>dev</c> or <c>serve</c> script, and no
    /// <c>main</c> or <c>bin</c> to run.
    /// </summary>
    /// <param name="packageJson">The file.</param>
    public static bool IsToolingOnly(string packageJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJson));
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object || root.TryGetProperty("main", out _) || root.TryGetProperty("bin", out _))
            {
                return false;
            }

            return !root.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != System.Text.Json.JsonValueKind.Object
                || !(scripts.TryGetProperty("start", out _) || scripts.TryGetProperty("dev", out _) || scripts.TryGetProperty("serve", out _));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// In one folder, a PHP app comes before the Node.js package beside it, which builds its assets, as a Laravel
    /// app's <c>package.json</c> runs Vite: the app is what F5 serves.
    /// </summary>
    private static void PreferPhpApps(List<ToolchainProject> result, string folder)
    {
        // A Laravel app's package.json builds its assets; a Swift package's CMakeLists.txt is its second build.
        Prefer(result, folder, "php", "node");
        Prefer(result, folder, "swift", "cpp");
    }

    /// <summary>Puts a folder's project of one toolchain before its project of another, as the folder's main build.</summary>
    private static void Prefer(List<ToolchainProject> result, string folder, string first, string second)
    {
        var preferred = result.FindIndex(p => p.Directory == folder && p.Toolchain.Id == first);
        var other = result.FindIndex(p => p.Directory == folder && p.Toolchain.Id == second);
        if (preferred > other && other >= 0)
        {
            var project = result[preferred];
            result.RemoveAt(preferred);
            result.Insert(other, project);
        }
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
