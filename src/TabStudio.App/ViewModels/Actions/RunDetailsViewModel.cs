using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>What the run details need from the GitHub Actions window.</summary>
internal interface IRunDetailsHost
{
    /// <summary>The client to read and act with.</summary>
    GitHubClient Client { get; }

    /// <summary>Follows a run started or re-run from TabStudio, to tell the user when it finishes.</summary>
    void Watch(string owner, string repository, GitHubWorkflowRun run, bool isRerun);

    /// <summary>Opens a job's log, at a step when one is given.</summary>
    Task OpenLogAsync(string owner, string repository, GitHubWorkflowRun run, GitHubJob job, GitHubJobStep? step);

    /// <summary>Opens a page in the browser.</summary>
    Task OpenUriAsync(Uri uri);

    /// <summary>Asks the user to confirm.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirm, bool destructive);

    /// <summary>Goes back from the details to the window's main page.</summary>
    void CloseRunDetails();
}

/// <summary>
/// A workflow run's details in the GitHub Actions window: its jobs and their steps, and re-running, cancelling
/// and opening it on GitHub.
/// </summary>
public sealed partial class RunDetailsViewModel : ObservableObject
{
    private readonly IRunDetailsHost _host;

    internal RunDetailsViewModel(string owner, string repository, GitHubWorkflowRun run, IRunDetailsHost host)
    {
        Owner = owner;
        Repository = repository;
        Run = run;
        _host = host;
    }

    /// <summary>The repository's owner.</summary>
    public string Owner { get; }

    /// <summary>The repository's name.</summary>
    public string Repository { get; }

    /// <summary>"owner/name".</summary>
    public string RepositoryName => $"{Owner}/{Repository}";

    /// <summary>The run, as last read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visual), nameof(Title), nameof(Header), nameof(Meta), nameof(StateText), nameof(IsCompleted))]
    [NotifyCanExecuteChangedFor(nameof(RerunCommand), nameof(RerunFailedJobsCommand), nameof(CancelCommand))]
    public partial GitHubWorkflowRun Run { get; private set; }

    /// <summary>The jobs of the run's latest attempt.</summary>
    public ObservableCollection<JobItemViewModel> Jobs { get; } = [];

    /// <summary>Whether the jobs are loading for the first time.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Whether a re-run or cancel is being sent.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RerunCommand), nameof(RerunFailedJobsCommand), nameof(CancelCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>What went wrong last, in plain words.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>What the last action did.</summary>
    [ObservableProperty]
    public partial string? InfoText { get; private set; }

    /// <summary>The run's icon and color.</summary>
    public RunVisual Visual => RunVisuals.For(Run.Status, Run.Conclusion);

    /// <summary>The run's title.</summary>
    public string Title => Run.Title;

    /// <summary>"CI #57 · attempt 2".</summary>
    public string Header => Run.Attempt > 1
        ? $"{Run.WorkflowName} #{Run.RunNumber.ToString(CultureInfo.CurrentCulture)} · attempt {Run.Attempt.ToString(CultureInfo.CurrentCulture)}"
        : $"{Run.WorkflowName} #{Run.RunNumber.ToString(CultureInfo.CurrentCulture)}";

    /// <summary>"push · main · 1a2b3c4 · octocat".</summary>
    public string Meta
    {
        get
        {
            var parts = new List<string> { ActionsFormat.Event(Run.Event) };
            if (Run.HeadBranch is { } branch)
            {
                parts.Add(branch);
            }

            parts.Add(Run.HeadSha.Length > 7 ? Run.HeadSha[..7] : Run.HeadSha);
            if (Run.Actor is { } actor)
            {
                parts.Add(actor);
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>"Failed after 3m 12s, 5 min ago" or "In progress for 1m 3s".</summary>
    public string StateText
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            var duration = Run.Duration(now) is { } time ? ActionsFormat.Duration(time) : null;
            var state = ActionsFormat.State(Run.Status, Run.Conclusion);
            var capitalized = char.ToUpper(state[0], CultureInfo.CurrentCulture) + state[1..];
            return Run.IsCompleted
                ? $"{capitalized}{(duration is null ? string.Empty : $" in {duration}")} · {ActionsFormat.Ago(Run.UpdatedAt, now)}"
                : duration is null ? capitalized : $"{capitalized} for {duration}";
        }
    }

    /// <summary>Whether the run finished.</summary>
    public bool IsCompleted => Run.IsCompleted;

    /// <summary>Whether a job failed, so "Re-run failed jobs" applies.</summary>
    public bool HasFailedJobs => Jobs.Any(j => j.Job.IsFailed || j.Job.Conclusion == GitHubConclusion.Cancelled);

    /// <summary>Reads the run and its jobs again, keeping which jobs are expanded.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = Jobs.Count == 0;
        try
        {
            var client = _host.Client;
            var run = await client.GetWorkflowRunAsync(Owner, Repository, Run.Id, cancellationToken);
            var jobs = await client.GetJobsAsync(Owner, Repository, Run.Id, cancellationToken);
            Run = run;
            ShowJobs(jobs);
            ErrorText = null;
        }
        catch (GitHubApiException ex)
        {
            ErrorText = ActionsErrors.Describe(ex, _host.Client.IsSignedIn);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Shows a run read elsewhere, such as in the runs list, when it is newer.</summary>
    /// <param name="run">The run.</param>
    public void Update(GitHubWorkflowRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Id == Run.Id && run.UpdatedAt >= Run.UpdatedAt)
        {
            Run = run;
        }
    }

    private void ShowJobs(IReadOnlyList<GitHubJob> jobs)
    {
        var expanded = Jobs.ToDictionary(j => j.Job.Name, j => j.IsExpanded, StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        Jobs.Clear();
        foreach (var job in jobs)
        {
            var item = new JobItemViewModel(job, now, (j, step) => _host.OpenLogAsync(Owner, Repository, Run, j, step));
            if (expanded.TryGetValue(job.Name, out var wasExpanded) && !job.IsFailed)
            {
                item.IsExpanded = wasExpanded;
            }

            Jobs.Add(item);
        }

        OnPropertyChanged(nameof(HasFailedJobs));
        RerunFailedJobsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRerun))]
    private Task RerunAsync() => ActAsync(
        (client, token) => client.RerunWorkflowAsync(Owner, Repository, Run.Id, token),
        $"Re-running every job of {Header}.",
        isRerun: true);

    [RelayCommand(CanExecute = nameof(CanRerunFailed))]
    private Task RerunFailedJobsAsync() => ActAsync(
        (client, token) => client.RerunFailedJobsAsync(Owner, Repository, Run.Id, token),
        $"Re-running the failed jobs of {Header}.",
        isRerun: true);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        if (await _host.ConfirmAsync($"Cancel {Header}?", "Its jobs stop where they are. You can re-run it afterwards.", "Cancel Run", destructive: true))
        {
            await ActAsync((client, token) => client.CancelWorkflowRunAsync(Owner, Repository, Run.Id, token), $"Cancelling {Header}.", isRerun: false);
        }
    }

    [RelayCommand]
    private Task OpenOnGitHubAsync() => _host.OpenUriAsync(Run.WebUri);

    [RelayCommand]
    private void Back() => _host.CloseRunDetails();

    private async Task ActAsync(Func<GitHubClient, CancellationToken, Task> action, string message, bool isRerun)
    {
        ErrorText = null;
        InfoText = null;
        IsBusy = true;
        var client = _host.Client;
        try
        {
            var before = Run;
            await action(client, CancellationToken.None);
            InfoText = message;
            if (isRerun)
            {
                // Re-runs are followed until they finish, to tell the user how they ended.
                _host.Watch(Owner, Repository, before, isRerun: true);
            }

            await RefreshAsync();
        }
        catch (GitHubApiException ex)
        {
            ErrorText = ActionsErrors.DescribeAction(ex, client.IsSignedIn);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRerun() => !IsBusy && Run.IsCompleted;

    private bool CanRerunFailed() => !IsBusy && Run.IsCompleted && HasFailedJobs;

    private bool CanCancel() => !IsBusy && !Run.IsCompleted;
}
