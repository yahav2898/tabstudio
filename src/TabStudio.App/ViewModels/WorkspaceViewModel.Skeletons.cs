using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Memory on compact tablets: only the projects of the open documents keep a full compiler model, and the
/// projects they reference are seen through compact metadata images.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private HashSet<string> _activeProjects = new(StringComparer.Ordinal);

    /// <summary>The projects the language service keeps in full, for tests.</summary>
    internal IReadOnlyCollection<string> ActiveProjects => _activeProjects;

    /// <summary>The language service, for tests.</summary>
    internal ILanguageService? LanguageService => _language;

    /// <summary>Tells the language service which projects have open documents, when that changed.</summary>
    private void UpdateActiveProjects()
    {
        if (!_profile.UseSkeletonReferences || _language is not { } language || Workspace is not { } workspace)
        {
            return;
        }

        var active = Documents
            .Select(d => workspace.FindProjectForFile(d.FilePath)?.FilePath)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (active.SetEquals(_activeProjects))
        {
            return;
        }

        _activeProjects = active;
        _ = Task.Run(() => language.SetActiveProjects(active));
    }
}
