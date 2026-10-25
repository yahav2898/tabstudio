using System.ComponentModel;
using System.Globalization;
using TabStudio.SourceControl;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The folder's status: refreshed after every operation, a moment after files are saved, and when the window
/// becomes visible; shown as the changes and conflicts lists, the incoming and outgoing counts, and the
/// Solution Explorer's glyphs.
/// </summary>
public sealed partial class GitChangesViewModel
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(5);

    /// <summary>"2 incoming", or a hint to fetch until GitHub has been asked.</summary>
    public string IncomingText => IncomingCommits switch
    {
        null => "Fetch to check GitHub",
        1 => "1 incoming",
        int count => $"{count.ToString(CultureInfo.CurrentCulture)} incoming",
    };

    /// <summary>Whether GitHub has commits to pull.</summary>
    public bool HasIncoming => IncomingCommits > 0;

    /// <summary>"3 outgoing": the changed files that a commit sends to GitHub.</summary>
    public string OutgoingText => $"{Changes.Count.ToString(CultureInfo.CurrentCulture)} outgoing";

    /// <summary>"Changes (3)".</summary>
    public string ChangesHeader => $"Changes ({Changes.Count.ToString(CultureInfo.CurrentCulture)})";

    /// <summary>"Merge Conflicts (1)".</summary>
    public string ConflictsHeader => $"Merge Conflicts ({Conflicts.Count.ToString(CultureInfo.CurrentCulture)})";

    /// <summary>Whether there are changed files.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Whether there are unresolved conflicts.</summary>
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>Whether the linked folder has nothing to commit and nothing to resolve.</summary>
    public bool IsClean => IsLinked && Changes.Count == 0 && Conflicts.Count == 0;

    /// <summary>How many changed files are checked.</summary>
    public int SelectedCount => Changes.Count(c => c.IsSelected);

    /// <summary>Whether every changed file is checked; setting it checks or clears them all.</summary>
    public bool AreAllSelected
    {
        get => Changes.Count > 0 && Changes.All(c => c.IsSelected);
        set
        {
            foreach (var change in Changes)
            {
                change.IsSelected = value;
            }
        }
    }

    /// <summary>The commit button's label: commit and push are one step on GitHub, so it says both.</summary>
    public string CommitButtonText => SelectedCount > 0 ? "Commit Selected and Push" : "Commit All and Push";

    /// <summary>Reads the folder's status again, and asks GitHub for new commits when asked to.</summary>
    /// <param name="checkGitHub">Whether to ask GitHub for new commits too.</param>
    public async Task RefreshAsync(bool checkGitHub = false)
    {
        if (_disposed)
        {
            return;
        }

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
            await RefreshCoreAsync(checkGitHub);
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

    /// <summary>Refreshes a moment from now, once, however often it is called: after files are saved or created.</summary>
    public void ScheduleRefresh()
    {
        if (_disposed || _sync is null)
        {
            return;
        }

        _scheduledRefresh?.Cancel();
        _scheduledRefresh?.Dispose();
        _scheduledRefresh = new CancellationTokenSource();
        var token = _scheduledRefresh.Token;
        _ = RefreshAfterDelayAsync(token);
    }

    /// <summary>Called when the window becomes visible: shows fresh status, and checks GitHub now and then.</summary>
    public void OnShown()
    {
        if (_sync is not null && !IsBusy)
        {
            var stale = _lastFetch is not { } last || DateTimeOffset.UtcNow - last > FetchInterval;
            _ = RefreshAsync(checkGitHub: stale);
        }
    }

    private async Task RefreshCoreAsync(bool checkGitHub)
    {
        if (_sync is not { } sync)
        {
            Apply(null);
            return;
        }

        // Local changes first, so they show even when GitHub cannot be reached.
        Apply(await sync.GetStatusAsync(checkGitHub: false, _lifetime.Token));
        if (checkGitHub)
        {
            var status = await sync.GetStatusAsync(checkGitHub: true, _lifetime.Token);
            _lastFetch = DateTimeOffset.UtcNow;
            _repository ??= await Account.Account.CreateClient().GetRepositoryAsync(sync.Link.Owner, sync.Link.Repository, _lifetime.Token);
            Apply(status);
        }
    }

    private async Task TryRefreshCoreAsync()
    {
        try
        {
            await RefreshCoreAsync(checkGitHub: false);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            // The error of the operation itself is the one worth showing.
        }
    }

    private async Task RefreshAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(RefreshDelay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await RefreshAsync();
    }

    /// <summary>Shows a status: the branch, incoming commits, changes and conflicts, and the Solution Explorer's glyphs.</summary>
    private void Apply(SyncStatus? status)
    {
        Link = status?.Link;
        var selected = Changes.Where(c => c.IsSelected).Select(c => c.RelativePath).ToHashSet(StringComparer.Ordinal);
        foreach (var change in Changes)
        {
            change.PropertyChanged -= OnChangePropertyChanged;
        }

        Changes.Clear();
        Conflicts.Clear();
        var glyphs = new Dictionary<string, GitFileStatus>(StringComparer.Ordinal);
        if (status is not null && _sync is { } sync)
        {
            RepositoryName = status.Link.FullName;
            BranchName = status.Link.Branch ?? status.Link.BaseCommit[..Math.Min(7, status.Link.BaseCommit.Length)];
            IsDetached = status.Link.IsDetached;
            if (status.IncomingCommits is { } incoming)
            {
                IncomingCommits = incoming;
            }

            var conflicted = status.Conflicts.ToHashSet(StringComparer.Ordinal);
            foreach (var path in status.Conflicts)
            {
                var item = new GitChangeItemViewModel(path, WorkingTree.FullPath(sync.Root, path), GitFileStatus.Conflict, this);
                Conflicts.Add(item);
                glyphs[item.FullPath] = GitFileStatus.Conflict;
            }

            foreach (var change in status.Changes.Where(c => !conflicted.Contains(c.Path)))
            {
                var kind = change.Kind switch
                {
                    ChangeKind.Added => GitFileStatus.Added,
                    ChangeKind.Deleted => GitFileStatus.Deleted,
                    _ => GitFileStatus.Modified,
                };
                var item = new GitChangeItemViewModel(change.Path, WorkingTree.FullPath(sync.Root, change.Path), kind, this)
                {
                    IsSelected = selected.Contains(change.Path),
                };
                item.PropertyChanged += OnChangePropertyChanged;
                Changes.Add(item);
                glyphs[item.FullPath] = kind;
            }
        }

        _host.ShowSourceControlStatus(glyphs);
        OnPropertyChanged(nameof(OutgoingText));
        OnPropertyChanged(nameof(ChangesHeader));
        OnPropertyChanged(nameof(ConflictsHeader));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(IsClean));
        OnSelectionChanged();
    }

    private void OnChangePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitChangeItemViewModel.IsSelected))
        {
            OnSelectionChanged();
        }
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(AreAllSelected));
        OnPropertyChanged(nameof(CommitButtonText));
        CommitCommand.NotifyCanExecuteChanged();
    }
}
