using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Ci;
using TabStudio.App.ViewModels.Actions;

namespace TabStudio.App.ViewModels;

/// <summary>
/// GitHub Actions in the workspace: the Actions tool window, the status bar's CI item, job log tabs, and CI's
/// errors in the Error List. The window follows the Git Changes window's link to GitHub.
/// </summary>
public sealed partial class WorkspaceViewModel : IActionsHost
{
    /// <summary>The app activity the workspace made itself, when the platform gave none, to dispose with it.</summary>
    private AvaloniaAppActivity? _ownAppActivity;

    /// <summary>The GitHub Actions window.</summary>
    public ActionsViewModel Actions { get; private set; } = null!;

    /// <summary>Whether the side bar shows the GitHub Actions window.</summary>
    public bool IsActionsSelected => SelectedSideBarPanel == SideBarPanel.Actions;

    /// <inheritdoc />
    string? IActionsHost.WorkspaceRoot => Workspace?.RootDirectory;

    /// <inheritdoc />
    void IActionsHost.ShowActions() => SelectSideBarPanel(SideBarPanel.Actions);

    /// <inheritdoc />
    CiLogViewModel IActionsHost.ShowLog(CiLogViewModel log, string tabPath)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (Documents.FirstOrDefault(d => d.IsCiLog && string.Equals(d.FilePath, tabPath, StringComparison.Ordinal)) is { CiLog: { } shown } open)
        {
            ActiveDocument = open;
            return shown;
        }

        var document = DocumentViewModel.ForCiLog(log, tabPath, _profile);
        Attach(document);
        Documents.Insert(ActiveDocument is null ? Documents.Count : Documents.IndexOf(ActiveDocument) + 1, document);
        ActiveDocument = document;
        return log;
    }

    /// <inheritdoc />
    void IActionsHost.ShowCiDiagnostics(IReadOnlyList<CiDiagnostic> diagnostics) => ErrorList.SetCiDiagnostics(diagnostics);

    /// <inheritdoc />
    string? IActionsHost.ProjectOf(string fullPath) => Workspace?.FindProjectForFile(fullPath)?.Name;

    /// <inheritdoc />
    void IActionsHost.ReportStatus(string message) => StatusText = message;

    /// <summary>Shows the GitHub Actions window, as View > GitHub Actions and Git > GitHub Actions do.</summary>
    [RelayCommand]
    private void ShowActions() => SelectSideBarPanel(SideBarPanel.Actions);

    /// <summary>Creates the Actions window over the Git Changes window's account, and follows its link.</summary>
    private void InitializeActions(IStoragePicker? browser, INotifications? notifications, IAppActivity? appActivity)
    {
        Actions = new ActionsViewModel(
            GitChanges.Account,
            this,
            browser ?? new TopLevelStoragePicker(),
            new ActionsSettingsStore(_paths),
            notifications ?? new NoSystemNotifications(),
            appActivity ?? (_ownAppActivity = new AvaloniaAppActivity()));
        GitChanges.PropertyChanged += OnGitChangesPropertyChangedForActions;
        PropertyChanged += OnPropertyChangedForActions;
        Actions.SetWorkspaceLink(GitChanges.Link);
    }

    private void DisposeActions()
    {
        GitChanges.PropertyChanged -= OnGitChangesPropertyChangedForActions;
        PropertyChanged -= OnPropertyChangedForActions;
        Actions.Dispose();
        _ownAppActivity?.Dispose();
    }

    private void OnGitChangesPropertyChangedForActions(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitChangesViewModel.Link))
        {
            Actions.SetWorkspaceLink(GitChanges.Link);
        }
    }

    private void OnPropertyChangedForActions(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectedSideBarPanel) or nameof(IsSideBarVisible))
        {
            Actions.IsShown = IsSideBarVisible && IsActionsSelected;
        }
    }
}
