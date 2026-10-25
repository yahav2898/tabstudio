using TabStudio.App.Services.Ci;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.ViewModels.Actions;

/// <summary>How a run, job, step or check is drawn: its icon and color, as on GitHub.</summary>
public enum RunVisual
{
    /// <summary>Nothing to show: no checks.</summary>
    None,

    /// <summary>Waiting to start: an amber clock.</summary>
    Queued,

    /// <summary>Running: an amber spinner.</summary>
    Running,

    /// <summary>Passed: a green check.</summary>
    Passed,

    /// <summary>Failed: a red cross.</summary>
    Failed,

    /// <summary>Cancelled: a gray slashed circle.</summary>
    Cancelled,

    /// <summary>Skipped: a gray slashed circle, dimmer.</summary>
    Skipped,

    /// <summary>Finished without passing or failing: a gray dash.</summary>
    Neutral,
}

/// <summary>Picks the <see cref="RunVisual"/> of a state.</summary>
public static class RunVisuals
{
    /// <summary>The visual of a run, job, step or check run.</summary>
    /// <param name="status">Where it is.</param>
    /// <param name="conclusion">How it ended.</param>
    public static RunVisual For(GitHubRunStatus status, GitHubConclusion? conclusion) => status switch
    {
        GitHubRunStatus.Completed => conclusion switch
        {
            GitHubConclusion.Success => RunVisual.Passed,
            GitHubConclusion.Cancelled => RunVisual.Cancelled,
            GitHubConclusion.Skipped => RunVisual.Skipped,
            GitHubConclusion.Neutral or GitHubConclusion.Stale or GitHubConclusion.Unknown or null => RunVisual.Neutral,
            _ => RunVisual.Failed,
        },
        GitHubRunStatus.InProgress => RunVisual.Running,
        _ => RunVisual.Queued,
    };

    /// <summary>The visual of a commit's CI.</summary>
    /// <param name="state">The state of all its checks together.</param>
    public static RunVisual For(CiState state) => state switch
    {
        CiState.Pending => RunVisual.Queued,
        CiState.Running => RunVisual.Running,
        CiState.Passed => RunVisual.Passed,
        CiState.Failed => RunVisual.Failed,
        CiState.Cancelled => RunVisual.Cancelled,
        _ => RunVisual.None,
    };
}
