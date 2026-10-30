namespace TabStudio.Core.Workspaces;

/// <summary>Limits that keep opening a huge repository fast and bounded.</summary>
public sealed record WorkspaceLoaderOptions
{
    /// <summary>The default options.</summary>
    public static WorkspaceLoaderOptions Default { get; } = new();

    /// <summary>How deep to look for project files when a folder without a solution is opened.</summary>
    public int ProjectSearchDepth { get; init; } = 4;

    /// <summary>The most files and folders the tree will contain. Anything beyond is left out.</summary>
    public int MaxTreeEntries { get; init; } = 20_000;
}

/// <summary>
/// Opens a solution, a project or a folder and builds the <see cref="Workspace"/> the IDE works with.
/// </summary>
/// <remarks>
/// <para>What gets opened depends on the path:</para>
/// <list type="bullet">
///   <item>A <c>.sln</c> or <c>.slnx</c> file opens that solution.</item>
///   <item>A project file opens that single project.</item>
///   <item>A folder opens the solution at its top level if there is one, otherwise every project found
///   within <see cref="WorkspaceLoaderOptions.ProjectSearchDepth"/> levels, otherwise the plain folder.</item>
/// </list>
/// <para>A project that fails to parse does not stop the rest from loading; it is reported in <see cref="Workspace.LoadErrors"/>.</para>
/// </remarks>
public static class WorkspaceLoader
{
    /// <summary>Opens a solution, a project or a folder.</summary>
    /// <param name="path">The path to open.</param>
    /// <param name="options">Limits for the scan, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ProjectLoadException">The path does not exist, or the solution itself cannot be parsed.</exception>
    public static Workspace Load(string path, WorkspaceLoaderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        options ??= WorkspaceLoaderOptions.Default;

        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            if (SolutionParser.IsSolutionFile(fullPath))
            {
                return LoadSolution(SolutionParser.Load(fullPath), options);
            }

            if (ProjectParser.IsProjectFile(fullPath))
            {
                return LoadProjects(Path.GetFileNameWithoutExtension(fullPath), Path.GetDirectoryName(fullPath)!, [fullPath], options);
            }

            throw new ProjectLoadException(fullPath, "it is not a solution, a project or a folder.", null);
        }

        if (!Directory.Exists(fullPath))
        {
            throw new ProjectLoadException(fullPath, "the path does not exist.", null);
        }

        var solution = FindSolution(fullPath) ?? FindNestedSolution(fullPath);
        if (solution is not null)
        {
            return LoadSolution(SolutionParser.Load(solution), options);
        }

        var projects = FindProjects(fullPath, options.ProjectSearchDepth);
        var name = new DirectoryInfo(fullPath).Name;
        return projects.Count > 0
            ? LoadProjects(name, fullPath, projects, options)
            : LoadFolder(name, fullPath, options);
    }

    /// <summary>
    /// Finds the solution file at the top level of a folder, preferring <c>.slnx</c> over <c>.sln</c>. When a folder has
    /// several, as repositories with a main solution and smaller ones for tools often do, it picks the main one: the
    /// solution named after the folder, otherwise the one with the most projects, otherwise the first by name.
    /// </summary>
    /// <param name="directory">The folder to look in.</param>
    /// <returns>The solution path, or <see langword="null"/> when there is none.</returns>
    public static string? FindSolution(string directory)
    {
        foreach (var extension in SolutionParser.SolutionExtensions)
        {
            var candidates = Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly)
                .Where(f => string.Equals(Path.GetExtension(f), extension, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (candidates.Count <= 1)
            {
                if (candidates.Count == 1)
                {
                    return candidates[0];
                }

                continue;
            }

            var folderName = new DirectoryInfo(directory).Name;
            return candidates.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), folderName, StringComparison.OrdinalIgnoreCase))
                ?? candidates.OrderByDescending(CountProjects).First();
        }

        return null;
    }

    /// <summary>
    /// The solution of a repository that keeps it in a folder, as blazor-workshop does in src/ beside eight copies
    /// of the app in save-points/, and Avalonia.Samples in src/Avalonia.Samples/: opened as a folder, its copies were
    /// loose projects with clashing names and the wrong one started. Only for a folder with nothing of its own to
    /// open (no project, and no other language's project file at its top), from the shallowest folder that has one,
    /// within two levels and outside samples, examples, docs, tests and benchmarks.
    /// </summary>
    /// <param name="directory">The folder to look in.</param>
    /// <returns>The solution path, or <see langword="null"/> when there is none or the folder has its own projects.</returns>
    public static string? FindNestedSolution(string directory)
    {
        try
        {
            if (Directory.EnumerateFiles(directory).Any(f => ProjectParser.IsProjectFile(f) || OtherProjectMarkers.Contains(Path.GetFileName(f))))
            {
                return null;
            }

            var level = new List<string> { directory };
            for (var depth = 1; depth <= 2 && level.Count > 0; depth++)
            {
                level = [.. level.SelectMany(SubfoldersToSearch)];
                var found = level.Select(FindSolution).OfType<string>().ToList();
                if (found.Count > 0)
                {
                    var folderName = new DirectoryInfo(directory).Name;
                    return found.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), folderName, StringComparison.OrdinalIgnoreCase))
                        ?? found.OrderByDescending(CountProjects).ThenBy(f => f, StringComparer.OrdinalIgnoreCase).First();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder opens as it is.
        }

        return null;

        static IEnumerable<string> SubfoldersToSearch(string folder) =>
            Directory.EnumerateDirectories(folder)
                .Where(d => !PathUtilities.IsIgnoredDirectory(Path.GetFileName(d)) && !SecondarySolutionFolders.Contains(Path.GetFileName(d)))
                .Order(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Files that make a folder another language's project, which a nested .NET solution must not hide.</summary>
    private static readonly HashSet<string> OtherProjectMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "pyproject.toml", "requirements.txt", "setup.py", "go.mod", "Cargo.toml", "CMakeLists.txt", "Makefile",
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "composer.json", "Package.swift",
        "pubspec.yaml", "platformio.ini", "index.html",
    };

    /// <summary>Folders whose solutions are a repository's extras rather than its main one.</summary>
    private static readonly HashSet<string> SecondarySolutionFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "samples", "sample", "examples", "example", "docs", "doc", "test", "tests", "benchmarks", "benchmark", "tools", "build",
    };

    /// <summary>The number of projects in a solution, or 0 when it cannot be read.</summary>
    private static int CountProjects(string solutionPath)
    {
        try
        {
            return SolutionParser.Load(solutionPath).Projects.Count;
        }
        catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static Workspace LoadSolution(SolutionInfo solution, WorkspaceLoaderOptions options)
    {
        var errors = new List<string>();
        var projects = new List<ProjectInfo>();
        var budget = new EntryBudget(options.MaxTreeEntries);

        var root = new WorkspaceNode(WorkspaceNodeKind.Solution, solution.Name, solution.FilePath);
        var folders = new Dictionary<string, WorkspaceNode>(StringComparer.OrdinalIgnoreCase);
        var projectDirectories = solution.Projects
            .Select(p => Path.GetDirectoryName(p.AbsolutePath)!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entry in solution.Projects)
        {
            var project = TryLoadProject(entry.AbsolutePath, errors);
            if (!entry.IsBuilt)
            {
                project = project with { IsBuiltBySolution = false };
            }

            projects.Add(project);

            var parent = GetOrCreateSolutionFolder(root, folders, entry.FolderPath);
            parent.Add(BuildProjectNode(project, projectDirectories, budget));
        }

        return new Workspace
        {
            Name = solution.Name,
            RootDirectory = solution.DirectoryPath,
            Solution = solution,
            Projects = projects,
            Tree = root,
            LoadErrors = errors,
        };
    }

    private static Workspace LoadProjects(string name, string rootDirectory, IReadOnlyList<string> projectPaths, WorkspaceLoaderOptions options)
    {
        var errors = new List<string>();
        var budget = new EntryBudget(options.MaxTreeEntries);
        var projects = projectPaths.Select(p => TryLoadProject(p, errors)).ToList();
        var projectDirectories = projects.Select(p => p.DirectoryPath).ToHashSet(StringComparer.Ordinal);

        var root = new WorkspaceNode(WorkspaceNodeKind.Root, name, rootDirectory);
        foreach (var project in projects)
        {
            root.Add(BuildProjectNode(project, projectDirectories, budget));
        }

        return new Workspace
        {
            Name = name,
            RootDirectory = rootDirectory,
            Projects = projects,
            Tree = root,
            LoadErrors = errors,
        };
    }

    private static Workspace LoadFolder(string name, string directory, WorkspaceLoaderOptions options)
    {
        var root = new WorkspaceNode(WorkspaceNodeKind.Root, name, directory);
        AddFileSystemChildren(root, directory, new HashSet<string>(StringComparer.Ordinal), new EntryBudget(options.MaxTreeEntries));

        return new Workspace
        {
            Name = name,
            RootDirectory = directory,
            Tree = root,
        };
    }

    private static ProjectInfo TryLoadProject(string projectPath, List<string> errors)
    {
        try
        {
            return ProjectParser.Load(projectPath);
        }
        catch (Exception ex) when (ex is ProjectLoadException or IOException or UnauthorizedAccessException)
        {
            errors.Add(ex.Message);
            return new ProjectInfo
            {
                Name = Path.GetFileNameWithoutExtension(projectPath),
                FilePath = projectPath,
                Kind = ProjectKind.Unknown,
            };
        }
    }

    private static WorkspaceNode GetOrCreateSolutionFolder(WorkspaceNode root, Dictionary<string, WorkspaceNode> folders, string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return root;
        }

        var parent = root;
        var current = string.Empty;
        foreach (var segment in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.Length == 0 ? segment : current + "/" + segment;
            if (!folders.TryGetValue(current, out var folder))
            {
                folder = parent.Add(new WorkspaceNode(WorkspaceNodeKind.SolutionFolder, segment));
                folders[current] = folder;
            }

            parent = folder;
        }

        return parent;
    }

    private static WorkspaceNode BuildProjectNode(ProjectInfo project, HashSet<string> projectDirectories, EntryBudget budget)
    {
        var node = new WorkspaceNode(WorkspaceNodeKind.Project, project.Name, project.FilePath, project);

        if (project.PackageReferences.Count > 0 || project.ProjectReferences.Count > 0)
        {
            var dependencies = node.Add(new WorkspaceNode(WorkspaceNodeKind.Dependencies, "Dependencies"));
            foreach (var reference in project.ProjectReferences)
            {
                dependencies.Add(new WorkspaceNode(WorkspaceNodeKind.ProjectReference, Path.GetFileNameWithoutExtension(reference), reference));
            }

            foreach (var package in project.PackageReferences.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var label = package.Version is null ? package.Name : $"{package.Name} ({package.Version})";
                dependencies.Add(new WorkspaceNode(WorkspaceNodeKind.Package, label));
            }
        }

        if (Directory.Exists(project.DirectoryPath))
        {
            // Other projects nested inside this one show up as their own project nodes, not as folders here.
            var otherProjects = new HashSet<string>(projectDirectories.Where(d => d != project.DirectoryPath), StringComparer.Ordinal);
            AddFileSystemChildren(node, project.DirectoryPath, otherProjects, budget, project.FilePath);
        }

        return node;
    }

    private static void AddFileSystemChildren(WorkspaceNode parent, string directory, HashSet<string> excludedDirectories, EntryBudget budget, string? excludedFile = null)
    {
        IEnumerable<string> subdirectories;
        IEnumerable<string> files;
        try
        {
            subdirectories = Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase).ToList();
            files = Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var subdirectory in subdirectories)
        {
            var name = Path.GetFileName(subdirectory);
            if (PathUtilities.IsIgnoredDirectory(name) || excludedDirectories.Contains(subdirectory) || !budget.TryTake())
            {
                continue;
            }

            var folder = parent.Add(new WorkspaceNode(WorkspaceNodeKind.Folder, name, subdirectory));
            AddFileSystemChildren(folder, subdirectory, excludedDirectories, budget);
        }

        foreach (var file in files)
        {
            if (string.Equals(file, excludedFile, StringComparison.Ordinal) || !budget.TryTake())
            {
                continue;
            }

            parent.Add(new WorkspaceNode(WorkspaceNodeKind.File, Path.GetFileName(file), file));
        }
    }

    private static List<string> FindProjects(string directory, int depth)
    {
        var result = new List<string>();
        Search(directory, depth);
        return result;

        void Search(string current, int remaining)
        {
            try
            {
                result.AddRange(Directory.EnumerateFiles(current)
                    .Where(ProjectParser.IsProjectFile)
                    .Order(StringComparer.OrdinalIgnoreCase));

                if (remaining == 0)
                {
                    return;
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(current).Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(subdirectory)))
                    {
                        Search(subdirectory, remaining - 1);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable folders are skipped; the rest of the workspace still opens.
            }
        }
    }

    /// <summary>Counts tree entries so that a huge repository cannot exhaust memory.</summary>
    private sealed class EntryBudget(int limit)
    {
        private int _remaining = limit;

        public bool TryTake()
        {
            if (_remaining <= 0)
            {
                return false;
            }

            _remaining--;
            return true;
        }
    }
}
