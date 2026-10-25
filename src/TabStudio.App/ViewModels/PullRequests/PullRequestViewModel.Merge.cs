using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>
/// Merging: the repository's allowed methods, the commit title and message GitHub would suggest, deleting the
/// branch, a confirmation step, and plain explanations when GitHub refuses.
/// </summary>
public sealed partial class PullRequestViewModel
{
    private RepositoryMergeSettings _mergeSettings = RepositoryMergeSettings.Default;
    private IReadOnlyList<GitCommit> _commits = [];

    /// <summary>The ways the repository allows merging.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MergeMethodOption> MergeMethods { get; private set; } = [];

    /// <summary>The chosen way to merge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommitMessage), nameof(MergeButtonText))]
    public partial MergeMethodOption? SelectedMergeMethod { get; set; }

    /// <summary>The merge or squashed commit's title.</summary>
    [ObservableProperty]
    public partial string MergeTitle { get; set; } = string.Empty;

    /// <summary>The merge or squashed commit's message.</summary>
    [ObservableProperty]
    public partial string MergeMessage { get; set; } = string.Empty;

    /// <summary>Whether to delete the head branch once merged.</summary>
    [ObservableProperty]
    public partial bool DeleteBranchAfterMerge { get; set; }

    /// <summary>Whether the merge waits for the user to confirm it.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingMerge { get; private set; }

    /// <summary>Whether GitHub is merging.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand), nameof(ConfirmMergeCommand), nameof(ReadyForReviewCommand))]
    public partial bool IsMerging { get; private set; }

    /// <summary>Why the merge failed, in plain words.</summary>
    [ObservableProperty]
    public partial string? MergeErrorText { get; private set; }

    /// <summary>Whether the merge panel shows: for open pull requests.</summary>
    public bool ShowsMergePanel => IsOpen;

    /// <summary>Whether it is a draft, which must be marked ready before it can be merged.</summary>
    public bool IsDraft => Info is { IsDraft: true, State: PullRequestState.Open };

    /// <summary>Whether the chosen method creates a commit with a title and message of its own; a rebase does not.</summary>
    public bool HasCommitMessage => SelectedMergeMethod?.Method is not MergeMethod.Rebase;

    /// <summary>"Squash and Merge".</summary>
    public string MergeButtonText => SelectedMergeMethod?.Method switch
    {
        MergeMethod.Squash => "Squash and Merge",
        MergeMethod.Rebase => "Rebase and Merge",
        _ => "Merge Pull Request",
    };

    /// <summary>Whether the head branch can be deleted: it is in this repository, and GitHub does not delete it on its own.</summary>
    public bool CanDeleteBranch => Info is { } info && !info.IsFromFork(Owner, Repository) && !_mergeSettings.DeleteBranchOnMerge;

    /// <summary>"GitHub deletes feature/login once it is merged.", when the repository does that itself.</summary>
    public string? AutomaticDeleteNote => _mergeSettings.DeleteBranchOnMerge ? $"GitHub deletes {HeadBranch} once it is merged." : null;

    /// <summary>The merge status line under the header.</summary>
    public string MergeabilityText => Details switch
    {
        null => string.Empty,
        { Info.State: PullRequestState.Merged } => $"Merged into {BaseBranch}.",
        { Info.State: PullRequestState.Closed } => "Closed without merging.",
        { Info.IsDraft: true } => "This pull request is a draft. Mark it ready for review to merge it.",
        { Mergeable: MergeableState.Conflicts } => $"This branch has conflicts with {BaseBranch} that must be resolved before merging.",
        { Mergeable: MergeableState.Blocked } => "Merging is blocked: the branch's rules require reviews or checks first.",
        { Mergeable: MergeableState.Behind } => $"This branch is behind {BaseBranch}, and the repository requires it to be up to date.",
        { Mergeable: MergeableState.Unstable } => "Some checks failed, but it can be merged.",
        { Mergeable: MergeableState.Unknown } => "GitHub is still checking whether this can be merged. Refresh in a moment.",
        _ => $"No conflicts with {BaseBranch}: this can be merged.",
    };

    /// <summary>How the merge status line reads.</summary>
    public MergeabilityKind MergeabilityKind => Details switch
    {
        null or { Mergeable: MergeableState.Unknown } => MergeabilityKind.Unknown,
        { Info.State: PullRequestState.Merged } => MergeabilityKind.Good,
        { Info.State: PullRequestState.Closed } or { Info.IsDraft: true } => MergeabilityKind.Warning,
        { Mergeable: MergeableState.Conflicts or MergeableState.Blocked or MergeableState.Behind } => MergeabilityKind.Blocked,
        { Mergeable: MergeableState.Unstable } => MergeabilityKind.Warning,
        _ => MergeabilityKind.Good,
    };

    /// <summary>Why the merge button is off, or <see langword="null"/> when it is on.</summary>
    public string? MergeBlockedText => Details switch
    {
        { Info.IsDraft: true } => "Drafts cannot be merged.",
        { Mergeable: MergeableState.Conflicts } => "Resolve the conflicts first: GitHub cannot merge them.",
        _ => null,
    };

    /// <summary>"Merged into main as 1a2b3c4.", once merged.</summary>
    public string? MergedText => Details is { Info.State: PullRequestState.Merged } details
        ? $"Merged into {BaseBranch}" + (details.MergeCommitSha is { Length: >= 7 } sha ? $" as {sha[..7]}." : ".")
        : null;

    /// <summary>Starts merging: asks the user to confirm first.</summary>
    [RelayCommand(CanExecute = nameof(CanMerge))]
    private void Merge()
    {
        MergeErrorText = null;
        IsConfirmingMerge = true;
    }

    [RelayCommand]
    private void CancelMerge() => IsConfirmingMerge = false;

    /// <summary>Merges the pull request, then deletes its branch when asked.</summary>
    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task ConfirmMergeAsync()
    {
        if (Info is not { } info || SelectedMergeMethod is not { } method || !RequireSignIn("merge"))
        {
            return;
        }

        IsConfirmingMerge = false;
        MergeErrorText = null;
        IsMerging = true;
        var token = _lifetime.Token;
        try
        {
            var client = _host.CreateClient();
            var commit = method.Method == MergeMethod.Rebase ? null : MergeTitle.Trim();
            var result = await client.MergePullRequestAsync(Owner, Repository, Number, method.Method, commit, MergeMessage.Trim(), info.Head.Sha, token);
            var deleted = DeleteBranchAfterMerge && CanDeleteBranch && await TryDeleteBranchAsync(client, info.Head.Name, token);
            InfoText = $"Merged pull request {NumberText} into {info.Base.Name} as {result.Sha[..Math.Min(7, result.Sha.Length)]}." + (deleted ? $" Deleted {info.Head.Name}." : string.Empty);
            _host.ReportStatus(InfoText);
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (GitHubApiException ex)
        {
            MergeErrorText = DescribeMergeFailure(ex);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            MergeErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsMerging = false;
        }
    }

    /// <summary>Takes a draft out of draft, through GitHub's GraphQL API.</summary>
    [RelayCommand(CanExecute = nameof(CanMarkReady))]
    private async Task ReadyForReviewAsync()
    {
        if (Info is not { } info || !RequireSignIn("mark it ready for review"))
        {
            return;
        }

        MergeErrorText = null;
        IsMerging = true;
        try
        {
            await _host.CreateClient().MarkReadyForReviewAsync(info.NodeId, _lifetime.Token);
            InfoText = $"Pull request {NumberText} is ready for review.";
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            MergeErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsMerging = false;
        }
    }

    partial void OnSelectedMergeMethodChanged(MergeMethodOption? value) => FillMergeMessage();

    private bool CanMerge() => IsOpen && !IsDraft && !IsMerging && SelectedMergeMethod is not null && Details?.Mergeable != MergeableState.Conflicts;

    private bool CanMarkReady() => IsDraft && !IsMerging;

    /// <summary>Takes the repository's merge settings, and offers its methods with GitHub's suggested messages.</summary>
    private void ApplyMergeSettings(RepositoryMergeSettings settings, IReadOnlyList<GitCommit> commits)
    {
        _mergeSettings = settings;
        _commits = commits;
        var previous = SelectedMergeMethod?.Method;
        MergeMethods = [.. settings.AllowedMethods.Select(m => MergeMethodOption.For(m, BaseBranch.Length > 0 ? BaseBranch : "the base branch"))];
        SelectedMergeMethod = MergeMethods.FirstOrDefault(m => m.Method == previous) ?? (MergeMethods.Count > 0 ? MergeMethods[0] : null);
        DeleteBranchAfterMerge = CanDeleteBranch;
        FillMergeMessage();
        OnPropertyChanged(nameof(AutomaticDeleteNote));
    }

    /// <summary>Fills in the commit title and message GitHub suggests for the chosen method.</summary>
    private void FillMergeMessage()
    {
        if (Info is { } info && SelectedMergeMethod is { } method)
        {
            var text = MergeMessages.Default(method.Method, info, _commits, _mergeSettings);
            MergeTitle = text.Title;
            MergeMessage = text.Message;
        }

        MergeCommand.NotifyCanExecuteChanged();
        ConfirmMergeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Reads the repository's merge settings; GitHub's defaults when the account may not see them.</summary>
    private async Task<RepositoryMergeSettings> GetMergeSettingsAsync(GitHubClient client, CancellationToken token)
    {
        try
        {
            return await client.GetMergeSettingsAsync(Owner, Repository, token);
        }
        catch (GitHubApiException)
        {
            return RepositoryMergeSettings.Default;
        }
    }

    /// <summary>Deletes the merged branch; one that is already gone counts as deleted.</summary>
    private async Task<bool> TryDeleteBranchAsync(GitHubClient client, string branch, CancellationToken token)
    {
        try
        {
            await client.DeleteBranchAsync(Owner, Repository, branch, token);
            return true;
        }
        catch (GitHubApiException ex) when (ex.IsNotFound || ex.IsRejected)
        {
            return true;
        }
        catch (GitHubApiException ex)
        {
            ErrorText = $"Merged, but {branch} could not be deleted: {SyncErrors.Describe(ex)}";
            return false;
        }
    }

    /// <summary>Says why GitHub refused to merge, in plain words.</summary>
    private string DescribeMergeFailure(GitHubApiException exception) => exception.StatusCode switch
    {
        HttpStatusCode.Conflict => "The branch changed since you loaded it. Refresh to review the new commits, then merge.",
        HttpStatusCode.Forbidden => "Your account may not merge into this repository. Ask a maintainer, or give your token write access to the repository's contents and pull requests.",
        HttpStatusCode.Unauthorized => "GitHub did not accept your token. Sign in to GitHub again.",
        HttpStatusCode.MethodNotAllowed when Details?.Mergeable == MergeableState.Conflicts || exception.Message.Contains("not mergeable", StringComparison.OrdinalIgnoreCase) =>
            $"GitHub cannot merge this pull request: it has conflicts with {BaseBranch}, or GitHub has not finished checking. Refresh, and resolve any conflicts first.",
        HttpStatusCode.MethodNotAllowed => $"GitHub refused to merge: {exception.Message}",
        _ => SyncErrors.Describe(exception),
    };
}
