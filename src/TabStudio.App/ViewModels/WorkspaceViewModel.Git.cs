using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.SourceControl.Merging;

namespace TabStudio.App.ViewModels;

/// <summary>Source control in the workspace: the Git Changes window, comparison tabs and reloading files that Git rewrote.</summary>
public sealed partial class WorkspaceViewModel : ISourceControlHost
{
    /// <summary>The Git Changes window.</summary>
    public GitChangesViewModel GitChanges { get; }

    /// <inheritdoc />
    Task<bool> ISourceControlHost.SaveDocumentsAsync() => SaveAllCoreAsync();

    /// <inheritdoc />
    async Task ISourceControlHost.OpenFileAsync(string path) => await OpenDocumentAsync(path);

    /// <inheritdoc />
    void ISourceControlHost.ShowComparison(FileComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var document = DocumentViewModel.ForComparison(comparison, _profile);
        var earlier = Documents.FirstOrDefault(d => d.Comparison?.FilePath == comparison.FilePath && !d.IsReview);
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
    async Task<IReadOnlyDictionary<string, string>> ISourceControlHost.CaptureDocumentsAsync()
    {
        // What is on disk, not what the editor shows: a keystroke that lands while the save before the operation
        // finishes is not in the file, so it is one of the edits to keep, not part of the ancestor.
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in Documents.Where(d => !d.IsComparison).ToList())
        {
            var path = Path.GetFullPath(document.FilePath);
            if (texts.ContainsKey(path) || !File.Exists(path))
            {
                continue;
            }

            try
            {
                texts[path] = (await TextFile.ReadAsync(path)).Text;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not text the editor can show; such a tab is left as it is afterwards too.
            }
        }

        return texts;
    }

    /// <inheritdoc />
    async Task<int> ISourceControlHost.ReloadDocumentsAsync(IReadOnlyCollection<string> paths, IReadOnlyDictionary<string, string>? before)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var changed = paths.Select(Path.GetFullPath).ToHashSet(StringComparer.Ordinal);
        var conflicts = 0;
        foreach (var document in Documents.Where(d => !d.IsComparison && changed.Contains(Path.GetFullPath(d.FilePath))).ToList())
        {
            // Edits typed while the operation ran are in the editor only: the files were saved before it started.
            var path = Path.GetFullPath(document.FilePath);
            var ancestor = before is not null && before.TryGetValue(path, out var text) ? text : null;
            if (!File.Exists(document.FilePath))
            {
                if (EditedSince(document, ancestor))
                {
                    // The file is gone on this branch, but the tab holds work that exists nowhere else: it stays
                    // open and unsaved, and saving it brings the file back.
                    continue;
                }

                Documents.Remove(document);
                Detach(document);
                document.Dispose();
                continue;
            }

            try
            {
                var file = await TextFile.ReadAsync(document.FilePath);

                // Decided only now, after the last await, so a keystroke typed while the file was read counts.
                if (EditedSince(document, ancestor))
                {
                    var merge = TextMerge.Merge(ancestor!, document.Document.Text, file.Text);
                    document.MergeFromDisk(file.Text, merge.Text);
                    conflicts += merge.IsClean ? 0 : 1;
                }
                else
                {
                    document.ReloadFromDisk(file.Text);
                }
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

        return conflicts;
    }

    private static bool EditedSince(DocumentViewModel document, string? ancestor) =>
        ancestor is not null && !string.Equals(document.Document.Text, ancestor, StringComparison.Ordinal);

    /// <inheritdoc />
    void ISourceControlHost.ShowSourceControlStatus(IReadOnlyDictionary<string, GitFileStatus> files) => Explorer.SetSourceControlStatus(files);

    /// <inheritdoc />
    void ISourceControlHost.ReportStatus(string message) => StatusText = message;

    /// <summary>Shows the Git Changes window, as View > Git Changes does.</summary>
    [RelayCommand]
    private void ShowGitChanges() => SelectSideBarPanel(SideBarPanel.GitChanges);

    /// <summary>Shows the Git Changes window with the cursor in the commit message, as Git > Commit does.</summary>
    [RelayCommand]
    private void StartCommit()
    {
        ShowGitChanges();
        GitChanges.FocusCommitMessage();
    }

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
