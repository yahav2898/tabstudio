using System.Globalization;
using TabStudio.App.Services;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>Job logs: tapping a job, a step or a check opens the job's log in a read-only editor tab.</summary>
public sealed partial class ActionsViewModel
{
    /// <summary>The folder log tabs are named under, next to the workspace's files. It never exists on disk.</summary>
    private const string LogFolder = "GitHub Actions";

    /// <summary>Opens a job's log, showing a step's part of it, or the first error when no step is given.</summary>
    /// <param name="owner">The repository's owner.</param>
    /// <param name="repository">The repository's name.</param>
    /// <param name="run">The run.</param>
    /// <param name="job">The job.</param>
    /// <param name="step">The step to show, or <see langword="null"/>.</param>
    public async Task OpenLogAsync(string owner, string repository, GitHubWorkflowRun run, GitHubJob job, GitHubJobStep? step)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(job);
        var source = new CiLogSource(owner, repository, run, job);
        var log = new CiLogViewModel(source, () => Client, _browser);
        var shown = _host.ShowLog(log, LogTabPath(source));
        shown.ShowStep(step?.Number);
        if (ReferenceEquals(shown, log))
        {
            await log.LoadAsync();
            return;
        }

        log.Dispose();
        if (!shown.HasLog || !shown.Source.Job.IsCompleted || shown.Source.Job.Id != job.Id || shown.Source.Job.Status != job.Status)
        {
            // The tab was opened while the job ran, or before a new attempt: show the log as it is now.
            await shown.UpdateAsync(source);
        }
    }

    /// <inheritdoc />
    Task IRunDetailsHost.OpenLogAsync(string owner, string repository, GitHubWorkflowRun run, GitHubJob job, GitHubJobStep? step) =>
        OpenLogAsync(owner, repository, run, job, step);

    /// <summary>
    /// The path a log tab is known by: "GitHub Actions/CI #57/Build and test.log" next to the workspace's files,
    /// with the repository for another repository's runs and the attempt for a re-run.
    /// </summary>
    private string LogTabPath(CiLogSource source)
    {
        var run = source.Run;
        var folder = $"{run.WorkflowName} #{run.RunNumber.ToString(CultureInfo.InvariantCulture)}";
        if (run.Attempt > 1)
        {
            folder += $" attempt {run.Attempt.ToString(CultureInfo.InvariantCulture)}";
        }

        var root = Path.Combine(_host.WorkspaceRoot ?? AppContext.BaseDirectory, LogFolder);
        if (!string.Equals(WorkspaceLink?.FullName, $"{source.Owner}/{source.Repository}", StringComparison.OrdinalIgnoreCase))
        {
            root = Path.Combine(root, AppPaths.MakeSafeFileName($"{source.Owner}-{source.Repository}"));
        }

        return Path.Combine(root, AppPaths.MakeSafeFileName(folder), AppPaths.MakeSafeFileName(source.Job.Name) + ".log");
    }
}
