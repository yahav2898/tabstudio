using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.PullRequests;

/// <summary>Which pull requests the Pull Requests window lists.</summary>
public enum PullRequestFilter
{
    /// <summary>Every open pull request.</summary>
    Open,

    /// <summary>Open pull requests the signed-in account opened.</summary>
    Mine,

    /// <summary>Open pull requests the signed-in account is asked to review.</summary>
    ReviewRequested,

    /// <summary>Merged and closed pull requests.</summary>
    Closed,
}

/// <summary>
/// The Pull Requests window, beside Git Changes as in Visual Studio: the linked repository's pull requests, filtered
/// to the open ones, the signed-in account's own, those it is asked to review, or the closed ones. Tapping one
/// opens its document.
/// </summary>
public sealed partial class PullRequestListViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);

    private readonly IPullRequestHost _host;
    private readonly Func<int, Task> _open;
    private readonly CancellationTokenSource _lifetime = new();
    private DateTimeOffset? _loadedAt;
    private string? _loadedFor;
    private int _version;
    private bool _disposed;

    /// <summary>Creates the window's view model.</summary>
    /// <param name="host">The IDE around it.</param>
    /// <param name="open">Opens a pull request's document, by number.</param>
    public PullRequestListViewModel(IPullRequestHost host, Func<int, Task> open)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(open);
        _host = host;
        _open = open;
    }

    /// <summary>The listed pull requests, the most recently updated first.</summary>
    public ObservableCollection<PullRequestItemViewModel> Items { get; } = [];

    /// <summary>Which pull requests are listed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyText), nameof(NeedsSignIn))]
    public partial PullRequestFilter Filter { get; private set; }

    /// <summary>"owner/repository", or <see langword="null"/> when the workspace is not linked to GitHub.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinked), nameof(IsEmpty))]
    public partial string? RepositoryName { get; private set; }

    /// <summary>Whether the workspace is in a folder linked to GitHub.</summary>
    public bool IsLinked => RepositoryName is not null;

    /// <summary>Whether the list is loading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial bool IsLoading { get; private set; }

    /// <summary>Why the list could not be loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string? ErrorText { get; private set; }

    /// <summary>Whether the filter needs a signed-in account, and none is.</summary>
    public bool NeedsSignIn => Filter is PullRequestFilter.Mine or PullRequestFilter.ReviewRequested && !_host.Account.IsSignedIn;

    /// <summary>Whether nothing matched the filter.</summary>
    public bool IsEmpty => IsLinked && !IsLoading && ErrorText is null && !NeedsSignIn && Items.Count == 0;

    /// <summary>What the empty list says.</summary>
    public string EmptyText => Filter switch
    {
        PullRequestFilter.Mine => "You have no open pull requests in this repository.",
        PullRequestFilter.ReviewRequested => "Nobody is waiting for your review here.",
        PullRequestFilter.Closed => "No pull requests were merged or closed yet.",
        _ => "No open pull requests. Create one from Git Changes after you push a branch.",
    };

    /// <summary>Called when the window becomes visible: loads the list when the repository changed or the list is a few minutes old.</summary>
    public void OnShown()
    {
        UpdateRepository();
        var stale = _loadedAt is not { } loaded || DateTimeOffset.UtcNow - loaded > StaleAfter || _loadedFor != RepositoryName;
        if (IsLinked && stale && !IsLoading)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>Called when the workspace's link to GitHub or the account changed.</summary>
    public void OnLinkChanged()
    {
        var before = RepositoryName;
        UpdateRepository();
        OnPropertyChanged(nameof(NeedsSignIn));
        OnPropertyChanged(nameof(IsEmpty));
        if (before != RepositoryName)
        {
            Items.Clear();
            _loadedAt = null;
        }
    }

    /// <summary>Reads the list from GitHub.</summary>
    public async Task LoadAsync()
    {
        UpdateRepository();
        if (_disposed || _host.Link is not { } link)
        {
            return;
        }

        var version = ++_version;
        ErrorText = null;
        if (NeedsSignIn)
        {
            Items.Clear();
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        IsLoading = true;
        try
        {
            var client = _host.CreateClient();
            var state = Filter == PullRequestFilter.Closed ? PullRequestStateFilter.Closed : PullRequestStateFilter.Open;
            var pulls = client.GetPullRequestsAsync(link.Owner, link.Repository, state, count: 50, cancellationToken: _lifetime.Token);
            var counts = GetCommentCountsAsync(client, link, state);
            await Task.WhenAll(pulls, counts);
            if (version != _version)
            {
                return;
            }

            var login = _host.Account.Login;
            var shown = pulls.Result.Where(p => Filter switch
            {
                PullRequestFilter.Mine => string.Equals(p.Author, login, StringComparison.OrdinalIgnoreCase),
                PullRequestFilter.ReviewRequested => p.RequestedReviewers.Contains(login ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                _ => true,
            });

            Items.Clear();
            var now = DateTimeOffset.Now;
            foreach (var pull in shown)
            {
                Items.Add(new PullRequestItemViewModel(pull, counts.Result.TryGetValue(pull.Number, out var count) ? count : null, login, now, OpenCommand));
            }

            _loadedAt = DateTimeOffset.UtcNow;
            _loadedFor = RepositoryName;
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (ex is GitHubApiException or IOException)
        {
            if (version == _version)
            {
                Items.Clear();
                ErrorText = SyncErrors.Describe(ex);
            }
        }
        finally
        {
            if (version == _version)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
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
    private Task SelectFilterAsync(PullRequestFilter filter)
    {
        Filter = filter;
        return LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenAsync(PullRequestItemViewModel? item) => item is null ? Task.CompletedTask : _open(item.Number);

    [RelayCommand]
    private void SignIn() => _host.Account.Open();

    private bool CanRefresh() => !IsLoading;

    private void UpdateRepository() => RepositoryName = _host.Link?.FullName;

    /// <summary>Comment counts come from GitHub's search, whose lower rate limit may refuse them; the list shows without them then.</summary>
    private async Task<IReadOnlyDictionary<int, int>> GetCommentCountsAsync(GitHubClient client, GitHubLink link, PullRequestStateFilter state)
    {
        try
        {
            return await client.GetPullRequestCommentCountsAsync(link.Owner, link.Repository, state, _lifetime.Token);
        }
        catch (GitHubApiException)
        {
            return new Dictionary<int, int>();
        }
    }
}
