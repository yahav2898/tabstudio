namespace TabStudio.Core.Workspaces;

/// <summary>
/// Works out which source files belong to a project, the way the .NET SDK does.
/// </summary>
/// <remarks>
/// <para>
/// SDK-style projects include every source file under the project folder by default, except build output
/// and the folders of other projects. On top of that, a project can add files with
/// <c>&lt;Compile Include="..." /&gt;</c>, remove them with <c>&lt;Compile Remove="..." /&gt;</c>, or turn the
/// defaults off with <c>EnableDefaultCompileItems</c>.
/// </para>
/// </remarks>
public static class ProjectSourceFiles
{
    /// <summary>Lists the source files of a project, sorted by path.</summary>
    /// <param name="project">The project.</param>
    /// <param name="otherProjectDirectories">Folders of other projects in the workspace, which are never included.</param>
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
            foreach (var file in EnumerateDirectory(project.DirectoryPath, extension, excluded))
            {
                files.Add(file);
            }
        }

        foreach (var include in project.CompileIncludes)
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
                foreach (var file in EnumerateDirectory(pattern.BaseDirectory, extension, excluded).Where(pattern.IsMatch))
                {
                    files.Add(file);
                }
            }
        }

        var removals = project.CompileRemoves.Select(r => new GlobPattern(project.DirectoryPath, r)).ToList();
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
                files.UnionWith(EnumerateDirectory(pattern.BaseDirectory, extension: null, []).Where(pattern.IsMatch));
            }
        }

        var removals = removes.Select(r => new GlobPattern(project.DirectoryPath, r)).ToList();
        return files.Where(f => !removals.Any(r => r.IsMatch(f))).ToList();
    }

    /// <summary>The files under a folder with an extension, or all of them, skipping build output and excluded folders.</summary>
    private static IEnumerable<string> EnumerateDirectory(string directory, string? extension, List<string> excludedPrefixes)
    {
        var pending = new Stack<string>();
        pending.Push(directory);

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
                if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(subdirectory)) &&
                    !excludedPrefixes.Any(p => withSeparator.StartsWith(p, StringComparison.Ordinal)))
                {
                    pending.Push(subdirectory);
                }
            }
        }
    }
}
