using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>
/// The Workflows section and "Run workflow...": each workflow's file is read for its <c>workflow_dispatch</c>
/// trigger and inputs, and the form built from them starts a run.
/// </summary>
public sealed partial class ActionsViewModel
{
    private ActionsScope? _workflowsRead;

    /// <summary>The repository's workflows.</summary>
    public ObservableCollection<WorkflowItemViewModel> Workflows { get; } = [];

    /// <summary>Why the workflows could not be read.</summary>
    [ObservableProperty]
    public partial string? WorkflowsErrorText { get; private set; }

    /// <summary>The "Run workflow" form, while it is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDispatchFormOpen))]
    public partial DispatchFormViewModel? DispatchForm { get; private set; }

    /// <summary>Whether the "Run workflow" form is open.</summary>
    public bool IsDispatchFormOpen => DispatchForm is not null;

    /// <summary>Whether the repository has workflows.</summary>
    public bool HasWorkflows => Workflows.Count > 0;

    /// <summary>Reads the workflows, and the files of those whose dispatch trigger is not known yet.</summary>
    private async Task RefreshWorkflowsAsync()
    {
        if (Scope is not { } scope)
        {
            return;
        }

        try
        {
            var workflows = await Client.GetWorkflowsAsync(scope.Owner, scope.Name, _lifetime.Token);
            if (Scope != scope)
            {
                return;
            }

            WorkflowsErrorText = null;
            ShowWorkflows(scope, workflows);
            foreach (var item in Workflows.Where(w => w.Workflow.IsActive && w.Definition is null && w.ErrorText is null).ToList())
            {
                await ReadDefinitionAsync(scope, item);
            }
        }
        catch (GitHubApiException ex)
        {
            WorkflowsErrorText = Describe(ex);
        }
    }

    private void ShowWorkflows(ActionsScope scope, IReadOnlyList<GitHubWorkflow> workflows)
    {
        var known = Workflows.ToDictionary(w => w.Workflow.Id);
        var same = _workflowsRead == scope && workflows.Count == Workflows.Count && workflows.All(w => known.TryGetValue(w.Id, out var item) && item.Workflow == w);
        if (same)
        {
            return;
        }

        _workflowsRead = scope;
        Workflows.Clear();
        foreach (var workflow in workflows.OrderBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new WorkflowItemViewModel(workflow, OpenDispatchFormCommand);
            if (known.TryGetValue(workflow.Id, out var previous) && previous.Workflow == workflow)
            {
                item.Definition = previous.Definition;
            }

            Workflows.Add(item);
        }

        var chosen = WorkflowFilter?.WorkflowId;
        _suppressRunRefresh = true;
        WorkflowFilters.Clear();
        WorkflowFilters.Add(AllWorkflows);
        foreach (var item in Workflows)
        {
            WorkflowFilters.Add(new RunWorkflowFilter(item.Name, item.Workflow.Id));
        }

        WorkflowFilter = WorkflowFilters.FirstOrDefault(f => f.WorkflowId == chosen) ?? AllWorkflows;
        _suppressRunRefresh = false;
        OnPropertyChanged(nameof(HasWorkflows));
    }

    private async Task ReadDefinitionAsync(ActionsScope scope, WorkflowItemViewModel item)
    {
        try
        {
            item.Definition = await Client.GetWorkflowDefinitionAsync(scope.Owner, scope.Name, item.Workflow, scope.Branch, _lifetime.Token);
        }
        catch (GitHubApiException ex)
        {
            item.ErrorText = ex.IsRejected ? "Its file is not valid YAML" : "Its file could not be read";
        }
    }

    /// <summary>Opens the "Run workflow" form on the scope's branch, then lists the repository's branches in it.</summary>
    [RelayCommand]
    private async Task OpenDispatchFormAsync(WorkflowItemViewModel? item)
    {
        if (item is null || Scope is not { } scope)
        {
            return;
        }

        ErrorText = null;
        try
        {
            var definition = item.Definition ?? await Client.GetWorkflowDefinitionAsync(scope.Owner, scope.Name, item.Workflow, scope.Branch, _lifetime.Token);
            if (!definition.CanDispatch)
            {
                ErrorText = $"{item.Name} cannot be run by hand: its file has no workflow_dispatch trigger.";
                return;
            }

            var branch = scope.Branch ?? (await Client.GetRepositoryAsync(scope.Owner, scope.Name, _lifetime.Token)).DefaultBranch;
            var form = new DispatchFormViewModel(scope.Owner, scope.Name, item.Workflow, definition, branch, RunDispatchAsync);
            DispatchForm = form;
            var branches = await Client.GetBranchesAsync(scope.Owner, scope.Name, _lifetime.Token);
            form.ShowBranches(branches.Select(b => b.Name));
            form.BranchChanged += OnDispatchBranchChanged;
        }
        catch (GitHubApiException ex)
        {
            if (DispatchForm is { } open)
            {
                open.ErrorText = Describe(ex);
            }
            else
            {
                ErrorText = Describe(ex);
            }
        }
    }

    /// <summary>A branch's version of the workflow may ask for other inputs: read it again.</summary>
    private async void OnDispatchBranchChanged(object? sender, string branch)
    {
        if (sender is not DispatchFormViewModel form || form != DispatchForm)
        {
            return;
        }

        try
        {
            var definition = await Client.GetWorkflowDefinitionAsync(form.Owner, form.Repository, form.Workflow, branch, _lifetime.Token);
            form.ShowDefinition(definition, branch);
        }
        catch (GitHubApiException ex)
        {
            form.ErrorText = Describe(ex);
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
    }

    [RelayCommand]
    private void CloseDispatchForm()
    {
        if (DispatchForm is { } form)
        {
            form.BranchChanged -= OnDispatchBranchChanged;
        }

        DispatchForm = null;
    }

    /// <summary>Starts the run, follows it so its end is reported, and shows it under Runs once GitHub lists it.</summary>
    private async Task RunDispatchAsync(DispatchFormViewModel form)
    {
        if (form.Branch is not { } branch)
        {
            return;
        }

        form.IsBusy = true;
        form.ErrorText = null;
        try
        {
            await Client.DispatchWorkflowAsync(form.Owner, form.Repository, form.Workflow.Id, branch, form.Values, _lifetime.Token);
            Monitor.WatchDispatch(form.Owner, form.Repository, form.Workflow.Id, branch);
            CloseDispatchForm();
            InfoText = $"Started {form.Workflow.Name} on {branch}. It appears under Runs in a moment.";
            _host.ReportStatus(InfoText);
            await RefreshRunsAsync();
            RequestWindowRefresh();
        }
        catch (GitHubApiException ex)
        {
            form.ErrorText = ActionsErrors.DescribeAction(ex, Client.IsSignedIn);
        }
        finally
        {
            form.IsBusy = false;
        }
    }
}
