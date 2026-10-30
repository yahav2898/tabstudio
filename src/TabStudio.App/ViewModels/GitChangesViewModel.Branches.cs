using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;

namespace TabStudio.App.ViewModels;

/// <summary>The branch picker: switching branches and creating new ones.</summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>The repository's branches, in the branch picker.</summary>
    public ObservableCollection<GitBranchItemViewModel> Branches { get; } = [];

    /// <summary>Whether the branch picker is shown.</summary>
    [ObservableProperty]
    public partial bool IsBranchPickerOpen { get; private set; }

    /// <summary>Whether the branch list is loading.</summary>
    [ObservableProperty]
    public partial bool IsLoadingBranches { get; private set; }

    /// <summary>Why the branch list could not be loaded.</summary>
    [ObservableProperty]
    public partial string? BranchesErrorText { get; private set; }

    [RelayCommand(CanExecute = nameof(CanUseGitHub))]
    private async Task OpenBranchPickerAsync()
    {
        if (_sync is not { } sync)
        {
            return;
        }

        Branches.Clear();
        BranchesErrorText = null;
        IsBranchPickerOpen = true;
        IsLoadingBranches = true;
        try
        {
            var branches = await sync.GetBranchesAsync(_lifetime.Token);
            _repository ??= await Account.Account.CreateClient().GetRepositoryAsync(sync.Link.Owner, sync.Link.Repository, _lifetime.Token);
            var defaultBranch = _repository.DefaultBranch;
            foreach (var branch in branches
                .OrderByDescending(b => b.Name == defaultBranch)
                .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
            {
                Branches.Add(new GitBranchItemViewModel(branch, branch.Name == sync.Link.Branch, branch.Name == defaultBranch, SwitchBranchCommand));
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            BranchesErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsLoadingBranches = false;
        }
    }

    [RelayCommand]
    private void CloseBranchPicker() => IsBranchPickerOpen = false;

    [RelayCommand]
    private async Task SwitchBranchAsync(GitBranchItemViewModel? branch)
    {
        IsBranchPickerOpen = false;
        if (branch is null || branch.IsCurrent || !await SaveBeforeChangingFilesAsync())
        {
            return;
        }

        await RunAsync(async (sync, progress, token) =>
        {
            var before = sync.Metadata.Files.Keys.ToList();
            var documents = await _host.CaptureDocumentsAsync();
            var result = await sync.SwitchBranchAsync(branch.Name, progress, token);
            PullRequest = null;
            CanCreatePullRequest = false;
            IncomingCommits = 0;
            var conflicts = await _host.ReloadDocumentsAsync(FullPaths(before.Concat(result.Updated)), documents);
            await _host.ReloadWorkspaceAsync();
            return $"Switched to {branch.Name}." + TypedEditsNote(conflicts);
        });
    }

    [RelayCommand(CanExecute = nameof(CanUseGitHub))]
    private async Task NewBranchAsync()
    {
        IsBranchPickerOpen = false;
        if (_sync is not { } sync)
        {
            return;
        }

        var from = sync.Link.Branch ?? sync.Link.BaseCommit[..Math.Min(7, sync.Link.BaseCommit.Length)];
        var name = await _host.Prompt.AskAsync(
            "New Branch",
            $"Creates the branch on GitHub from {from} and switches to it. Your changes stay, and go into the branch's next commit.",
            "Create");
        if (name is null)
        {
            return;
        }

        if (!BranchNames.IsValid(name, out var reason))
        {
            ErrorText = reason;
            return;
        }

        await RunAsync(async (current, _, token) =>
        {
            await current.CreateBranchAsync(name, token);
            PullRequest = null;
            CanCreatePullRequest = false;
            IncomingCommits = 0;
            return $"Created {name} on GitHub and switched to it.";
        });
    }
}
