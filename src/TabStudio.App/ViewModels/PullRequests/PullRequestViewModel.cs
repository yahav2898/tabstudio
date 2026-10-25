using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Agent.Anthropic;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>The sections of a pull request's document, as tabs under its header.</summary>
public enum PullRequestSection
{
    /// <summary>The description, reviewers and merging.</summary>
    Overview,

    /// <summary>Comments and reviews, oldest first.</summary>
    Conversation,

    /// <summary>The changed files.</summary>
    Files,

    /// <summary>The commits.</summary>
    Commits,
}

/// <summary>Where a pull request stands, for the pill in its header.</summary>
public enum PullRequestBadge
{
    /// <summary>Open.</summary>
    Open,

    /// <summary>Open as a draft.</summary>
    Draft,

    /// <summary>Merged.</summary>
    Merged,

    /// <summary>Closed without merging.</summary>
    Closed,
}

/// <summary>How the merge status line reads.</summary>
public enum MergeabilityKind
{
    /// <summary>GitHub is still working it out.</summary>
    Unknown,

    /// <summary>It can be merged.</summary>
    Good,

    /// <summary>It can be merged, with something to know first.</summary>
    Warning,

    /// <summary>It cannot be merged as it is.</summary>
    Blocked,
}

/// <summary>
/// A pull request in the IDE, like Visual Studio's pull request page: the header, overview, conversation, files
/// and commits; the review being written, merging, checking out the branch and Claude's review. One instance per
/// pull request and workspace: its document tab, the diffs of its files and the editors' comments all show it.
/// This file holds the header and loading; the other parts of the class are in <c>PullRequestViewModel.*.cs</c>.
/// </summary>
public sealed partial class PullRequestViewModel : ObservableObject, IDisposable
{
    private readonly IPullRequestHost _host;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    /// <summary>Creates the pull request's view model. Call <see cref="LoadAsync"/> to read it from GitHub.</summary>
    /// <param name="host">The IDE around it.</param>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="number">The pull request's number.</param>
    public PullRequestViewModel(IPullRequestHost host, string owner, string repository, int number)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(repository);
        _host = host;
        Owner = owner;
        Repository = repository;
        Number = number;
    }

    /// <summary>Raised when the review changed: its threads, the pending comments or Claude's drafts.</summary>
    public event EventHandler? ReviewChanged;

    /// <summary>The repository's owner.</summary>
    public string Owner { get; }

    /// <summary>The repository.</summary>
    public string Repository { get; }

    /// <summary>The pull request's number.</summary>
    public int Number { get; }

    /// <summary>"#12".</summary>
    public string NumberText => "#" + Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The document tab's title.</summary>
    public string TabTitle => "Pull Request " + NumberText;

    /// <summary>The pull request as GitHub last described it, once loaded.</summary>
    [ObservableProperty]
    public partial PullRequestDetails? Details { get; private set; }

    /// <summary>What lists show of it, once loaded.</summary>
    public PullRequestInfo? Info => Details?.Info;

    /// <summary>Its title.</summary>
    public string Title => Info?.Title ?? "Pull request " + NumberText;

    /// <summary>Who opened it.</summary>
    public string Author => Info?.Author ?? string.Empty;

    /// <summary>The branch with the changes.</summary>
    public string HeadBranch => Info?.Head.Name ?? string.Empty;

    /// <summary>The branch the changes go into.</summary>
    public string BaseBranch => Info?.Base.Name ?? string.Empty;

    /// <summary>Where it stands.</summary>
    public PullRequestBadge Badge => Info switch
    {
        { State: PullRequestState.Merged } => PullRequestBadge.Merged,
        { State: PullRequestState.Closed } => PullRequestBadge.Closed,
        { IsDraft: true } => PullRequestBadge.Draft,
        _ => PullRequestBadge.Open,
    };

    /// <summary>"Open", "Draft", "Merged" or "Closed".</summary>
    public string BadgeText => Badge.ToString();

    /// <summary>Whether it is open, drafts included.</summary>
    public bool IsOpen => Info?.State == PullRequestState.Open;

    /// <summary>Whether it was merged.</summary>
    public bool IsMerged => Info?.State == PullRequestState.Merged;

    /// <summary>"octocat wants to merge 2 commits into main from feature/login", or what happened once it is not open.</summary>
    public string SummaryText
    {
        get
        {
            if (Details is not { } details)
            {
                return string.Empty;
            }

            var commits = ReviewDisplay.Count(details.Commits, "commit");
            return details.Info.State switch
            {
                PullRequestState.Merged => $"{details.MergedBy ?? Author} merged {commits} into {BaseBranch} from {HeadBranch}",
                PullRequestState.Closed => $"{Author} wanted to merge {commits} into {BaseBranch} from {HeadBranch}",
                _ => $"{Author} wants to merge {commits} into {BaseBranch} from {HeadBranch}",
            };
        }
    }

    /// <summary>"opened 3 days ago".</summary>
    public string OpenedText => Info is { } info ? "opened " + ReviewDisplay.Age(info.CreatedAt, DateTimeOffset.Now) : string.Empty;

    /// <summary>"+120 −30 in 4 files".</summary>
    public string ChangesText => Details is { } details
        ? string.Create(CultureInfo.CurrentCulture, $"+{details.Additions} −{details.Deletions} in {ReviewDisplay.Count(details.ChangedFiles, "file")}")
        : string.Empty;

    /// <summary>Whether the signed-in account opened it, so it cannot approve or request changes.</summary>
    public bool IsMine => Info is { } info && string.Equals(info.Author, _host.Account.Login, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether it is being read from GitHub.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Whether it was read at least once.</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>What went wrong last, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>What the last action did, such as "Approved pull request #12".</summary>
    [ObservableProperty]
    public partial string? InfoText { get; private set; }

    /// <summary>The section shown under the header.</summary>
    [ObservableProperty]
    public partial PullRequestSection Section { get; set; }

    /// <summary>"Conversation (3)".</summary>
    public string ConversationTabText => Timeline.Count > 1 ? $"Conversation ({(Timeline.Count - 1).ToString(CultureInfo.CurrentCulture)})" : "Conversation";

    /// <summary>"Files changed (4)".</summary>
    public string FilesTabText => $"Files changed ({Files.Count.ToString(CultureInfo.CurrentCulture)})";

    /// <summary>"Commits (2)".</summary>
    public string CommitsTabText => $"Commits ({Commits.Count.ToString(CultureInfo.CurrentCulture)})";

    /// <summary>
    /// Reads the pull request from GitHub: its details, files, commits, conversation, reviews and comments, and the
    /// repository's merge settings. Called when its document opens, and by Refresh.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_disposed || IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        var token = _lifetime.Token;
        try
        {
            var client = _host.CreateClient();
            var details = client.GetPullRequestAsync(Owner, Repository, Number, token);
            var files = client.GetPullRequestFilesAsync(Owner, Repository, Number, token);
            var commits = client.GetPullRequestCommitsAsync(Owner, Repository, Number, token);
            var conversation = client.GetIssueCommentsAsync(Owner, Repository, Number, token);
            var settings = GetMergeSettingsAsync(client, token);
            await Task.WhenAll(details, files, commits, conversation, settings);

            _mergeBase = null;
            Details = details.Result;
            _conversation = conversation.Result;
            ApplyFiles(files.Result);
            ApplyCommits(commits.Result);
            ApplyMergeSettings(settings.Result, commits.Result);
            await LoadReviewAsync(client, token);
            IsLoaded = true;
            OnHeaderChanged();
            await LoadChecksAsync(client, details.Result.Info.Head.Sha, token);
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Shows the pull request's document.</summary>
    public void Show() => _host.ShowPullRequest(this);

    /// <summary>Shows the pull request's document with the Finish Review panel open, as a diff's Finish Review button does.</summary>
    public void ShowFinishReview()
    {
        Show();
        OpenFinishReview();
    }

    /// <summary>Called when the linked folder's branch changed, which decides whether the pull request is checked out.</summary>
    public void OnLinkChanged()
    {
        OnPropertyChanged(nameof(IsCheckedOut));
        OnPropertyChanged(nameof(CheckedOutText));
        CheckOutCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // The token source is left to the garbage collector: a request that is still finishing must be able to read it.
        _disposed = true;
        _lifetime.Cancel();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task OpenOnGitHubAsync()
    {
        var page = Info?.WebUri ?? new Uri(string.Create(CultureInfo.InvariantCulture, $"https://github.com/{Owner}/{Repository}/pull/{Number}"));
        await _host.OpenUriAsync(page);
    }

    [RelayCommand]
    private void SelectSection(PullRequestSection section) => Section = section;

    /// <summary>Tells the views that the header and everything derived from <see cref="Details"/> changed.</summary>
    private void OnHeaderChanged()
    {
        foreach (var name in new[]
        {
            nameof(Info), nameof(Title), nameof(Author), nameof(HeadBranch), nameof(BaseBranch), nameof(Badge), nameof(BadgeText),
            nameof(IsOpen), nameof(IsMerged), nameof(SummaryText), nameof(OpenedText), nameof(ChangesText), nameof(IsMine),
            nameof(MergeabilityText), nameof(MergeabilityKind), nameof(IsCheckedOut), nameof(CheckedOutText), nameof(IsFromFork),
            nameof(CheckOutToolTip), nameof(CanApprove), nameof(OwnPullRequestNote), nameof(ConversationTabText), nameof(FilesTabText),
            nameof(CommitsTabText), nameof(HasDescription), nameof(Description), nameof(ShowsMergePanel), nameof(MergeBlockedText),
            nameof(CanDeleteBranch), nameof(MergedText), nameof(IsDraft),
        })
        {
            OnPropertyChanged(name);
        }

        CheckOutCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
        ReadyForReviewCommand.NotifyCanExecuteChanged();
        ReviewWithClaudeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Signs in first when an action needs an account, saying so in <see cref="ErrorText"/>.</summary>
    /// <returns><see langword="true"/> when an account is signed in.</returns>
    private bool RequireSignIn(string action)
    {
        if (_host.Account.IsSignedIn)
        {
            return true;
        }

        ErrorText = $"Sign in to GitHub to {action}.";
        _host.Account.Open();
        return false;
    }

    private void RaiseReviewChanged() => ReviewChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Failures that are reported to the user; anything else is a bug and is not caught.</summary>
    private static bool IsExpected(Exception exception) =>
        exception is GitHubApiException or SyncException or AnthropicException or IOException or UnauthorizedAccessException or InvalidOperationException;
}
