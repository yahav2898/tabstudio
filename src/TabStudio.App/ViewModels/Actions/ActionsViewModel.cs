using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>A repository the GitHub Actions window shows: the workspace's, or a watched one.</summary>
/// <param name="Owner">The owner.</param>
/// <param name="Name">The repository name.</param>
/// <param name="Branch">The branch "This branch" means: the workspace's, or the default branch of a watched repository.</param>
/// <param name="IsWorkspace">Whether it is the workspace's repository.</param>
public sealed record ActionsScope(string Owner, string Name, string? Branch, bool IsWorkspace)
{
    /// <summary>"owner/name".</summary>
    public string FullName => $"{Owner}/{Name}";

    /// <summary>The repository's page.</summary>
    public Uri WebUri => new($"https://github.com/{Owner}/{Name}/actions");
}

/// <summary>
/// The GitHub Actions window: the workspace commit's checks, the repository's runs and workflows, a run's jobs and
/// steps, watched repositories, and run notifications. Also feeds the status bar's CI item and the Error List.
/// This file holds the state, the GitHub client and the window's refresh loop; the sections are in the other
/// parts of the class.
/// </summary>
public sealed partial class ActionsViewModel : ObservableObject, IRunDetailsHost, IDisposable
{
    private static readonly TimeSpan ActiveRefresh = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan IdleRefresh = TimeSpan.FromMinutes(3);

    private readonly IActionsHost _host;
    private readonly IStoragePicker _browser;
    private readonly IAppActivity _activity;
    private readonly CancellationTokenSource _lifetime = new();
    private GitHubClient? _client;
    private CancellationTokenSource? _wake;
    private bool _refreshLoopStarted;
    private bool _disposed;

    /// <summary>Creates the window's view model.</summary>
    /// <param name="account">The GitHub account and its sign-in dialog.</param>
    /// <param name="host">The IDE around the window.</param>
    /// <param name="browser">Opens GitHub's pages.</param>
    /// <param name="settings">Where the watched repositories and notification settings are kept.</param>
    /// <param name="notifications">The system's notifications.</param>
    /// <param name="activity">Whether the app is in the background.</param>
    public ActionsViewModel(GitHubAccountViewModel account, IActionsHost host, IStoragePicker browser, ActionsSettingsStore settings, INotifications notifications, IAppActivity activity)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(activity);
        Account = account;
        _host = host;
        _browser = browser;
        _activity = activity;
        Monitor = new CiMonitor(() => Client, activity);
        Monitor.Changed += OnMonitorChanged;
        Monitor.RunFinished += OnRunFinished;
        Account.Account.Changed += OnAccountChanged;
        InitializeWatched(settings);
        InitializeNotifications(notifications);
        Monitor.Start();
    }

    /// <summary>The GitHub account and its sign-in dialog.</summary>
    public GitHubAccountViewModel Account { get; }

    /// <summary>Watches the workspace commit's CI.</summary>
    public CiMonitor Monitor { get; }

    /// <summary>The client every part of the window uses, so their conditional reads share one cache.</summary>
    public GitHubClient Client => _client ??= Account.Account.CreateClient();

    /// <summary>Whether the workspace is in a folder linked to a GitHub repository.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCiVisible))]
    public partial bool IsLinked { get; private set; }

    /// <summary>The workspace's repository, branch and commit, when it is linked.</summary>
    public GitHubLink? WorkspaceLink { get; private set; }

    /// <summary>The repository the window shows, or <see langword="null"/> when there is none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScope), nameof(IsWorkspaceScope), nameof(IsWatchedScope), nameof(ScopeName))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(OpenOnGitHubCommand))]
    public partial ActionsScope? Scope { get; private set; }

    /// <summary>Whether the window shows a repository.</summary>
    public bool HasScope => Scope is not null;

    /// <summary>Whether the window shows the workspace's repository.</summary>
    public bool IsWorkspaceScope => Scope?.IsWorkspace == true;

    /// <summary>Whether the window shows a watched repository rather than the workspace's.</summary>
    public bool IsWatchedScope => Scope is { IsWorkspace: false };

    /// <summary>"owner/name" of the repository shown.</summary>
    public string? ScopeName => Scope?.FullName;

    /// <summary>Whether the window is visible in the side bar. Set by the workspace.</summary>
    public bool IsShown
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (value)
            {
                OnShown();
            }
        }
    }

    /// <summary>The run whose details the window shows, or <see langword="null"/> for its main page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunDetailsOpen), nameof(IsMainPageVisible))]
    public partial RunDetailsViewModel? RunDetails { get; private set; }

    /// <summary>Whether the window shows a run's details.</summary>
    public bool IsRunDetailsOpen => RunDetails is not null;

    /// <summary>Whether the window shows its main page: the commit's checks, runs, workflows and watched repositories.</summary>
    public bool IsMainPageVisible => RunDetails is null;

    /// <summary>What went wrong last, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>What the last action did, such as "Started CI on main".</summary>
    [ObservableProperty]
    public partial string? InfoText { get; private set; }

    /// <summary>Follows the workspace's link: its repository, branch and commit, or none when it is not linked.</summary>
    /// <param name="link">The link, or <see langword="null"/>.</param>
    public void SetWorkspaceLink(GitHubLink? link)
    {
        var repositoryChanged = WorkspaceLink?.FullName != link?.FullName;
        var branchChanged = repositoryChanged || WorkspaceLink?.Branch != link?.Branch;
        WorkspaceLink = link;
        IsLinked = link is not null;
        Monitor.SetTarget(link is null ? null : new CiTarget(link.Owner, link.Repository, link.Branch, link.BaseCommit));
        if (link is null)
        {
            if (Scope is { IsWorkspace: true })
            {
                Scope = null;
                RunDetails = null;
            }
        }
        else if (Scope is null or { IsWorkspace: true } && (Scope is null || branchChanged))
        {
            Scope = new ActionsScope(link.Owner, link.Repository, link.Branch, IsWorkspace: true);
            if (repositoryChanged)
            {
                RunDetails = null;
                ClearRepositoryLists();
            }

            ResetRunFilters();
        }

        OnLinkChanged();
        RequestWindowRefresh();
    }

    /// <summary>Shows the window on the workspace commit's checks, as tapping the status bar's CI item does.</summary>
    [RelayCommand]
    public void ShowCommitChecks()
    {
        if (WorkspaceLink is { } link && Scope is not { IsWorkspace: true })
        {
            Scope = new ActionsScope(link.Owner, link.Repository, link.Branch, IsWorkspace: true);
            ClearRepositoryLists();
            ResetRunFilters();
        }

        RunDetails = null;
        _host.ShowActions();
        Monitor.RequestRefresh();
    }

    /// <summary>Opens a run's details.</summary>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository's name.</param>
    /// <param name="run">The run.</param>
    public async Task ShowRunAsync(string owner, string repository, GitHubWorkflowRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var details = new RunDetailsViewModel(owner, repository, run, this);
        RunDetails = details;
        _host.ShowActions();
        await details.RefreshAsync(_lifetime.Token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Account.Account.Changed -= OnAccountChanged;
        Monitor.Changed -= OnMonitorChanged;
        Monitor.RunFinished -= OnRunFinished;
        Monitor.Dispose();
        DisposeNotifications();
        _lifetime.Cancel();
        _wake?.Dispose();
        _wake = null;
    }

    /// <inheritdoc />
    GitHubClient IRunDetailsHost.Client => Client;

    /// <inheritdoc />
    void IRunDetailsHost.Watch(string owner, string repository, GitHubWorkflowRun run, bool isRerun) => Monitor.Watch(owner, repository, run, isRerun);

    /// <inheritdoc />
    Task IRunDetailsHost.OpenUriAsync(Uri uri) => _browser.OpenUriAsync(uri);

    /// <inheritdoc />
    Task<bool> IRunDetailsHost.ConfirmAsync(string title, string message, string confirm, bool destructive) =>
        _host.Prompt.ConfirmAsync(title, message, confirm, destructive);

    /// <inheritdoc />
    void IRunDetailsHost.CloseRunDetails() => RunDetails = null;

    /// <summary>Reads everything the window shows again.</summary>
    [RelayCommand(CanExecute = nameof(HasScope))]
    private async Task RefreshAsync()
    {
        Monitor.RequestRefresh();
        await RefreshWindowAsync();
    }

    [RelayCommand(CanExecute = nameof(HasScope))]
    private async Task OpenOnGitHubAsync()
    {
        if (Scope is { } scope)
        {
            await _browser.OpenUriAsync(scope.WebUri);
        }
    }

    [RelayCommand]
    private void CloseRunDetails() => RunDetails = null;

    [RelayCommand]
    private void DismissMessages()
    {
        ErrorText = null;
        InfoText = null;
    }

    /// <summary>Called when the window becomes visible: shows fresh data, and keeps it fresh while visible.</summary>
    private void OnShown()
    {
        Monitor.RequestRefresh();
        if (!_refreshLoopStarted)
        {
            _refreshLoopStarted = true;
            _ = RefreshLoopAsync();
        }
        else
        {
            RequestWindowRefresh();
        }
    }

    /// <summary>
    /// While the window is visible, reads its runs, the open run's details and the watched repositories again: often
    /// while something runs, rarely otherwise. Every read is conditional, so this costs little.
    /// </summary>
    private async Task RefreshLoopAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            if (IsShown && !_activity.IsInBackground)
            {
                await RefreshWindowAsync();
            }

            var busy = RunDetails is { IsCompleted: false } || Runs.Any(r => !r.Run.IsCompleted);
            await WaitAsync(IsShown ? (busy ? ActiveRefresh : IdleRefresh) : Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RefreshWindowAsync()
    {
        if (_disposed || Scope is null)
        {
            return;
        }

        try
        {
            await RefreshRunsAsync();
            await RefreshWorkflowsAsync();
            if (RunDetails is { } details)
            {
                await details.RefreshAsync(_lifetime.Token);
            }

            await RefreshWatchedAsync();
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
    }

    private void RequestWindowRefresh() => _wake?.Cancel();

    private async Task WaitAsync(TimeSpan delay)
    {
        _wake?.Dispose();
        _wake = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, _wake.Token);
        try
        {
            await Task.Delay(delay, linked.Token);
        }
        catch (OperationCanceledException)
        {
            // Woken up early, or the workspace closed.
        }
    }

    /// <summary>A new account means new permissions and a new request limit: start over with a new client.</summary>
    private void OnAccountChanged(object? sender, EventArgs e)
    {
        _client = null;
        ErrorText = null;
        ClearRepositoryLists();
        Monitor.RequestRefresh();
        RequestWindowRefresh();
    }

    /// <summary>Describes a failure for the window.</summary>
    private string Describe(Exception exception) => ActionsErrors.Describe(exception, Client.IsSignedIn);
}
