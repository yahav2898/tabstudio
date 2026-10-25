using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>
/// Watched repositories: others the user follows, such as a team's, each with the latest run on its default
/// branch. Tapping one shows its runs, run details and logs, where re-run and cancel work as for the workspace's.
/// </summary>
public sealed partial class ActionsViewModel
{
    private static readonly TimeSpan WatchedInterval = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan WatchedIntervalSignedOut = TimeSpan.FromMinutes(5);

    private ActionsSettingsStore _settingsStore = null!;
    private ActionsSettings _settings = ActionsSettings.Default;
    private DateTimeOffset _watchedReadAt = DateTimeOffset.MinValue;

    /// <summary>The watched repositories, in the order they were added.</summary>
    public ObservableCollection<WatchedRepositoryItemViewModel> Watched { get; } = [];

    /// <summary>What the user typed to watch another repository: owner/name or its GitHub address.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddWatchedCommand))]
    public partial string WatchText { get; set; } = string.Empty;

    /// <summary>Why the repository could not be added.</summary>
    [ObservableProperty]
    public partial string? WatchErrorText { get; private set; }

    /// <summary>Whether any repository is watched.</summary>
    public bool HasWatched => Watched.Count > 0;

    private void InitializeWatched(ActionsSettingsStore store)
    {
        _settingsStore = store;
        _settings = store.Load();
        foreach (var name in _settings.WatchedRepositories)
        {
            if (GitHubRepository.TryParse(name, out var repository))
            {
                Watched.Add(CreateWatched(repository.Owner, repository.Name));
            }
        }
    }

    private WatchedRepositoryItemViewModel CreateWatched(string owner, string name) =>
        new(owner, name, OpenWatchedCommand, RemoveWatchedCommand);

    [RelayCommand(CanExecute = nameof(CanAddWatched))]
    private async Task AddWatchedAsync()
    {
        WatchErrorText = null;
        if (!GitHubRepository.TryParse(WatchText, out var repository))
        {
            WatchErrorText = "Enter a repository as owner/name, such as verimenta/platform, or paste its GitHub address.";
            return;
        }

        var fullName = $"{repository.Owner}/{repository.Name}";
        if (Watched.Any(w => string.Equals(w.FullName, fullName, StringComparison.OrdinalIgnoreCase)))
        {
            WatchErrorText = $"{fullName} is already watched.";
            return;
        }

        if (string.Equals(WorkspaceLink?.FullName, fullName, StringComparison.OrdinalIgnoreCase))
        {
            WatchErrorText = $"{fullName} is this workspace's repository: its runs are shown above.";
            return;
        }

        var item = CreateWatched(repository.Owner, repository.Name);
        Watched.Add(item);
        OnPropertyChanged(nameof(HasWatched));
        WatchText = string.Empty;
        SaveSettings(_settings with { WatchedRepositories = [.. Watched.Select(w => w.FullName)] });
        await RefreshWatchedItemAsync(item);
    }

    private bool CanAddWatched() => !string.IsNullOrWhiteSpace(WatchText);

    [RelayCommand]
    private void RemoveWatched(WatchedRepositoryItemViewModel? item)
    {
        if (item is null || !Watched.Remove(item))
        {
            return;
        }

        OnPropertyChanged(nameof(HasWatched));
        SaveSettings(_settings with { WatchedRepositories = [.. Watched.Select(w => w.FullName)] });
        if (Scope is { IsWorkspace: false } scope && scope.FullName == item.FullName)
        {
            BackToWorkspace();
        }
    }

    /// <summary>Shows a watched repository's runs and workflows in the window.</summary>
    [RelayCommand]
    private async Task OpenWatchedAsync(WatchedRepositoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.DefaultBranch is null)
        {
            await RefreshWatchedItemAsync(item);
        }

        Scope = new ActionsScope(item.Owner, item.Name, item.DefaultBranch, IsWorkspace: false);
        RunDetails = null;
        ClearRepositoryLists();
        ResetRunFilters();
        RequestWindowRefresh();
    }

    /// <summary>Goes back from a watched repository to the workspace's.</summary>
    [RelayCommand]
    private void BackToWorkspace()
    {
        RunDetails = null;
        if (WorkspaceLink is { } link)
        {
            Scope = new ActionsScope(link.Owner, link.Repository, link.Branch, IsWorkspace: true);
        }
        else
        {
            Scope = null;
        }

        ClearRepositoryLists();
        ResetRunFilters();
        RequestWindowRefresh();
    }

    /// <summary>Reads the watched repositories' latest runs, every few minutes at most.</summary>
    private async Task RefreshWatchedAsync(bool force = false)
    {
        var interval = Client.IsSignedIn ? WatchedInterval : WatchedIntervalSignedOut;
        if (Watched.Count == 0 || (!force && DateTimeOffset.UtcNow - _watchedReadAt < interval))
        {
            return;
        }

        _watchedReadAt = DateTimeOffset.UtcNow;
        foreach (var item in Watched.ToList())
        {
            await RefreshWatchedItemAsync(item);
        }
    }

    private async Task RefreshWatchedItemAsync(WatchedRepositoryItemViewModel item)
    {
        try
        {
            item.DefaultBranch ??= (await Client.GetRepositoryAsync(item.Owner, item.Name, _lifetime.Token)).DefaultBranch;
            var page = await Client.GetWorkflowRunsAsync(item.Owner, item.Name, new GitHubRunQuery(Branch: item.DefaultBranch, PerPage: 1), _lifetime.Token);
            item.Show(page.Runs.Count > 0 ? page.Runs[0] : null, null);
        }
        catch (GitHubApiException ex)
        {
            item.Show(null, Describe(ex));
        }
    }

    private void SaveSettings(ActionsSettings settings)
    {
        _settings = settings;
        if (!_settingsStore.Save(settings))
        {
            ErrorText = "TabStudio could not save the GitHub Actions settings; they last until it closes.";
        }
    }
}
