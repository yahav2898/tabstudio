using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>Which branches the Runs list shows.</summary>
/// <param name="Label">"main" for the workspace's branch, "main (default)" for a watched repository's, or "All branches".</param>
/// <param name="Branch">The branch, or <see langword="null"/> for all of them.</param>
public sealed record RunBranchFilter(string Label, string? Branch);

/// <summary>Which workflows the Runs list shows.</summary>
/// <param name="Label">"All workflows", or the workflow's name.</param>
/// <param name="WorkflowId">The workflow, or <see langword="null"/> for all of them.</param>
public sealed record RunWorkflowFilter(string Label, long? WorkflowId);

/// <summary>The Runs list: the repository's latest workflow runs, by branch and by workflow.</summary>
public sealed partial class ActionsViewModel
{
    private const int RunsShown = 25;

    private static readonly RunWorkflowFilter AllWorkflows = new("All workflows", null);

    private bool _runsRead;
    private bool _suppressRunRefresh;

    /// <summary>The runs, newest first.</summary>
    public ObservableCollection<WorkflowRunItemViewModel> Runs { get; } = [];

    /// <summary>The branch choices: the scope's branch, and all branches.</summary>
    public ObservableCollection<RunBranchFilter> BranchFilters { get; } = [];

    /// <summary>The workflow choices: all workflows, and each workflow.</summary>
    public ObservableCollection<RunWorkflowFilter> WorkflowFilters { get; } = [AllWorkflows];

    /// <summary>The chosen branches.</summary>
    [ObservableProperty]
    public partial RunBranchFilter? BranchFilter { get; set; }

    /// <summary>The chosen workflows.</summary>
    [ObservableProperty]
    public partial RunWorkflowFilter? WorkflowFilter { get; set; } = AllWorkflows;

    /// <summary>Whether the runs are loading for the first time.</summary>
    [ObservableProperty]
    public partial bool IsLoadingRuns { get; private set; }

    /// <summary>Why the runs could not be read.</summary>
    [ObservableProperty]
    public partial string? RunsErrorText { get; private set; }

    /// <summary>Whether the list has runs.</summary>
    public bool HasRuns => Runs.Count > 0;

    /// <summary>Whether to say there are no runs: read, empty, and no error.</summary>
    public bool ShowsNoRuns => !IsLoadingRuns && RunsErrorText is null && Runs.Count == 0 && _runsRead;

    /// <summary>"No runs on main yet." or "No runs yet.".</summary>
    public string NoRunsText => BranchFilter?.Branch is { } branch ? $"No runs on {branch} yet." : "No runs yet.";

    partial void OnBranchFilterChanged(RunBranchFilter? value)
    {
        OnPropertyChanged(nameof(NoRunsText));
        _ = RefreshRunsAsync(clear: true);
    }

    partial void OnWorkflowFilterChanged(RunWorkflowFilter? value) => _ = RefreshRunsAsync(clear: true);

    /// <summary>Sets the filters to the scope's branch and all workflows.</summary>
    private void ResetRunFilters()
    {
        BranchFilters.Clear();
        if (Scope is { } scope)
        {
            if (scope.Branch is { } branch)
            {
                // The branch's name first, so it shows in the narrow side bar.
                BranchFilters.Add(new RunBranchFilter(scope.IsWorkspace ? branch : $"{branch} (default)", branch));
            }

            BranchFilters.Add(new RunBranchFilter("All branches", null));
        }

        _suppressRunRefresh = true;
        BranchFilter = BranchFilters.Count > 0 ? BranchFilters[0] : null;
        WorkflowFilter = AllWorkflows;
        _suppressRunRefresh = false;
    }

    /// <summary>Forgets the lists of the previous repository.</summary>
    private void ClearRepositoryLists()
    {
        Runs.Clear();
        Workflows.Clear();
        WorkflowFilters.Clear();
        WorkflowFilters.Add(AllWorkflows);
        _workflowsRead = null;
        _runsRead = false;
        RunsErrorText = null;
        WorkflowsErrorText = null;
        OnPropertyChanged(nameof(HasRuns));
        OnPropertyChanged(nameof(ShowsNoRuns));
        OnPropertyChanged(nameof(HasWorkflows));
    }

    /// <summary>Reads the runs again with the chosen filters.</summary>
    /// <param name="clear">Whether the filters changed, so the old list no longer applies.</param>
    private async Task RefreshRunsAsync(bool clear = false)
    {
        if (_suppressRunRefresh || Scope is not { } scope)
        {
            return;
        }

        if (clear)
        {
            Runs.Clear();
            _runsRead = false;
            OnPropertyChanged(nameof(HasRuns));
        }

        IsLoadingRuns = Runs.Count == 0;
        try
        {
            var query = new GitHubRunQuery(Branch: BranchFilter?.Branch, WorkflowId: WorkflowFilter?.WorkflowId, PerPage: RunsShown);
            var page = await Client.GetWorkflowRunsAsync(scope.Owner, scope.Name, query, _lifetime.Token);
            if (Scope != scope || query.Branch != BranchFilter?.Branch || query.WorkflowId != WorkflowFilter?.WorkflowId)
            {
                return;
            }

            ShowRuns(page.Runs);
            RunsErrorText = null;
        }
        catch (GitHubApiException ex)
        {
            RunsErrorText = Describe(ex);
        }
        finally
        {
            IsLoadingRuns = false;
            _runsRead = true;
            OnPropertyChanged(nameof(ShowsNoRuns));
        }
    }

    private void ShowRuns(IReadOnlyList<GitHubWorkflowRun> runs)
    {
        var now = DateTimeOffset.UtcNow;
        Runs.Clear();
        foreach (var run in runs)
        {
            Runs.Add(new WorkflowRunItemViewModel(run, now, OpenRunCommand));
            RunDetails?.Update(run);
        }

        OnPropertyChanged(nameof(HasRuns));
    }

    [RelayCommand]
    private async Task OpenRunAsync(WorkflowRunItemViewModel? item)
    {
        if (item is not null && Scope is { } scope)
        {
            await ShowRunAsync(scope.Owner, scope.Name, item.Run);
        }
    }
}
