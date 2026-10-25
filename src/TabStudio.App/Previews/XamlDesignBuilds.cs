using TabStudio.App.Services;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Previews;

/// <summary>
/// The builds the XAML designer previews with, one per Avalonia project: made in the background the first time a
/// XAML file of the project is designed and again after one of its other files is saved, without the Output panel.
/// </summary>
/// <remarks>
/// Every build stays loaded for as long as TabStudio runs, so a project is only built again when its code changed,
/// never while XAML is typed: the XAML being edited is compiled on its own, against the last build.
/// </remarks>
internal sealed class XamlDesignBuilds
{
    private readonly ILanguageService _language;
    private readonly Func<Task> _packagesRestored;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Creates the builds of a workspace.</summary>
    /// <param name="language">Builds the projects.</param>
    /// <param name="packagesRestored">Completes once the workspace's packages are restored, which builds wait for.</param>
    public XamlDesignBuilds(ILanguageService language, Func<Task> packagesRestored)
    {
        _language = language;
        _packagesRestored = packagesRestored;
    }

    /// <summary>Raised on the UI thread when a project has a new build, or its build failed; the argument is the project file.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>
    /// The project's latest build: the one already made, or a new one when there is none or the project changed.
    /// UI thread only.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The build, or <see langword="null"/> when the project has never built.</returns>
    public Task<XamlDesignBuild?> GetAsync(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!_entries.TryGetValue(project.FilePath, out var entry))
        {
            entry = new Entry();
            _entries[project.FilePath] = entry;
        }

        if (entry.Pending is null || entry.IsStale)
        {
            entry.IsStale = false;
            entry.Pending = BuildAsync(project, entry);
        }

        return entry.Pending;
    }

    /// <summary>The errors of the project's last build when it failed, such as "CS1002: ; expected".</summary>
    /// <param name="project">The project.</param>
    public string? BuildError(ProjectInfo project) => _entries.GetValueOrDefault(project.FilePath)?.Error;

    /// <summary>Marks a project as changed, after one of its files was saved; designers then ask for a new build.</summary>
    /// <param name="project">The project.</param>
    public void Invalidate(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (_entries.TryGetValue(project.FilePath, out var entry))
        {
            entry.IsStale = true;
            Changed?.Invoke(this, project.FilePath);
        }
    }

    private async Task<XamlDesignBuild?> BuildAsync(ProjectInfo project, Entry entry)
    {
        var previous = entry.Pending is { IsCompletedSuccessfully: true } done ? done.Result : null;
        try
        {
            await _packagesRestored();
            var build = await _language.BuildAsync(project, CancellationToken.None);
            if (build is not { Success: true })
            {
                entry.Error = string.Join(Environment.NewLine, build.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
                    .Take(3)
                    .Select(d => d.ToString()));
                return previous;
            }

            entry.Error = null;
            return XamlDesignBuild.Load(build, _language.GetPackageRuntimeAssemblies(project.FilePath));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            entry.Error = ex.Message;
            return previous;
        }
    }

    private sealed class Entry
    {
        public Task<XamlDesignBuild?>? Pending { get; set; }

        public bool IsStale { get; set; }

        public string? Error { get; set; }
    }
}
