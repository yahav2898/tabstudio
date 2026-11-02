using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Platform;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Visual Studio's Git Changes window for a workspace backed by GitHub: the branch, incoming commits, the
/// changed files with their checkboxes, the commit message, merge conflicts, pull requests, and publishing a
/// folder that is not on GitHub yet. This file holds the state, finding the linked folder and running
/// operations; status is in <c>GitChangesViewModel.Status.cs</c> and the operations in the other parts.
/// </summary>
/// <remarks>
/// Operations on one folder never overlap: each takes <see cref="_gate"/> first, so a refresh that arrives
/// during a pull waits for it.
/// </remarks>
public sealed partial class GitChangesViewModel : ObservableObject, IGitChangeActions, IDisposable
{
    private readonly ISourceControlHost _host;
    private readonly IStoragePicker _browser;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _scheduledRefresh;
    private GitHubSync? _sync;
    private string? _workspaceRoot;
    private GitHubRepositoryInfo? _repository;
    private DateTimeOffset? _lastFetch;
    private bool _disposed;

    /// <summary>Creates the window's view model. Call <see cref="AttachAsync"/> when a workspace opens.</summary>
    /// <param name="account">The GitHub account dialog.</param>
    /// <param name="host">The IDE around the window.</param>
    /// <param name="browser">Opens GitHub pages.</param>
    public GitChangesViewModel(GitHubAccountViewModel account, ISourceControlHost host, IStoragePicker browser)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(browser);
        Account = account;
        _host = host;
        _browser = browser;
        Account.Account.Changed += OnAccountChanged;
    }

    /// <summary>Raised when the commit message box should take the keyboard focus, as Git > Commit does.</summary>
    public event EventHandler? CommitMessageFocusRequested;

    /// <summary>The GitHub account dialog.</summary>
    public GitHubAccountViewModel Account { get; }

    /// <summary>Changed files, except conflicted ones.</summary>
    public ObservableCollection<GitChangeItemViewModel> Changes { get; } = [];

    /// <summary>Files a pull could not merge.</summary>
    public ObservableCollection<GitChangeItemViewModel> Conflicts { get; } = [];

    /// <summary>Whether a workspace is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPublishVisible), nameof(IsLinkCheckoutVisible))]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    public partial bool HasWorkspace { get; private set; }

    /// <summary>Whether the workspace is in a folder linked to a GitHub repository.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPublishVisible), nameof(IsLinkCheckoutVisible))]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(PullCommand), nameof(SyncCommand), nameof(CommitCommand), nameof(PublishCommand), nameof(LinkCheckoutCommand), nameof(OpenBranchPickerCommand), nameof(NewBranchCommand), nameof(OpenPullRequestFormCommand), nameof(OpenHistoryCommand), nameof(OpenOnGitHubCommand))]
    public partial bool IsLinked { get; private set; }

    /// <summary>Whether the "Publish to GitHub" panel is shown instead of the changes.</summary>
    public bool IsPublishVisible => HasWorkspace && !IsLinked && Checkout is null;

    /// <summary>
    /// The Git clone of a GitHub repository that holds the workspace while it is not linked yet, such as a folder
    /// cloned on a computer; <see langword="null"/> otherwise. Link to GitHub links it, without downloading it again;
    /// opening a folder never writes into it or asks GitHub by itself.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPublishVisible), nameof(IsLinkCheckoutVisible), nameof(LinkCheckoutText))]
    [NotifyCanExecuteChangedFor(nameof(LinkCheckoutCommand))]
    public partial GitCheckout? Checkout { get; private set; }

    /// <summary>Whether the "Link to GitHub" panel for a Git clone is shown instead of the changes.</summary>
    public bool IsLinkCheckoutVisible => HasWorkspace && !IsLinked && Checkout is not null;

    /// <summary>What the "Link to GitHub" panel says about the clone.</summary>
    public string LinkCheckoutText => Checkout is { } checkout
        ? $"This folder is a Git clone of {checkout.FullName}{(checkout.Branch is { } branch ? $" on {branch}" : string.Empty)}. Link it to see its changes, commit, pull and create branches from here. Nothing is downloaded again."
        : string.Empty;

    /// <summary>"owner/repository".</summary>
    [ObservableProperty]
    public partial string? RepositoryName { get; private set; }

    /// <summary>The branch, or the commit when the folder holds a tag or commit.</summary>
    [ObservableProperty]
    public partial string BranchName { get; private set; } = string.Empty;

    /// <summary>Whether the folder holds a tag or commit instead of following a branch.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand), nameof(OpenPullRequestFormCommand))]
    public partial bool IsDetached { get; private set; }

    /// <summary>Commits on GitHub the folder does not have yet, or <see langword="null"/> until GitHub is asked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IncomingText), nameof(HasIncoming))]
    public partial int? IncomingCommits { get; private set; }

    /// <summary>The commit message.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial string CommitMessage { get; set; } = string.Empty;

    /// <summary>Whether an operation is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand), nameof(PullCommand), nameof(SyncCommand), nameof(CommitCommand), nameof(PublishCommand), nameof(LinkCheckoutCommand), nameof(OpenBranchPickerCommand), nameof(NewBranchCommand), nameof(OpenPullRequestFormCommand), nameof(CreatePullRequestCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>What the running operation is doing.</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; private set; }

    /// <summary>What went wrong last, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>What the last operation did, such as "Pushed 1a2b3c4 to main".</summary>
    [ObservableProperty]
    public partial string? InfoText { get; private set; }

    /// <summary>
    /// Finds out whether a workspace folder is linked to GitHub and shows its changes. Called when a workspace
    /// opens and whenever it is read again.
    /// </summary>
    /// <param name="workspaceRoot">The workspace's root folder.</param>
    public async Task AttachAsync(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(workspaceRoot);
        if (string.Equals(_workspaceRoot, workspaceRoot, StringComparison.Ordinal))
        {
            ScheduleRefresh();
            return;
        }

        _workspaceRoot = workspaceRoot;
        HasWorkspace = true;
        PublishName = DefaultRepositoryName(workspaceRoot);
        OpenLinkedFolder();
        await RefreshAsync();
    }

    /// <summary>Asks the window to put the keyboard focus in the commit message box.</summary>
    public void FocusCommitMessage() => CommitMessageFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // The gate and the lifetime token are left to the garbage collector: an operation that is still
        // finishing must be able to release the one and read the other.
        _disposed = true;
        Account.Account.Changed -= OnAccountChanged;
        _scheduledRefresh?.Cancel();
        _scheduledRefresh?.Dispose();
        _scheduledRefresh = null;
        _lifetime.Cancel();
    }

    /// <summary>Runs an operation on the linked folder: one at a time, with progress, errors shown inline, and fresh status after.</summary>
    /// <param name="operation">The operation; returns a message for the user, or <see langword="null"/>.</param>
    /// <param name="checkGitHub">Whether the status afterwards asks GitHub for new commits.</param>
    /// <returns><see langword="true"/> when the operation succeeded.</returns>
    private async Task<bool> RunAsync(Func<GitHubSync, IProgress<SyncProgress>, CancellationToken, Task<string?>> operation, bool checkGitHub = false)
    {
        if (_sync is null || _disposed)
        {
            return false;
        }

        ErrorText = null;
        InfoText = null;
        IsBusy = true;
        ProgressText = "Working";
        var progress = new Progress<SyncProgress>(p => ProgressText = DescribeProgress(p));
        var succeeded = false;
        try
        {
            await _gate.WaitAsync(_lifetime.Token);
            try
            {
                if (_sync is { } sync)
                {
                    var message = await operation(sync, progress, _lifetime.Token);
                    succeeded = true;
                    await RefreshCoreAsync(checkGitHub);
                    if (message is not null)
                    {
                        InfoText = message;
                        _host.ReportStatus(message);
                    }
                }
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                ShowError(ex);
                await TryRefreshCoreAsync();
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
            ProgressText = null;
        }

        return succeeded;
    }

    /// <summary>Opens the linked folder that holds the workspace, with a client for the current account.</summary>
    private void OpenLinkedFolder()
    {
        var root = _workspaceRoot is null ? null : GitHubSync.FindRoot(_workspaceRoot);
        _sync = root is null ? null : GitHubSync.Open(Account.Account.CreateClient(), root);
        _repository = null;
        _lastFetch = null;
        IncomingCommits = null;
        PullRequest = null;
        CanCreatePullRequest = false;
        IsLinked = _sync is not null;
        Checkout = _sync is null && _workspaceRoot is not null ? GitCheckout.Find(_workspaceRoot) : null;
        if (_sync is null)
        {
            RepositoryName = null;
            BranchName = string.Empty;
            IsDetached = false;
        }
    }

    private void OnAccountChanged(object? sender, EventArgs e)
    {
        if (_disposed || _workspaceRoot is null)
        {
            return;
        }

        ErrorText = null;
        PublishCommand.NotifyCanExecuteChanged();
        LinkCheckoutCommand.NotifyCanExecuteChanged();
        _ = ReopenAsync();
    }

    /// <summary>Opens the linked folder again with a client for the account that is signed in now.</summary>
    private async Task ReopenAsync()
    {
        try
        {
            await _gate.WaitAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (_sync is { } current)
            {
                _sync = GitHubSync.Open(Account.Account.CreateClient(), current.Root);
                IsLinked = _sync is not null;
                await RefreshCoreAsync(checkGitHub: false);
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            ErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Shows a failure in the window and the status bar, and offers to sign in when that is what it takes.</summary>
    private void ShowError(Exception exception)
    {
        ErrorText = SyncErrors.Describe(exception);
        _host.ReportStatus(ErrorText);
        if (exception is SyncException { Failure: SyncFailure.NotSignedIn } or GitHubApiException { IsUnauthorized: true })
        {
            Account.Open();
        }
    }

    private List<string> FullPaths(IEnumerable<string> relativePaths) =>
        _sync is { } sync ? [.. relativePaths.Select(p => WorkingTree.FullPath(sync.Root, p))] : [];

    /// <summary>Failures an operation reports to the user; anything else is a bug and is not caught.</summary>
    private static bool IsExpected(Exception exception) =>
        exception is SyncException or GitHubApiException or IOException or UnauthorizedAccessException or InvalidOperationException;

    /// <summary>"Uploading 12 files (3 of 12)"; downloads counted in bytes state their size themselves.</summary>
    private static string DescribeProgress(SyncProgress progress) =>
        progress.Total > 0 && progress.Message.EndsWith("files", StringComparison.Ordinal)
            ? $"{progress.Message} ({progress.Completed.ToString(CultureInfo.CurrentCulture)} of {progress.Total.ToString(CultureInfo.CurrentCulture)})"
            : progress.Message;

    /// <summary>A repository name GitHub accepts, from the workspace folder's name.</summary>
    private static string DefaultRepositoryName(string workspaceRoot)
    {
        var name = Path.GetFileName(workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var cleaned = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-').ToArray()).Trim('-', '.');
        return cleaned.Length == 0 ? "my-project" : cleaned;
    }
}
