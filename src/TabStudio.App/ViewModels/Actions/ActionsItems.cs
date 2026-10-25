using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>A check of the workspace's commit, in the GitHub Actions window's "This commit" section.</summary>
public sealed class CiCheckItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="check">The check.</param>
    /// <param name="now">The time now, for durations of running checks.</param>
    /// <param name="open">Opens the check: its log for a GitHub Actions job, its page otherwise.</param>
    public CiCheckItemViewModel(CiCheck check, DateTimeOffset now, IRelayCommand<CiCheckItemViewModel> open)
    {
        ArgumentNullException.ThrowIfNull(check);
        Check = check;
        Open = open;
        Visual = RunVisuals.For(check.Status, check.Conclusion);
        var state = ActionsFormat.State(check.Status, check.Conclusion);
        var duration = GitHubRunStates.Duration(check.StartedAt, check.CompletedAt, now);
        Detail = check.Kind == CiCheckKind.Status && check.Description is { } description
            ? description
            : duration is { } time && check.HasStarted ? $"{state} · {ActionsFormat.Duration(time)}" : state;
        Source = check.Kind switch
        {
            CiCheckKind.Job => "GitHub Actions",
            CiCheckKind.CheckRun => check.CheckRun?.App ?? "Check",
            _ => "Commit status",
        };
    }

    /// <summary>The check.</summary>
    public CiCheck Check { get; }

    /// <summary>Its name.</summary>
    public string Name => Check.Name;

    /// <summary>Its icon and color.</summary>
    public RunVisual Visual { get; }

    /// <summary>"failed · 2m 28s", or the status's description.</summary>
    public string Detail { get; }

    /// <summary>Where it comes from: "GitHub Actions", an app, or "Commit status".</summary>
    public string Source { get; }

    /// <summary>Whether tapping it opens its log in the editor, rather than its page.</summary>
    public bool OpensLog => Check.Kind == CiCheckKind.Job;

    /// <summary>The tooltip: what tapping does.</summary>
    public string ToolTip => OpensLog ? $"{Name} ({Source}): open the log" : $"{Name} ({Source}): open its page";

    /// <summary>Opens the check.</summary>
    public IRelayCommand<CiCheckItemViewModel> Open { get; }
}

/// <summary>A workflow run in the Runs list.</summary>
public sealed class WorkflowRunItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="run">The run.</param>
    /// <param name="now">The time now, for its age and running duration.</param>
    /// <param name="open">Opens its details.</param>
    public WorkflowRunItemViewModel(GitHubWorkflowRun run, DateTimeOffset now, IRelayCommand<WorkflowRunItemViewModel> open)
    {
        ArgumentNullException.ThrowIfNull(run);
        Run = run;
        Open = open;
        Visual = RunVisuals.For(run.Status, run.Conclusion);
        Workflow = $"{run.WorkflowName} #{run.RunNumber.ToString(CultureInfo.CurrentCulture)}";
        Trigger = run.HeadBranch is { } branch ? $"{ActionsFormat.Event(run.Event)} · {branch}" : ActionsFormat.Event(run.Event);
        var duration = run.Duration(now) is { } time ? ActionsFormat.Duration(time) : null;
        var parts = new[] { run.Actor, duration, ActionsFormat.Ago(run.CreatedAt, now) }.Where(p => !string.IsNullOrEmpty(p));
        Footer = string.Join(" · ", parts);
    }

    /// <summary>The run.</summary>
    public GitHubWorkflowRun Run { get; }

    /// <summary>Its title, usually the commit's or pull request's.</summary>
    public string Title => Run.Title;

    /// <summary>Its icon and color.</summary>
    public RunVisual Visual { get; }

    /// <summary>"CI #57".</summary>
    public string Workflow { get; }

    /// <summary>"push · main".</summary>
    public string Trigger { get; }

    /// <summary>"octocat · 3m 12s · 5 min ago".</summary>
    public string Footer { get; }

    /// <summary>Opens its details.</summary>
    public IRelayCommand<WorkflowRunItemViewModel> Open { get; }
}

/// <summary>A job of the run shown in the run details, with its steps.</summary>
public sealed partial class JobItemViewModel : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="job">The job.</param>
    /// <param name="now">The time now, for running durations.</param>
    /// <param name="openLog">Opens the job's log, at a step when one is given.</param>
    public JobItemViewModel(GitHubJob job, DateTimeOffset now, Func<GitHubJob, GitHubJobStep?, Task> openLog)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(openLog);
        Job = job;
        Visual = RunVisuals.For(job.Status, job.Conclusion);
        DurationText = GitHubRunStates.Duration(job.StartedAt, job.CompletedAt, now) is { } time && job.StartedAt is not null
            ? ActionsFormat.Duration(time)
            : ActionsFormat.State(job.Status, job.Conclusion);
        Steps = [.. job.Steps.Select(s => new StepItemViewModel(s, now, () => openLog(job, s)))];
        IsExpanded = job.IsFailed || job.Status == GitHubRunStatus.InProgress;
        OpenLogCommand = new AsyncRelayCommand(() => openLog(job, null));
    }

    /// <summary>The job.</summary>
    public GitHubJob Job { get; }

    /// <summary>Its name.</summary>
    public string Name => Job.Name;

    /// <summary>Its icon and color.</summary>
    public RunVisual Visual { get; }

    /// <summary>How long it ran, or its state while it has not started.</summary>
    public string DurationText { get; }

    /// <summary>Its steps.</summary>
    public IReadOnlyList<StepItemViewModel> Steps { get; }

    /// <summary>Whether it has steps to show.</summary>
    public bool HasSteps => Steps.Count > 0;

    /// <summary>Whether its steps are shown: at first for failed and running jobs.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Opens the job's log in the editor.</summary>
    public IAsyncRelayCommand OpenLogCommand { get; }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

/// <summary>A step of a job in the run details.</summary>
public sealed class StepItemViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="step">The step.</param>
    /// <param name="now">The time now, for running durations.</param>
    /// <param name="openLog">Opens the job's log at this step.</param>
    public StepItemViewModel(GitHubJobStep step, DateTimeOffset now, Func<Task> openLog)
    {
        ArgumentNullException.ThrowIfNull(step);
        Step = step;
        Visual = RunVisuals.For(step.Status, step.Conclusion);
        DurationText = step.StartedAt is not null && GitHubRunStates.Duration(step.StartedAt, step.CompletedAt, now) is { } time
            ? ActionsFormat.Duration(time)
            : string.Empty;
        OpenLogCommand = new AsyncRelayCommand(openLog);
    }

    /// <summary>The step.</summary>
    public GitHubJobStep Step { get; }

    /// <summary>Its number in the job.</summary>
    public string Number => Step.Number.ToString(CultureInfo.CurrentCulture);

    /// <summary>Its name.</summary>
    public string Name => Step.Name;

    /// <summary>Its icon and color.</summary>
    public RunVisual Visual { get; }

    /// <summary>How long it ran; empty when it did not run.</summary>
    public string DurationText { get; }

    /// <summary>Opens the job's log at this step.</summary>
    public IAsyncRelayCommand OpenLogCommand { get; }
}

/// <summary>A workflow in the Workflows section, with "Run workflow..." when it can be run by hand.</summary>
public sealed partial class WorkflowItemViewModel : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="run">Opens the "Run workflow" form for it.</param>
    public WorkflowItemViewModel(GitHubWorkflow workflow, IAsyncRelayCommand<WorkflowItemViewModel> run)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        Workflow = workflow;
        Run = run;
    }

    /// <summary>The workflow.</summary>
    public GitHubWorkflow Workflow { get; }

    /// <summary>Its name.</summary>
    public string Name => Workflow.Name;

    /// <summary>Its file.</summary>
    public string Path => Workflow.Path;

    /// <summary>What its file says about running it by hand, once read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDispatch), nameof(Note))]
    public partial WorkflowDefinition? Definition { get; set; }

    /// <summary>Why its file could not be read, if it could not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Note))]
    public partial string? ErrorText { get; set; }

    /// <summary>Whether it can be run by hand: active, with a <c>workflow_dispatch</c> trigger.</summary>
    public bool CanDispatch => Workflow.IsActive && Definition?.CanDispatch == true;

    /// <summary>What the row says under the name besides the file: why it cannot be run by hand.</summary>
    public string? Note => !Workflow.IsActive ? "Disabled on GitHub"
        : ErrorText is not null ? ErrorText
        : Definition is { CanDispatch: false } ? "Runs on its triggers only"
        : null;

    /// <summary>Opens the "Run workflow" form.</summary>
    public IAsyncRelayCommand<WorkflowItemViewModel> Run { get; }
}

/// <summary>A repository the user watches, with the latest run on its default branch.</summary>
public sealed partial class WatchedRepositoryItemViewModel : ObservableObject
{
    /// <summary>Creates the row.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="name">The repository name.</param>
    /// <param name="open">Shows the repository's runs.</param>
    /// <param name="remove">Stops watching it.</param>
    public WatchedRepositoryItemViewModel(string owner, string name, IRelayCommand<WatchedRepositoryItemViewModel> open, IRelayCommand<WatchedRepositoryItemViewModel> remove)
    {
        Owner = owner;
        Name = name;
        Open = open;
        Remove = remove;
    }

    /// <summary>The owner.</summary>
    public string Owner { get; }

    /// <summary>The repository name.</summary>
    public string Name { get; }

    /// <summary>"owner/name".</summary>
    public string FullName => $"{Owner}/{Name}";

    /// <summary>The repository's default branch, once read.</summary>
    public string? DefaultBranch { get; set; }

    /// <summary>The latest run on the default branch, once read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visual), nameof(Summary))]
    public partial GitHubWorkflowRun? LatestRun { get; private set; }

    /// <summary>Why the latest run could not be read, if it could not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visual), nameof(Summary))]
    public partial string? ErrorText { get; private set; }

    /// <summary>Whether the repository has been read once.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>The latest run's icon and color.</summary>
    public RunVisual Visual => LatestRun is { } run ? RunVisuals.For(run.Status, run.Conclusion) : RunVisual.None;

    /// <summary>"CI #12 · main · 2 h ago", or why there is nothing to show.</summary>
    public string Summary => ErrorText
        ?? (LatestRun is { } run ? $"{run.WorkflowName} #{run.RunNumber.ToString(CultureInfo.CurrentCulture)} · {ActionsFormat.State(run.Status, run.Conclusion)} · {ActionsFormat.Ago(run.CreatedAt, DateTimeOffset.UtcNow)}"
        : IsLoaded ? $"No runs on {DefaultBranch ?? "the default branch"}"
        : "Loading…");

    /// <summary>Shows the repository's runs.</summary>
    public IRelayCommand<WatchedRepositoryItemViewModel> Open { get; }

    /// <summary>Stops watching it.</summary>
    public IRelayCommand<WatchedRepositoryItemViewModel> Remove { get; }

    /// <summary>Shows what GitHub answered.</summary>
    /// <param name="run">The latest run, or <see langword="null"/> when there is none.</param>
    /// <param name="error">Why it could not be read, or <see langword="null"/>.</param>
    public void Show(GitHubWorkflowRun? run, string? error)
    {
        LatestRun = run;
        ErrorText = error;
        IsLoaded = true;
    }
}
