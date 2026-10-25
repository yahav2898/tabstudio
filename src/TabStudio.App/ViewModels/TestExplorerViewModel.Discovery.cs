using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The tests: found in the source of the test projects before a build and by the runner after one, merged into one
/// list in which every test keeps its result.
/// </summary>
public sealed partial class TestExplorerViewModel
{
    /// <summary>Sets the workspace's test projects, after it opened or reloaded. Tests of other projects go away.</summary>
    /// <param name="projects">The C# test projects.</param>
    public void SetProjects(IReadOnlyList<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        _projects.RemoveAll(known => !projects.Any(p => p.FilePath == known.FilePath));
        foreach (var project in projects)
        {
            if (Find(project.FilePath) is { } known)
            {
                known.Name = project.Name;
            }
            else
            {
                _projects.Add(new ProjectTests(project.FilePath, project.Name));
            }
        }

        HasTestProjects = _projects.Count > 0;
        MergeEntries();
        RefreshCommands();
    }

    /// <summary>Shows the tests found in a project's source.</summary>
    /// <param name="project">The test project.</param>
    /// <param name="classes">Its test classes.</param>
    public void SetSourceTests(ProjectInfo project, IReadOnlyList<SourceTestClass> classes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(classes);
        if (Find(project.FilePath) is not { } known)
        {
            return;
        }

        var entries = new List<TestEntry>();
        foreach (var type in classes)
        {
            foreach (var method in type.Methods)
            {
                if (method.DataRows.Count == 0)
                {
                    entries.Add(TestEntry.FromSource(known.FilePath, known.Name, type, method, null));
                }

                foreach (var row in method.DataRows)
                {
                    entries.Add(TestEntry.FromSource(known.FilePath, known.Name, type, method, row));
                }
            }
        }

        known.Source = entries;
        MergeEntries();
    }

    /// <summary>
    /// Shows the tests the runner found in a built project. Until the next build they take precedence over the
    /// source, which only adds the tests written since.
    /// </summary>
    /// <param name="project">The test project.</param>
    /// <param name="tests">Its tests.</param>
    public void SetBuiltTests(ProjectInfo project, IReadOnlyList<TestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(tests);
        if (Find(project.FilePath) is { } known)
        {
            known.Built = tests.Select(t => TestEntry.FromBuild(known.FilePath, known.Name, t)).ToList();
            MergeEntries();
        }
    }

    /// <summary>Marks the results of projects as out of date, because code they test was saved.</summary>
    /// <param name="projectFilePaths">The test projects.</param>
    public void MarkStale(IEnumerable<string> projectFilePaths)
    {
        ArgumentNullException.ThrowIfNull(projectFilePaths);
        var projects = projectFilePaths.ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var entry in _entries.Where(e => e.Result is not null && !e.IsStale && projects.Contains(e.ProjectFilePath)))
        {
            entry.IsStale = true;
            changed = true;
        }

        if (changed)
        {
            RefreshNodes(_entries);
        }
    }

    /// <summary>
    /// Combines each project's built and source tests into one list, keeping every test's result: the tests of the
    /// last build, plus tests written since. Tests removed since the build stay, stale, until the next build, as in
    /// Visual Studio.
    /// </summary>
    private void MergeEntries()
    {
        var previous = _byId.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var merged = new List<TestEntry>();
        foreach (var project in _projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (project.Built is { } built)
            {
                var ids = built.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
                var methods = built.Select(e => e.MethodFullName).ToHashSet(StringComparer.Ordinal);
                var methodLocations = project.Source.DistinctBy(e => e.MethodFullName).ToDictionary(e => e.MethodFullName, StringComparer.Ordinal);
                foreach (var entry in built)
                {
                    // The source's location is the method's name, which is where "Go To Test" should land.
                    if (methodLocations.TryGetValue(entry.MethodFullName, out var source))
                    {
                        entry.Location = source.Location;
                        entry.ClassLocation = source.ClassLocation;
                    }
                }

                merged.AddRange(built);
                merged.AddRange(project.Source.Where(e => !ids.Contains(e.Id) && !(e.IsPlaceholder && methods.Contains(e.MethodFullName))));
            }
            else
            {
                merged.AddRange(project.Source);
            }
        }

        _byId.Clear();
        foreach (var entry in merged)
        {
            var key = EntryKey(entry.ProjectFilePath, entry.Id);
            if (previous.TryGetValue(key, out var old) && !ReferenceEquals(old, entry))
            {
                entry.CarryOver(old);
            }

            _byId.TryAdd(key, entry);
        }

        _entries = merged;
        Rebuild();
    }

    /// <summary>What the Test Explorer knows about one test project.</summary>
    /// <param name="filePath">The project file.</param>
    /// <param name="name">The project name.</param>
    private sealed class ProjectTests(string filePath, string name)
    {
        /// <summary>The project file.</summary>
        public string FilePath { get; } = filePath;

        /// <summary>The project name.</summary>
        public string Name { get; set; } = name;

        /// <summary>The tests its source declares.</summary>
        public List<TestEntry> Source { get; set; } = [];

        /// <summary>The tests the runner found after its last build, or <see langword="null"/> before the first.</summary>
        public List<TestEntry>? Built { get; set; }
    }
}
