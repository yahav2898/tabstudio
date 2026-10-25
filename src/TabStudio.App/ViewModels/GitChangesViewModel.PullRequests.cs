using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>Pull requests from the folder's branch: the link after a push, and the form that creates one.</summary>
public sealed partial class GitChangesViewModel
{
    /// <summary>Whether to offer "Create a Pull Request": the branch was pushed and has none open.</summary>
    [ObservableProperty]
    public partial bool CanCreatePullRequest { get; private set; }

    /// <summary>The open pull request from the branch, once created or found.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPullRequest), nameof(PullRequestText))]
    public partial GitHubPullRequest? PullRequest { get; private set; }

    /// <summary>Whether the branch has an open pull request.</summary>
    public bool HasPullRequest => PullRequest is not null;

    /// <summary>"Pull request #3: Add the login page".</summary>
    public string? PullRequestText => PullRequest is { } pull
        ? $"Pull request #{pull.Number.ToString(CultureInfo.InvariantCulture)}{(pull.IsDraft ? " (draft)" : string.Empty)}: {pull.Title}"
        : null;

    /// <summary>Whether the pull request form is shown.</summary>
    [ObservableProperty]
    public partial bool IsPullRequestFormOpen { get; private set; }

    /// <summary>The new pull request's title.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreatePullRequestCommand))]
    public partial string PullRequestTitle { get; set; } = string.Empty;

    /// <summary>The new pull request's description, in Markdown.</summary>
    [ObservableProperty]
    public partial string PullRequestDescription { get; set; } = string.Empty;

    /// <summary>The branch the changes should go into.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreatePullRequestCommand))]
    public partial string PullRequestBase { get; set; } = string.Empty;

    /// <summary>Whether to open the pull request as a draft.</summary>
    [ObservableProperty]
    public partial bool PullRequestDraft { get; set; }

    /// <summary>Why the pull request could not be created.</summary>
    [ObservableProperty]
    public partial string? PullRequestErrorText { get; private set; }

    [RelayCommand(CanExecute = nameof(CanOpenPullRequestForm))]
    private async Task OpenPullRequestFormAsync()
    {
        if (_sync is not { } sync)
        {
            return;
        }

        PullRequestTitle = _lastCommitMessage?.Split('\n', 2)[0].Trim() ?? string.Empty;
        PullRequestDescription = string.Empty;
        PullRequestDraft = false;
        PullRequestErrorText = null;
        PullRequestBase = _repository?.DefaultBranch ?? string.Empty;
        IsPullRequestFormOpen = true;
        if (_repository is null)
        {
            try
            {
                _repository = await Account.Account.CreateClient().GetRepositoryAsync(sync.Link.Owner, sync.Link.Repository, _lifetime.Token);
                PullRequestBase = _repository.DefaultBranch;
            }
            catch (OperationCanceledException)
            {
                // The workspace closed.
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                PullRequestErrorText = SyncErrors.Describe(ex);
            }
        }
    }

    [RelayCommand]
    private void ClosePullRequestForm() => IsPullRequestFormOpen = false;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreatePullRequestAsync()
    {
        if (_sync is not { } sync)
        {
            return;
        }

        PullRequestErrorText = null;
        IsBusy = true;
        try
        {
            await _gate.WaitAsync(_lifetime.Token);
            try
            {
                var description = string.IsNullOrWhiteSpace(PullRequestDescription) ? null : PullRequestDescription.Trim();
                var pull = await sync.CreatePullRequestAsync(PullRequestTitle.Trim(), description, PullRequestBase.Trim(), PullRequestDraft, _lifetime.Token);
                PullRequest = pull;
                CanCreatePullRequest = false;
                IsPullRequestFormOpen = false;
                InfoText = $"Opened pull request #{pull.Number.ToString(CultureInfo.InvariantCulture)} into {pull.BaseBranch}.";
                _host.ReportStatus(InfoText);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                PullRequestErrorText = SyncErrors.Describe(ex);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPullRequestAsync()
    {
        if (PullRequest is { } pull)
        {
            await _browser.OpenUriAsync(pull.WebUri);
        }
    }

    /// <summary>After a push to a branch other than the default one, finds its open pull request or offers to create one.</summary>
    private async Task FindPullRequestAsync()
    {
        PullRequest = null;
        CanCreatePullRequest = false;
        if (_sync is not { Link.Branch: { } branch } sync)
        {
            return;
        }

        try
        {
            var client = Account.Account.CreateClient();
            _repository ??= await client.GetRepositoryAsync(sync.Link.Owner, sync.Link.Repository, _lifetime.Token);
            if (branch == _repository.DefaultBranch)
            {
                return;
            }

            var open = await client.GetOpenPullRequestsAsync(sync.Link.Owner, sync.Link.Repository, branch, _lifetime.Token);
            PullRequest = open.Count > 0 ? open[0] : null;
            CanCreatePullRequest = open.Count == 0;
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            // The commit worked; the pull request link is only a shortcut, so offer it anyway.
            CanCreatePullRequest = true;
        }
    }

    private bool CanOpenPullRequestForm() => IsLinked && !IsBusy && !IsDetached;

    private bool CanCreate() => !IsBusy && !string.IsNullOrWhiteSpace(PullRequestTitle) && !string.IsNullOrWhiteSpace(PullRequestBase);
}
