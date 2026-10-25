using System.Globalization;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Services.Ci;

/// <summary>How the GitHub Actions window writes durations, times and events, the way GitHub's own pages do.</summary>
public static class ActionsFormat
{
    /// <summary>"45s", "3m 12s", "1h 4m".</summary>
    /// <param name="duration">How long.</param>
    public static string Duration(TimeSpan duration)
    {
        var culture = CultureInfo.CurrentCulture;
        return duration.TotalHours >= 1 ? $"{((int)duration.TotalHours).ToString(culture)}h {duration.Minutes.ToString(culture)}m"
            : duration.TotalMinutes >= 1 ? $"{duration.Minutes.ToString(culture)}m {duration.Seconds.ToString(culture)}s"
            : $"{Math.Max(0, duration.Seconds).ToString(culture)}s";
    }

    /// <summary>"just now", "5 min ago", "3 h ago", "yesterday", "12 Oct".</summary>
    /// <param name="time">When.</param>
    /// <param name="now">The time now.</param>
    public static string Ago(DateTimeOffset time, DateTimeOffset now)
    {
        var age = now - time;
        var culture = CultureInfo.CurrentCulture;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{((int)age.TotalMinutes).ToString(culture)} min ago"
            : age.TotalDays < 1 ? $"{((int)age.TotalHours).ToString(culture)} h ago"
            : age.TotalDays < 2 ? "yesterday"
            : age.TotalDays < 7 ? $"{((int)age.TotalDays).ToString(culture)} days ago"
            : time.ToLocalTime().ToString("d MMM", culture);
    }

    /// <summary>What started a run, in words: "push", "pull request", "manual", "schedule".</summary>
    /// <param name="name">The event's name in the API, such as <c>workflow_dispatch</c>.</param>
    public static string Event(string name) => name switch
    {
        "workflow_dispatch" => "manual",
        "pull_request" or "pull_request_target" => "pull request",
        "schedule" => "schedule",
        "workflow_run" => "after a workflow",
        "repository_dispatch" => "API",
        "" => "run",
        _ => name.Replace('_', ' '),
    };

    /// <summary>How a run, job, step or check stands, in words: "queued", "in progress", "failed", "succeeded".</summary>
    /// <param name="status">Where it is.</param>
    /// <param name="conclusion">How it ended.</param>
    public static string State(GitHubRunStatus status, GitHubConclusion? conclusion) => status switch
    {
        GitHubRunStatus.Completed => conclusion switch
        {
            GitHubConclusion.Success => "succeeded",
            GitHubConclusion.Failure => "failed",
            GitHubConclusion.Cancelled => "cancelled",
            GitHubConclusion.Skipped => "skipped",
            GitHubConclusion.TimedOut => "timed out",
            GitHubConclusion.ActionRequired => "needs action",
            GitHubConclusion.StartupFailure => "could not start",
            GitHubConclusion.Neutral => "neutral",
            GitHubConclusion.Stale => "stale",
            _ => "completed",
        },
        GitHubRunStatus.InProgress => "in progress",
        GitHubRunStatus.Waiting => "waiting",
        GitHubRunStatus.Pending or GitHubRunStatus.Requested => "pending",
        _ => "queued",
    };
}
