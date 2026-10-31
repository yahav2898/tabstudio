namespace TabStudio.Core.Workspaces;

/// <summary>
/// Works out which source files belong to a project, the way the .NET SDK does.
/// </summary>
/// <remarks>
/// <para>
/// SDK-style projects include every source file under the project folder by default, except build output
/// and the folders of other projects. On top of that, a project can add files with
/// <c>&lt;Compile Include="..." /&gt;</c> (less the files its <c>Exclude</c> names), remove them with
/// <c>&lt;Compile Remove="..." /&gt;</c>, or turn the defaults off with <c>EnableDefaultCompileItems</c>.
/// </para>
/// <para>
/// An explicit include is taken as written, as MSBuild takes it: a pattern such as Dock's
/// <c>..\Dock.Model.ReactiveUI\**\*.cs</c> that points into another project's folder gets that folder's files,
/// nested folders included. Only build output (<c>bin</c> and <c>obj</c>) is skipped under it, which such items
/// usually exclude themselves. The default items also leave out other projects' folders and the tool and hidden
/// folders <see cref="PathUtilities.IsIgnoredDirectory"/> names.
/// </para>
/// </remarks>
public static class ProjectSourceFiles
{
    /// <summary>Lists the source files of a project, sorted by path.</summary>
    /// <param name="project">The project.</param>
    /// <param name="otherProjectDirectories">
    /// Folders of other projects in the workspace, which the default items leave out; explicit
    /// <c>&lt;Compile Include="..." /&gt;</c> items can still include their files.
    /// </param>
    /// <param name="extension">The source file extension, <c>.cs</c> for C#.</param>
    public static IReadOnlyList<string> Enumerate(ProjectInfo project, IEnumerable<string>? otherProjectDirectories = null, string extension = ".cs")
    {
        ArgumentNullException.ThrowIfNull(project);

        var excluded = (otherProjectDirectories ?? [])
            .Where(d => !string.Equals(d, project.DirectoryPath, StringComparison.Ordinal))
            .Select(d => d.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToList();

        var files = new SortedSet<string>(StringComparer.Ordinal);

        var defaultsEnabled = !string.Equals(project.GetProperty("EnableDefaultCompileItems"), "false", StringComparison.OrdinalIgnoreCase);
        if (defaultsEnabled && Directory.Exists(project.DirectoryPath))
        {
            foreach (var file in EnumerateDirectory(project.DirectoryPath, extension, excluded, PathUtilities.IsIgnoredDirectory))
            {
                files.Add(file);
            }
        }

        // Avalonia's XAML: the .axaml files in the folder, and the project's own AvaloniaXaml items from elsewhere.
        var axaml = string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase);
        var includes = axaml ? project.CompileIncludes.Concat(project.AvaloniaXamlIncludes) : project.CompileIncludes;
        foreach (var include in includes)
        {
            var exclusions = (project.CompileIncludeExcludes.TryGetValue(include, out var excludes) || (axaml && project.AvaloniaXamlIncludeExcludes.TryGetValue(include, out excludes)))
                ? excludes.Select(e => new GlobPattern(project.DirectoryPath, e)).ToList()
                : [];
            var pattern = new GlobPattern(project.DirectoryPath, include);
            if (!pattern.HasWildcards)
            {
                if (PathUtilities.FindFile(pattern.FullPattern) is { } existing && !exclusions.Any(e => e.IsMatch(existing)))
                {
                    files.Add(existing);
                }

                continue;
            }

            if (Directory.Exists(pattern.BaseDirectory))
            {
                var matches = EnumerateDirectory(pattern.BaseDirectory, extension, [], IsBuildOutput)
                    .Where(f => pattern.IsMatch(f) && !exclusions.Any(e => e.IsMatch(f)));
                foreach (var file in matches)
                {
                    files.Add(file);
                }
            }
        }

        var removals = (axaml ? project.CompileRemoves.Concat(project.AvaloniaXamlRemoves) : project.CompileRemoves).Select(r => new GlobPattern(project.DirectoryPath, r)).ToList();
        return files.Where(f => !removals.Any(r => r.IsMatch(f))).ToList();
    }

    /// <summary>
    /// Lists the files of a project's items of one type that have no default files, such as
    /// <c>&lt;EmbeddedResource Include="Data\**" /&gt;</c>, sorted by path. Build output is never included.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="includes">The items' <c>Include</c> patterns, relative to the project folder.</param>
    /// <param name="removes">The items' <c>Remove</c> patterns.</param>
    public static IReadOnlyList<string> EnumerateItems(ProjectInfo project, IReadOnlyList<string> includes, IReadOnlyList<string> removes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(includes);
        ArgumentNullException.ThrowIfNull(removes);

        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var include in includes)
        {
            var pattern = new GlobPattern(project.DirectoryPath, include);
            if (!pattern.HasWildcards)
            {
                if (PathUtilities.FindFile(pattern.FullPattern) is { } existing)
                {
                    files.Add(existing);
                }

                continue;
            }

            if (Directory.Exists(pattern.BaseDirectory))
            {
                files.UnionWith(EnumerateDirectory(pattern.BaseDirectory, extension: null, [], PathUtilities.IsIgnoredDirectory).Where(pattern.IsMatch));
            }
        }

        var removals = removes.Select(r => new GlobPattern(project.DirectoryPath, r)).ToList();
        return files.Where(f => !removals.Any(r => r.IsMatch(f))).ToList();
    }

    /// <summary>Whether a folder holds build output, the only folders an explicit include leaves out under its pattern.</summary>
    private static bool IsBuildOutput(string directoryName) =>
        string.Equals(directoryName, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(directoryName, "obj", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The files under a folder with an extension, or all of them, skipping excluded folders and the folders whose
    /// names <paramref name="skip"/> picks, such as build output, tool folders and hidden ones
    /// (<see cref="PathUtilities.IsIgnoredDirectory"/>).
    /// </summary>
    /// <summary>
    /// How many folders one walk goes through at most: a pattern such as <c>/**/*.cs</c>, or a link to <c>/</c>, otherwise
    /// walked the whole device.
    /// </summary>
    private const int MaximumDirectories = 20_000;

    private static IEnumerable<string> EnumerateDirectory(string directory, string? extension, List<string> excludedPrefixes, Func<string, bool> skip)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        var visited = new HashSet<string>(StringComparer.Ordinal) { PathUtilities.RealDirectoryPath(directory) };

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] files;
            string[] subdirectories;
            try
            {
                files = Directory.GetFiles(current, "*" + extension);
                subdirectories = Directory.GetDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (extension is null || string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }

            foreach (var subdirectory in subdirectories)
            {
                var withSeparator = subdirectory + Path.DirectorySeparatorChar;
                if (!skip(Path.GetFileName(subdirectory)) &&
                    !excludedPrefixes.Any(p => withSeparator.StartsWith(p, StringComparison.Ordinal)) &&
                    visited.Count < MaximumDirectories &&
                    PathUtilities.TryVisitDirectory(subdirectory, visited))
                {
                    pending.Push(subdirectory);
                }
            }
        }
    }
}
