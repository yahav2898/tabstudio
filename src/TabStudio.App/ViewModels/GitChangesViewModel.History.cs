using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl;

namespace TabStudio.App.ViewModels;

/// <summary>The branch's history: its recent commits, each of which opens on GitHub.</summary>
public sealed partial class GitChangesViewModel
{
    private const int HistoryLength = 30;

    /// <summary>The recent commits of the branch, newest first, in the history list.</summary>
    public ObservableCollection<GitCommitItemViewModel> History { get; } = [];

    /// <summary>Whether the history list is shown.</summary>
    [ObservableProperty]
    public partial bool IsHistoryOpen { get; private set; }

    /// <summary>Whether the history is loading.</summary>
    [ObservableProperty]
    public partial bool IsLoadingHistory { get; private set; }

    /// <summary>Why the history could not be loaded.</summary>
    [ObservableProperty]
    public partial string? HistoryErrorText { get; private set; }

    [RelayCommand(CanExecute = nameof(IsLinked))]
    private async Task OpenHistoryAsync()
    {
        if (_sync is not { } sync)
        {
            return;
        }

        History.Clear();
        HistoryErrorText = null;
        IsHistoryOpen = true;
        IsLoadingHistory = true;
        try
        {
            var now = DateTimeOffset.Now;
            foreach (var commit in await sync.GetHistoryAsync(HistoryLength, _lifetime.Token))
            {
                History.Add(new GitCommitItemViewModel(commit, now, OpenCommitCommand));
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            HistoryErrorText = SyncErrors.Describe(ex);
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    [RelayCommand]
    private void CloseHistory() => IsHistoryOpen = false;

    [RelayCommand]
    private async Task OpenCommitAsync(GitCommitItemViewModel? commit)
    {
        if (commit is not null && _sync?.Link is { } link)
        {
            await _browser.OpenUriAsync(new Uri($"{link.WebUri}/commit/{commit.Sha}"));
        }
    }
}
