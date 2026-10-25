using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Agent;
using TabStudio.App.ViewModels.PullRequests;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Pull request review in the workspace: the Pull Requests window, a document per pull request, the diffs of its
/// files, and the threads of the branch's pull request in the code editors, as Visual Studio shows them.
/// </summary>
public sealed partial class WorkspaceViewModel : IPullRequestHost
{
    private readonly Dictionary<int, PullRequestViewModel> _pullRequests = [];
    private PullRequestListViewModel? _pullRequestList;
    private PullRequestViewModel? _branchPullRequest;

    /// <summary>The Pull Requests window, created the first time it is shown.</summary>
    public PullRequestListViewModel PullRequests => _pullRequestList ??= new PullRequestListViewModel(this, OpenPullRequestAsync);

    /// <summary>Whether the side bar shows the Pull Requests window.</summary>
    public bool IsPullRequestsSelected => SelectedSideBarPanel == SideBarPanel.PullRequests;

    /// <inheritdoc />
    GitHubAccountViewModel IPullRequestHost.Account => GitChanges.Account;

    /// <inheritdoc />
    GitHubLink? IPullRequestHost.Link => GitChanges.Link;

    /// <inheritdoc />
    HttpClient IPullRequestHost.AgentHttp => AgentHttp;

    /// <summary>
    /// Opens a pull request of the linked repository in its own tab, reading it from GitHub the first time, as
    /// tapping it in the Pull Requests window does.
    /// </summary>
    /// <param name="number">The pull request's number.</param>
    /// <returns>The pull request, or <see langword="null"/> when the workspace is not linked to GitHub.</returns>
    public async Task<PullRequestViewModel?> ShowPullRequestAsync(int number)
    {
        if (PullRequestFor(number) is not { } pullRequest)
        {
            return null;
        }

        ((IPullRequestHost)this).ShowPullRequest(pullRequest);
        if (!pullRequest.IsLoaded)
        {
            await pullRequest.LoadAsync();
        }

        return pullRequest;
    }

    /// <inheritdoc />
    GitHubClient IPullRequestHost.CreateClient() => GitChanges.Account.Account.CreateClient();

    /// <inheritdoc />
    string IPullRequestHost.ReviewFilePath(string relativePath) =>
        Path.Combine(Workspace?.RootDirectory ?? _paths.Projects, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <inheritdoc />
    Task IPullRequestHost.OpenUriAsync(Uri uri) => GitChanges.OpenInBrowserAsync(uri);

    /// <inheritdoc />
    void IPullRequestHost.ShowPullRequest(PullRequestViewModel pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (Documents.FirstOrDefault(d => ReferenceEquals(d.PullRequest, pullRequest)) is { } open)
        {
            ActiveDocument = open;
            return;
        }

        var tabPath = Path.Combine(Workspace?.RootDirectory ?? _paths.Projects, pullRequest.TabTitle);
        var document = DocumentViewModel.ForPullRequest(pullRequest, tabPath, _profile);
        Attach(document);
        Documents.Insert(ActiveDocument is null ? Documents.Count : Documents.IndexOf(ActiveDocument) + 1, document);
        ActiveDocument = document;
    }

    /// <inheritdoc />
    void IPullRequestHost.ShowFileReview(FileComparison comparison, FileReviewViewModel review)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(review);
        var document = DocumentViewModel.ForReview(comparison, review, _profile);
        var earlier = Documents.FirstOrDefault(d => d.Review is { } open && ReferenceEquals(open.PullRequest, review.PullRequest) && open.Path == review.Path);
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
    Task<string?> IPullRequestHost.CheckOutAsync(PullRequestInfo pullRequest) => GitChanges.CheckOutPullRequestAsync(pullRequest);

    /// <inheritdoc />
    AgentSettings IPullRequestHost.LoadAgentSettings() => new AgentSettingsStore(_paths).Load();

    /// <inheritdoc />
    void IPullRequestHost.ShowAgentSettings()
    {
        SelectSideBarPanel(SideBarPanel.Agent);
        Agent.OpenSettingsCommand.Execute(null);
    }

    /// <inheritdoc />
    void IPullRequestHost.ReportStatus(string message) => StatusText = message;

    /// <summary>Shows the Pull Requests window, as its tab and Git > Pull Requests do, and lists the pull requests when the list is not fresh.</summary>
    [RelayCommand]
    private void ShowPullRequests()
    {
        SelectSideBarPanel(SideBarPanel.PullRequests);
        PullRequests.OnShown();
    }

    private Task OpenPullRequestAsync(int number) => ShowPullRequestAsync(number);

    /// <summary>The view model of a pull request of the linked repository, created once per workspace.</summary>
    private PullRequestViewModel? PullRequestFor(int number)
    {
        if (GitChanges.Link is not { } link)
        {
            return null;
        }

        if (!_pullRequests.TryGetValue(number, out var pullRequest) || pullRequest.Owner != link.Owner || pullRequest.Repository != link.Repository)
        {
            pullRequest = new PullRequestViewModel(this, link.Owner, link.Repository, number);
            _pullRequests[number] = pullRequest;
        }

        return pullRequest;
    }

    /// <summary>Follows the Git Changes window: its pull request link, the branch, and the account. Called by the constructor.</summary>
    private void InitializePullRequests()
    {
        GitChanges.PullRequestViewRequested += OnPullRequestViewRequested;
        GitChanges.PropertyChanged += OnGitChangesChangedForPullRequests;
        Documents.CollectionChanged += OnDocumentsChangedForPullRequests;
    }

    private void DisposePullRequests()
    {
        GitChanges.PullRequestViewRequested -= OnPullRequestViewRequested;
        GitChanges.PropertyChanged -= OnGitChangesChangedForPullRequests;
        Documents.CollectionChanged -= OnDocumentsChangedForPullRequests;
        _pullRequestList?.Dispose();
        foreach (var pullRequest in _pullRequests.Values)
        {
            pullRequest.Dispose();
        }

        _pullRequests.Clear();
    }

    private async void OnPullRequestViewRequested(object? sender, int number) => await ShowPullRequestAsync(number);

    private void OnGitChangesChangedForPullRequests(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GitChangesViewModel.RepositoryName):
            case nameof(GitChangesViewModel.IsLinked):
                _pullRequestList?.OnLinkChanged();
                OnPropertyChanged(nameof(IsPullRequestsSelected));
                break;
            case nameof(GitChangesViewModel.BranchName):
                foreach (var pullRequest in _pullRequests.Values)
                {
                    pullRequest.OnLinkChanged();
                }

                if (GitChanges.IsLinked && GitChanges.BranchName.Length > 0)
                {
                    _ = GitChanges.FindBranchPullRequestAsync();
                }

                break;
            case nameof(GitChangesViewModel.PullRequest):
                _ = ShowBranchPullRequestInEditorsAsync(GitChanges.PullRequest);
                break;
        }
    }

    /// <summary>Shows the review threads of the branch's pull request in the editors of its files, or takes them away.</summary>
    private async Task ShowBranchPullRequestInEditorsAsync(GitHubPullRequest? branchPullRequest)
    {
        var pullRequest = branchPullRequest is null ? null : PullRequestFor(branchPullRequest.Number);
        _branchPullRequest = pullRequest;
        if (pullRequest is not null && !pullRequest.IsLoaded)
        {
            await pullRequest.LoadAsync();
        }

        if (!ReferenceEquals(_branchPullRequest, pullRequest))
        {
            // Another branch took over while the pull request loaded.
            return;
        }

        foreach (var document in Documents)
        {
            AttachEditorReview(document);
        }
    }

    private void OnDocumentsChangedForPullRequests(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var document in e.NewItems?.OfType<DocumentViewModel>() ?? [])
        {
            AttachEditorReview(document);
        }
    }

    /// <summary>Gives a code file's editor the threads of the branch's pull request on that file, if it has any.</summary>
    private void AttachEditorReview(DocumentViewModel document)
    {
        if (document.IsComparison || document.IsNotebook || document.IsPullRequest)
        {
            return;
        }

        var root = GitChanges.Link is null ? null : GitHubSync.FindRoot(document.FilePath);
        if (_branchPullRequest is not { IsLoaded: true } pullRequest || root is null)
        {
            document.EditorReview = null;
            return;
        }

        var relative = Path.GetRelativePath(root, document.FilePath).Replace(Path.DirectorySeparatorChar, '/');
        if (document.EditorReview is { } current && ReferenceEquals(current.PullRequest, pullRequest) && current.Path == relative)
        {
            return;
        }

        document.EditorReview = pullRequest.ThreadsFor(relative).Any() || pullRequest.FileAt(relative) is not null
            ? new EditorReviewViewModel(pullRequest, document.Document, relative)
            : null;
    }
}
