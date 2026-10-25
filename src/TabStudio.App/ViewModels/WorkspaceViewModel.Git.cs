using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The tool windows that share the side bar, as tabs.</summary>
public enum SideBarPanel
{
    /// <summary>The Solution Explorer, with Properties below it.</summary>
    SolutionExplorer,

    /// <summary>The Git Changes window.</summary>
    GitChanges,
}

/// <summary>Source control in the workspace: the Git Changes window, comparison tabs and reloading files that Git rewrote.</summary>
public sealed partial class WorkspaceViewModel : ISourceControlHost
{
    /// <summary>The Git Changes window.</summary>
    public GitChangesViewModel GitChanges { get; }

    /// <summary>The side bar's visible tool window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolutionExplorerSelected), nameof(IsGitChangesSelected))]
    public partial SideBarPanel SelectedSideBarPanel { get; set; } = SideBarPanel.SolutionExplorer;

    /// <summary>Whether the Solution Explorer is the visible side bar tab.</summary>
    public bool IsSolutionExplorerSelected => SelectedSideBarPanel == SideBarPanel.SolutionExplorer;

    /// <summary>Whether the Git Changes window is the visible side bar tab.</summary>
    public bool IsGitChangesSelected => SelectedSideBarPanel == SideBarPanel.GitChanges;

    /// <inheritdoc />
    Task<bool> ISourceControlHost.SaveDocumentsAsync() => SaveAllCoreAsync();

    /// <inheritdoc />
    async Task ISourceControlHost.OpenFileAsync(string path) => await OpenDocumentAsync(path);

    /// <inheritdoc />
    void ISourceControlHost.ShowComparison(FileComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var document = DocumentViewModel.ForComparison(comparison, _profile);
        var earlier = Documents.FirstOrDefault(d => d.Comparison?.FilePath == comparison.FilePath);
        var index = earlier is not null ? Documents.IndexOf(earlier)
            : ActiveDocument is null ? Documents.Count
            : Documents.IndexOf(ActiveDocument) + 1;
        if (earlier is not null)
        {
            Documents.Remove(earlier);
            Detach(earlier);
            earlier.Dispose();
        }

        Attach(document);
        Documents.Insert(index, document);
        ActiveDocument = document;
    }

    /// <inheritdoc />
    async Task ISourceControlHost.ReloadDocumentsAsync(IReadOnlyCollection<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var changed = paths.Select(Path.GetFullPath).ToHashSet(StringComparer.Ordinal);
        foreach (var document in Documents.Where(d => !d.IsComparison && changed.Contains(Path.GetFullPath(d.FilePath))).ToList())
        {
            if (!File.Exists(document.FilePath))
            {
                Documents.Remove(document);
                Detach(document);
                document.Dispose();
                continue;
            }

            try
            {
                var file = await TextFile.ReadAsync(document.FilePath);
                document.ReloadFromDisk(file.Text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file is no longer text the editor can show; its tab keeps what it had.
            }
        }

        if (ActiveDocument is not null && !Documents.Contains(ActiveDocument))
        {
            ActiveDocument = Documents.FirstOrDefault();
        }
    }

    /// <inheritdoc />
    void ISourceControlHost.ShowSourceControlStatus(IReadOnlyDictionary<string, GitFileStatus> files) => Explorer.SetSourceControlStatus(files);

    /// <inheritdoc />
    void ISourceControlHost.ReportStatus(string message) => StatusText = message;

    [RelayCommand]
    private void SelectSideBarPanel(SideBarPanel panel)
    {
        SelectedSideBarPanel = panel;
        IsSideBarVisible = true;
    }

    /// <summary>Shows the Git Changes window, as View > Git Changes and Git > Commit do.</summary>
    [RelayCommand]
    private void ShowGitChanges() => SelectSideBarPanel(SideBarPanel.GitChanges);

    partial void OnSelectedSideBarPanelChanged(SideBarPanel value)
    {
        if (value == SideBarPanel.GitChanges && IsSideBarVisible)
        {
            GitChanges.OnShown();
        }
    }

    partial void OnIsSideBarVisibleChanged(bool value)
    {
        if (value && IsGitChangesSelected)
        {
            GitChanges.OnShown();
        }
    }

    partial void OnWorkspaceChanged(Workspace? oldValue, Workspace? newValue)
    {
        if (newValue is not null)
        {
            _ = GitChanges.AttachAsync(newValue.RootDirectory);
        }
    }

    /// <summary>Creates the Git Changes window; without the app's shared account, a signed-out one of its own.</summary>
    private GitChangesViewModel CreateGitChanges(GitHubAccountViewModel? gitHubAccount, IStoragePicker? browser)
    {
        var picker = browser ?? new TopLevelStoragePicker();
        return new GitChangesViewModel(gitHubAccount ?? new GitHubAccountViewModel(new GitHubAccount(_paths), picker), this, picker);
    }
}
